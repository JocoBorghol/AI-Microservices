using IntelligentSalesAssistantAPI.Data;
using IntelligentSalesAssistantAPI.DTOs;
using IntelligentSalesAssistantAPI.Exceptions;
using IntelligentSalesAssistantAPI.Filters;
using IntelligentSalesAssistantAPI.Http.Clients;
using IntelligentSalesAssistantAPI.Services;
using IntelligentSalesAssistantAPI.Services.Enrichment;
using IntelligentSalesAssistantAPI.Services.WebsiteGenerator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelligentSalesAssistantAPI.Controllers
{
    /// <summary>
    /// Hanterar CRUD-operationer för AI-genererade företagshemsidor
    /// </summary>
    [ApiController]
    [Route("api/websites")]
    [Authorize]
    [MeasureExecutionTime]
    public class WebsiteGeneratorController : ControllerBase
    {
        private readonly IWebsiteGeneratorService _generatorService;
        private readonly ApplicationDbContext _db;
        private readonly ITemplateService _templateService;
        private readonly ILogger<WebsiteGeneratorController> _logger;

        public WebsiteGeneratorController(
            IWebsiteGeneratorService generatorService,
            ApplicationDbContext db,
            ITemplateService templateService,
            ILogger<WebsiteGeneratorController> logger)
        {
            _generatorService = generatorService;
            _db = db;
            _templateService = templateService;
            _logger = logger;
        }

        /// <summary>
        /// Hämtar alla genererade hemsidor med valfri filtrering och sortering.
        /// </summary>
        /// <param name="category">Filtrera på bransch (t.ex. "Byggverksamhet", "IT-tjänster")</param>
        /// <param name="sort">Sorteringsordning: "createdAt" (äldst först), "-createdAt" (nyast först, standard)</param>
        /// <returns>Lista med genererade hemsidor</returns>
        /// <response code="200">Returnerar listan med hemsidor</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        [HttpGet]
        [ProducesResponseType(typeof(WebsiteListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<WebsiteListResponse>> GetAll(
            [FromQuery] string? category = null,
            [FromQuery] string? sort = "-createdAt")
        {
            var query = _db.CompanyWebsites.AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(x => x.Category == category);

            query = sort?.ToLower() switch
            {
                "createdat" => query.OrderBy(x => x.CreatedAt),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };

            var websites = await query
                .Select(x => new WebsiteResponse(
                    x.Id,
                    x.CompanyName,
                    x.OrgNumber,
                    $"/generated/{SanitizeName(x.CompanyName)}/index.html",
                    x.Category,
                    x.Tone,
                    x.TargetAudience,
                    x.CreatedAt,
                    x.UpdatedAt))
                .ToListAsync();

            return Ok(new WebsiteListResponse(websites.Count, websites));
        }

        /// <summary>
        /// Hämtar en specifik genererad hemsida baserat på ID.
        /// </summary>
        /// <param name="id">Unikt ID för hemsidan</param>
        /// <returns>Hemsideinformation</returns>
        /// <response code="200">Returnerar hemsideinformationen</response>
        /// <response code="404">Om hemsidan inte finns</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(WebsiteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<WebsiteResponse>> GetById(int id)
        {
            var entity = await _db.CompanyWebsites.FindAsync(id);
            if (entity is null) return NotFound();

            return Ok(new WebsiteResponse(
                entity.Id,
                entity.CompanyName,
                entity.OrgNumber,
                $"/generated/{SanitizeName(entity.CompanyName)}/index.html",
                entity.Category,
                entity.Tone,
                entity.TargetAudience,
                entity.CreatedAt,
                entity.UpdatedAt));
        }

        /// <summary>
        /// Genererar en ny hemsida baserat på cached enrichment data och customization.
        /// Använder automatiskt senast sparade enrichment search från POST /api/research.
        /// Hemsidan genereras med placeholder-bilder som kan ersättas manuellt.
        /// </summary>
        /// <param name="request">Customization-inställningar för hemsidan</param>
        /// <param name="researchService">Company research service (injected)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Den nyskapade hemsidan med placeholder-bilder</returns>
        /// <response code="201">Hemsidan skapades framgångsrikt</response>
        /// <response code="400">Ogiltiga customization-parametrar</response>
        /// <response code="404">Ingen cached enrichment data finns - kör POST /api/research först</response>
        /// <response code="500">Fel från Service B eller Gemini API</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        /// <remarks>
        /// Detta endpoint använder automatiskt cached enrichment data från senaste POST /api/research.
        /// Hemsidan genereras med placeholder-referenser. För att lägga till egna bilder, placera dem manuellt i Site/generated/{company}/images/.
        /// 
        /// Flöde:
        /// 1. POST /api/research med orgNumber
        /// 2. POST /api/websites med customization
        /// 
        /// Exempel 1 - Minimal request (använder AI-defaults):
        /// POST /api/websites
        /// {
        ///   "customization": {}
        /// }
        /// 
        /// Exempel 2 - Med anpassningar:
        /// POST /api/websites
        /// {
        ///   "customization": {
        ///     "tone": "professionell",
        ///     "targetAudience": "privatpersoner",
        ///     "topServices": ["Service och underhåll", "Reparationer", "Däckbyte"],
        ///     "keywords": ["bilverkstad", "professionell", "pålitlig"]
        ///   }
        /// }
        /// 
        /// Exempel 3 - Helt utan customization:
        /// POST /api/websites
        /// {}
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(WebsiteResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<WebsiteResponse>> Generate(
            [FromBody] GenerateWebsiteSimpleRequest request,
            [FromServices] ICompanyResearchService researchService,
            CancellationToken ct)
        {
            var userId = User.Identity?.Name ?? "anonymous";
            
            // Hämta från cache
            if (!CompanyResearchController.TryGetLatestSearch(userId, out var latestSearch))
            {
                _logger.LogWarning("Ingen cached enrichment data hittades för användare {UserId}", userId);
                return NotFound(new ProblemDetails
                {
                    Title = "Ingen enrichment data",
                    Detail = "Du måste först köra POST /api/research för att hämta företagsdata",
                    Status = StatusCodes.Status404NotFound
                });
            }
            
            _logger.LogInformation(
                "Genererar hemsida med cached enrichment för {CompanyName}",
                latestSearch?.Bolag.CompanyName ?? "okänt företag");
            
            // Konvertera till CompanyEnrichmentData format
            var enrichmentData = new CompanyEnrichmentData(
                Bolag: new BolagInfo(
                    OrgNumber: latestSearch!.Bolag.OrgNumber,
                    CompanyName: latestSearch.Bolag.CompanyName,
                    Address: latestSearch.Bolag.Address,
                    City: latestSearch.Bolag.City,
                    PostCode: latestSearch.Bolag.PostCode,
                    Website: latestSearch.Bolag.Website,
                    ContactPerson: latestSearch.Bolag.ContactPerson,
                    Phone: latestSearch.Bolag.Phone,
                    Email: latestSearch.Bolag.Email,
                    Industry: latestSearch.Bolag.Industry
                ),
                Google: null
            );
            
            var result = await _generatorService.GenerateWebsiteAsync(
                latestSearch.Bolag.OrgNumber, 
                request.Customization, 
                enrichmentData,
                ct);
            
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Uppdaterar och regenererar en befintlig hemsida.
        /// </summary>
        /// <param name="id">ID för hemsidan som ska regenereras</param>
        /// <param name="request">Organisationsnummer och valfria anpassningar</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Den uppdaterade hemsidan</returns>
        /// <response code="200">Hemsidan regenererades framgångsrikt</response>
        /// <response code="404">Hemsidan eller företaget finns inte</response>
        /// <response code="400">Valideringsfel</response>
        /// <response code="500">Fel från Service B</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(WebsiteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<WebsiteResponse>> Regenerate(
            int id,
            [FromBody] UpdateWebsiteRequest request,
            CancellationToken ct)
        {
            var result = await _generatorService.RegenerateWebsiteAsync(id, request.Customization, ct);
            return Ok(result);
        }

        /// <summary>
        /// Tar bort en genererad hemsida från databas och filsystem.
        /// </summary>
        /// <param name="id">ID för hemsidan som ska tas bort</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Ingen innehåll (204 No Content)</returns>
        /// <response code="204">Hemsidan togs bort framgångsrikt</response>
        /// <response code="404">Om hemsidan inte finns</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var entity = await _db.CompanyWebsites.FindAsync(new object[] { id }, ct);
            if (entity is null) return NotFound();

            await _templateService.DeleteWebsiteAsync(entity.CompanyName, ct);

            _db.CompanyWebsites.Remove(entity);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        // Hjälpmetod för att undvika beroende på TemplateService i LINQ-queries
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "okant-foretag";
            var n = name.ToLowerInvariant()
                .Replace("å", "a").Replace("ä", "a").Replace("ö", "o")
                .Replace(" ", "-");
            n = System.Text.RegularExpressions.Regex.Replace(n, @"[^a-z0-9\-]", "");
            n = System.Text.RegularExpressions.Regex.Replace(n, @"-{2,}", "-").Trim('-');
            return string.IsNullOrEmpty(n) ? "okant-foretag" : n;
        }
    }
}
