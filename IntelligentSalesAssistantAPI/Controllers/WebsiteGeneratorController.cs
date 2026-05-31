using IntelligentSalesAssistantAPI.Data;
using IntelligentSalesAssistantAPI.DTOs;
using IntelligentSalesAssistantAPI.Exceptions;
using IntelligentSalesAssistantAPI.Filters;
using IntelligentSalesAssistantAPI.Http.Clients;
using IntelligentSalesAssistantAPI.Services;
using IntelligentSalesAssistantAPI.Services.Enrichment;
using IntelligentSalesAssistantAPI.Services.WebsiteGenerator;
using IntelligentSalesAssistantAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;

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

            var username = User.Identity?.Name;
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && !string.IsNullOrEmpty(username))
            {
                query = query.Where(x => x.CreatedBy == username);
            }

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
                    x.UpdatedAt,
                    x.Theme,
                    null))
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

            if (!HasWebsiteOwnership(entity)) return Forbid();

            return Ok(new WebsiteResponse(
                entity.Id,
                entity.CompanyName,
                entity.OrgNumber,
                $"/generated/{SanitizeName(entity.CompanyName)}/index.html",
                entity.Category,
                entity.Tone,
                entity.TargetAudience,
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.Theme,
                entity.GeneratedContentJson));
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
                userId,
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
            var entity = await _db.CompanyWebsites.FindAsync(new object[] { id }, ct);
            if (entity is null) return NotFound();

            if (!HasWebsiteOwnership(entity)) return Forbid();

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

            if (!HasWebsiteOwnership(entity)) return Forbid();

            await _templateService.DeleteWebsiteAsync(entity.CompanyName, ct);

            _db.CompanyWebsites.Remove(entity);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        /// <summary>
        /// Byter CSS-tema på en befintlig hemsida utan att regenerera innehållet.
        /// Uppdaterar databasen och skriver om stylesheet-länken i index.html direkt.
        /// </summary>
        /// <param name="id">ID för hemsidan</param>
        /// <param name="request">Temat som ska användas (t.ex. "ocean", "dark", "forest")</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Den uppdaterade hemsidan med nytt tema</returns>
        /// <response code="200">Temat byttes framgångsrikt</response>
        /// <response code="400">Okänt tema eller inga ändringar</response>
        /// <response code="404">Hemsidan finns inte</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        [HttpPatch("{id}/theme")]
        [ProducesResponseType(typeof(WebsiteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<WebsiteResponse>> ApplyTheme(
            int id,
            [FromBody] ApplyThemeRequest request,
            CancellationToken ct)
        {
            // Validera mot tillåten temalist
            var allowedThemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "original", "dark", "forest", "ocean", "nordic", "warm",
                "sunset", "mint", "rose", "slate", "purple", "terracotta"
            };

            if (!allowedThemes.Contains(request.Theme))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Okänt tema",
                    Detail = $"Temat '{request.Theme}' finns inte. Tillåtna teman: {string.Join(", ", allowedThemes)}",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var entity = await _db.CompanyWebsites.FindAsync(new object[] { id }, ct);
            if (entity is null) return NotFound();

            if (!HasWebsiteOwnership(entity)) return Forbid();

            // Uppdatera databasen
            entity.Theme = request.Theme.ToLowerInvariant();
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Skriv om stylesheet-länken i den genererade index.html
            var siteRoot = Path.Combine(
                Directory.GetCurrentDirectory(), "..", "Site", "generated",
                SanitizeName(entity.CompanyName));
            var htmlPath = Path.GetFullPath(Path.Combine(siteRoot, "index.html"));

            if (System.IO.File.Exists(htmlPath))
            {
                var html = await System.IO.File.ReadAllTextAsync(htmlPath, ct);

                // Byt ut befintlig stylesheet-referens (styles.css eller themes/styles-*.css)
                var newLink = entity.Theme == "original"
                    ? "<link rel=\"stylesheet\" href=\"styles.css\">"
                    : $"<link rel=\"stylesheet\" href=\"themes/styles-{entity.Theme}.css\">";

                // Ersatt båda möjliga format
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"<link\s+rel=""stylesheet""\s+href=""(styles\.css|themes/styles-[\w-]+\.css)""\s*>",
                    newLink);

                await System.IO.File.WriteAllTextAsync(htmlPath, html, ct);
                _logger.LogInformation(
                    "Tema '{Theme}' applicerat på {CompanyName} (fil: {Path})",
                    entity.Theme, entity.CompanyName, htmlPath);
            }
            else
            {
                _logger.LogWarning("index.html hittades inte på sökväg: {Path}", htmlPath);
            }

            return Ok(new WebsiteResponse(
                entity.Id,
                entity.CompanyName,
                entity.OrgNumber,
                $"/generated/{SanitizeName(entity.CompanyName)}/index.html",
                entity.Category,
                entity.Tone,
                entity.TargetAudience,
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.Theme,
                entity.GeneratedContentJson));
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

        /// <summary>
        /// Laddar upp en bild till en specifik plats på hemsidan (hero, about, service1-3).
        /// Bilden sparas direkt i images/-mappen och syns vid nästa sidladdning.
        /// </summary>
        /// <param name="id">ID för hemsidan</param>
        /// <param name="image">Bildfilen (jpg, png, webp — max 10 MB)</param>
        /// <param name="slot">Bildplats: hero | about | service1 | service2 | service3</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Bekräftelse med sökväg till uppladdad bild</returns>
        [HttpPost("{id}/images")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
        public async Task<IActionResult> UploadImage(
            int id,
            IFormFile image,
            [FromForm] string slot,
            CancellationToken ct)
        {
            // Validera slot
            var allowedSlots = new HashSet<string> { "hero", "about", "service1", "service2", "service3" };
            var isServicePattern = System.Text.RegularExpressions.Regex.IsMatch(slot ?? "", @"^service-\d+-\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!allowedSlots.Contains(slot?.ToLower() ?? "") && !isServicePattern)
                return BadRequest(new ProblemDetails
                {
                    Title = "Ogiltigt bildslot",
                    Detail = $"Slot '{slot}' är inte giltigt. Tillåtna: {string.Join(", ", allowedSlots)} eller 'service-X-Y'.",
                    Status = 400
                });

            // Validera filtyp
            var allowedTypes = new HashSet<string> { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowedTypes.Contains((image.ContentType ?? "").ToLower()))
                return BadRequest(new ProblemDetails
                {
                    Title = "Ogiltig filtyp",
                    Detail = "Endast JPG, PNG och WebP är tillåtna.",
                    Status = 400
                });

            // Hämta hemsidan
            var entity = await _db.CompanyWebsites.FindAsync(new object[] { id }, ct);
            if (entity is null) return NotFound();

            if (!HasWebsiteOwnership(entity)) return Forbid();

            // Bygg sökväg till images-mappen
            var siteRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "Site", "generated",
                SanitizeName(entity.CompanyName)));
            var imagesPath = Path.Combine(siteRoot, "images");
            Directory.CreateDirectory(imagesPath);

            // Spara alltid som .jpg (konverteras inte, men döps om)
            var fileName = $"{slot!.ToLower()}.jpg";
            var filePath = Path.Combine(imagesPath, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await image.CopyToAsync(stream, ct);

            _logger.LogInformation(
                "Bild uppladdad för {CompanyName}: slot={Slot}, fil={File}",
                entity.CompanyName, slot, fileName);

            return Ok(new { slot, path = $"/generated/{SanitizeName(entity.CompanyName)}/images/{fileName}", message = "Bild uppladdad." });
        }

        /// <summary>
        /// Uppdaterar kontaktuppgifter direkt i den genererade HTML-filen utan att regenerera hemsidan.
        /// </summary>
        [HttpPatch("{id}/contact")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateContact(
            int id,
            [FromBody] UpdateContactRequest request,
            CancellationToken ct)
        {
            var entity = await _db.CompanyWebsites.FindAsync(new object[] { id }, ct);
            if (entity is null) return NotFound();

            if (!HasWebsiteOwnership(entity)) return Forbid();

            var siteRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "Site", "generated",
                SanitizeName(entity.CompanyName)));
            var htmlPath = Path.Combine(siteRoot, "index.html");

            if (!System.IO.File.Exists(htmlPath))
                return NotFound(new ProblemDetails { Title = "HTML-filen hittades inte." });

            var html = await System.IO.File.ReadAllTextAsync(htmlPath, ct);
            var opts = System.Text.RegularExpressions.RegexOptions.Singleline;
            string Rx(string input, string pattern, string replacement, System.Text.RegularExpressions.RegexOptions o = System.Text.RegularExpressions.RegexOptions.None)
                => System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement, o);
            string Enc(string s) => System.Net.WebUtility.HtmlEncode(s);

            // Telefon — uppdatera href och synlig text
            if (request.Phone is not null)
            {
                html = Rx(html, @"href=""tel:[^""]*""", $@"href=""tel:{request.Phone}""");
                html = Rx(html, @"(<a href=""tel:[^""]*""[^>]*>)[^<]*(</a>)", $"$1{Enc(request.Phone)}$2");
            }

            // E-post — uppdatera href och synlig text
            if (request.Email is not null)
            {
                html = Rx(html, @"href=""mailto:[^""]*""", $@"href=""mailto:{request.Email}""");
                html = Rx(html, @"(<a href=""mailto:[^""]*""[^>]*>)[^<]*(</a>)", $"$1{Enc(request.Email)}$2");
            }

            // Adress — ta bort befintlig och lägg till ny
            if (request.Address is not null)
            {
                html = Rx(html,
                    @"<div class='dc-item'>\s*<i class='fas fa-map-marker-alt'></i>.*?</div>\s*</div>",
                    "", opts);

                if (!string.IsNullOrWhiteSpace(request.Address))
                {
                    var addressItem = $"<div class='dc-item'><i class='fas fa-map-marker-alt'></i><div><strong>Adress </strong><span>{Enc(request.Address)}</span></div></div>";
                    html = Rx(html,
                        @"(<div class='dc-item'>\s*<i class=""fas fa-user-circle""></i>.*?</div>\s*</div>)",
                        $"$1\n                        {addressItem}", opts);
                }
            }

            // Facebook
            if (request.FacebookUrl is not null)
            {
                var fbLink = string.IsNullOrWhiteSpace(request.FacebookUrl) ? "" :
                    $@"<a href=""{request.FacebookUrl}"" target=""_blank"" rel=""noopener"" class=""social-link""><i class=""fab fa-facebook""></i></a>";
                html = Rx(html,
                    @"<a href=""[^""]*""[^>]*class=""social-link""><i class=""fab fa-facebook""></i></a>",
                    fbLink);
            }

            // Instagram
            if (request.InstagramUrl is not null)
            {
                var igLink = string.IsNullOrWhiteSpace(request.InstagramUrl) ? "" :
                    $@"<a href=""{request.InstagramUrl}"" target=""_blank"" rel=""noopener"" class=""social-link""><i class=""fab fa-instagram""></i></a>";
                html = Rx(html,
                    @"<a href=""[^""]*""[^>]*class=""social-link""><i class=""fab fa-instagram""></i></a>",
                    igLink);
            }

            await System.IO.File.WriteAllTextAsync(htmlPath, html, ct);

            // Keep GeneratedContentJson in sync with contact edits
            if (!string.IsNullOrEmpty(entity.GeneratedContentJson))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<WebsiteContentResponse>(entity.GeneratedContentJson);
                    if (content is not null)
                    {
                        var updatedContact = new WebsiteContactInfo(
                            IntroText: content.Contact?.IntroText ?? "",
                            Phone: request.Phone ?? content.Contact?.Phone ?? "",
                            Email: request.Email ?? content.Contact?.Email ?? "",
                            Address: request.Address ?? content.Contact?.Address,
                            FacebookUrl: request.FacebookUrl ?? content.Contact?.FacebookUrl,
                            InstagramUrl: request.InstagramUrl ?? content.Contact?.InstagramUrl
                        );

                        var updatedContent = content with { Contact = updatedContact };
                        entity.GeneratedContentJson = JsonSerializer.Serialize(updatedContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kunde inte uppdatera GeneratedContentJson under UpdateContact");
                }
            }

            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Kontaktuppgifter uppdaterade för {CompanyName}", entity.CompanyName);
            return Ok(new { message = "Kontaktuppgifter uppdaterade." });
        }

        /// <summary>
        /// Uppdaterar textinnehåll direkt i den genererade HTML-filen utan att regenerera hemsidan.
        /// </summary>
        [HttpPatch("{id}/content")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateContent(
            int id,
            [FromBody] UpdateContentRequest req,
            CancellationToken ct)
        {
            var entity = await _db.CompanyWebsites.FindAsync(new object[] { id }, ct);
            if (entity is null) return NotFound();

            if (!HasWebsiteOwnership(entity)) return Forbid();

            var siteRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "Site", "generated",
                SanitizeName(entity.CompanyName)));
            var htmlPath = Path.Combine(siteRoot, "index.html");

            if (!System.IO.File.Exists(htmlPath))
                return NotFound(new ProblemDetails { Title = "HTML-filen hittades inte." });

            var html = await System.IO.File.ReadAllTextAsync(htmlPath, ct);
            string Enc(string s) => System.Net.WebUtility.HtmlEncode(s);

            // ── Hero ──────────────────────────────────────────────────────────
            if (req.HeroTitle is not null)
                html = ReplaceTagContent(html, "h1", Enc(req.HeroTitle));
            if (req.HeroText is not null)
                html = ReplaceClassContent(html, "hero-text", Enc(req.HeroText));
            if (req.CtaPrimary is not null)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"(<a href=""#kontakt"" class=""btn btn-primary"">)[^<]*(</a>)",
                    $"$1{Enc(req.CtaPrimary)}$2");
            if (req.CtaSecondary is not null)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"(<a href=""tel:[^""]*"" class=""btn btn-outline""><i[^>]*></i> )[^<]*(</a>)",
                    $"$1{Enc(req.CtaSecondary)}$2");

            // ── Trust band ────────────────────────────────────────────────────
            if (req.Trust1 is not null)
                html = ReplaceNthTrustItem(html, 1, Enc(req.Trust1));
            if (req.Trust2 is not null)
                html = ReplaceNthTrustItem(html, 2, Enc(req.Trust2));
            if (req.Trust3 is not null)
                html = ReplaceNthTrustItem(html, 3, Enc(req.Trust3));

            // ── Om oss ────────────────────────────────────────────────────────
            if (req.AboutSubtitle is not null)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"(<section id=""om-oss""[^>]*>[\s\S]*?<h[1-6][^>]*class=""[^""]*section-subtitle[^""]*""[^>]*>).*?(</h[1-6]>)",
                    $"$1{Enc(req.AboutSubtitle)}$2",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
            if (req.AboutTitle is not null)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"(<section id=""om-oss""[^>]*>[\s\S]*?<h[1-6][^>]*class=""[^""]*section-title[^""]*""[^>]*>).*?(</h[1-6]>)",
                    $"$1{Enc(req.AboutTitle)}$2",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
            if (req.AboutCta is not null)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"(<a href=""tel:[^""]*"" class=""btn btn-primary""><i[^>]*></i> )[^<]*(</a>)",
                    $"$1{Enc(req.AboutCta)}$2");
            if (req.OwnerName is not null)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"(<strong>)([^<]*?)( </strong>)",
                    m => m.Index < html.IndexOf("fas fa-user-circle") + 200
                        ? $"{m.Groups[1].Value}{Enc(req.OwnerName)}{m.Groups[3].Value}"
                        : m.Value);
            if (req.OwnerTitle is not null)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"(<div class=""dc-item"">[\s\S]*?fa-user-circle[\s\S]*?<span>)([^<]*?)(</span>)",
                    $"$1{Enc(req.OwnerTitle)}$3",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

            // Om oss-paragrafer — ersätt befintliga <p>-taggar i story-section
            var aboutParas = new[] { req.AboutParagraph1, req.AboutParagraph2, req.AboutParagraph3 }
                .Where(p => p is not null).Select(p => p!).ToList();
            if (aboutParas.Count > 0)
            {
                var newParaHtml = string.Join("", aboutParas.Select(p => $"<p>{Enc(p)}</p>"));
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"(class=""section-title"">[^<]*</h2>\s*)(<p>[\s\S]*?</p>)+(\s*<div class=""anders-contact"">)",
                    $"$1{newParaHtml}$3",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
            }

            // ── Tagline ───────────────────────────────────────────────────────
            if (req.Tagline is not null)
            {
                // R5: Ersätt hela title-taggen istället för att söka efter em-dash-ankare
                // (em-dash-mönstret fungerar inte sedan teckenrensningen)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"<title>[^<]*</title>",
                    $"<title>{Enc(entity.CompanyName)} - {Enc(req.Tagline)}</title>");
                html = ReplaceClassContent(html, "footer-tagline", Enc(req.Tagline));
            }

            // ── Tjänster ──────────────────────────────────────────────────────
            html = PatchServiceCard(html, 1, req.Service1Title, req.Service1Description, Enc);
            html = PatchServiceCard(html, 2, req.Service2Title, req.Service2Description, Enc);
            html = PatchServiceCard(html, 3, req.Service3Title, req.Service3Description, Enc);

            // ── FAQ ───────────────────────────────────────────────────────────
            html = PatchFaqCard(html, 1, req.Faq1Question, req.Faq1Answer, Enc);
            html = PatchFaqCard(html, 2, req.Faq2Question, req.Faq2Answer, Enc);
            html = PatchFaqCard(html, 3, req.Faq3Question, req.Faq3Answer, Enc);

            await System.IO.File.WriteAllTextAsync(htmlPath, html, ct);

            // Keep GeneratedContentJson in sync with content edits
            if (!string.IsNullOrEmpty(entity.GeneratedContentJson))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<WebsiteContentResponse>(entity.GeneratedContentJson);
                    if (content is not null)
                    {
                        var hero = content.Hero is null ? null : new WebsiteHeroContent(
                            Title: req.HeroTitle ?? content.Hero.Title,
                            Text: req.HeroText ?? content.Hero.Text,
                            BackgroundImageUrl: content.Hero.BackgroundImageUrl,
                            CtaPrimary: req.CtaPrimary ?? content.Hero.CtaPrimary,
                            CtaSecondary: req.CtaSecondary ?? content.Hero.CtaSecondary
                        );

                        var trustBand = content.TrustBand is null ? null : new WebsiteTrustBand(
                            Trust1: req.Trust1 ?? content.TrustBand.Trust1,
                            Trust2: req.Trust2 ?? content.TrustBand.Trust2,
                            Trust3: req.Trust3 ?? content.TrustBand.Trust3
                        );

                        // Om oss
                        var paragraphs = content.About?.Paragraphs?.ToList() ?? new List<string>();
                        if (req.AboutParagraph1 is not null)
                        {
                            if (paragraphs.Count > 0) paragraphs[0] = req.AboutParagraph1;
                            else paragraphs.Add(req.AboutParagraph1);
                        }
                        if (req.AboutParagraph2 is not null)
                        {
                            if (paragraphs.Count > 1) paragraphs[1] = req.AboutParagraph2;
                            else { while (paragraphs.Count < 1) paragraphs.Add(""); paragraphs.Add(req.AboutParagraph2); }
                        }
                        if (req.AboutParagraph3 is not null)
                        {
                            if (paragraphs.Count > 2) paragraphs[2] = req.AboutParagraph3;
                            else { while (paragraphs.Count < 2) paragraphs.Add(""); paragraphs.Add(req.AboutParagraph3); }
                        }

                        var about = content.About is null ? null : new WebsiteAboutContent(
                            Subtitle: req.AboutSubtitle ?? content.About.Subtitle,
                            Title: req.AboutTitle ?? content.About.Title,
                            Paragraphs: paragraphs,
                            ImageUrl: content.About.ImageUrl,
                            CtaText: req.AboutCta ?? content.About.CtaText,
                            OwnerName: req.OwnerName ?? content.About.OwnerName,
                            OwnerTitle: req.OwnerTitle ?? content.About.OwnerTitle
                        );

                        // Tjänster
                        var services = content.Services?.ToList() ?? new List<WebsiteServiceCard>();
                        if (services.Count > 0 && (req.Service1Title is not null || req.Service1Description is not null))
                        {
                            services[0] = new WebsiteServiceCard(
                                Title: req.Service1Title ?? services[0].Title,
                                Description: req.Service1Description ?? services[0].Description,
                                ImageUrl: services[0].ImageUrl
                            );
                        }
                        if (services.Count > 1 && (req.Service2Title is not null || req.Service2Description is not null))
                        {
                            services[1] = new WebsiteServiceCard(
                                Title: req.Service2Title ?? services[1].Title,
                                Description: req.Service2Description ?? services[1].Description,
                                ImageUrl: services[1].ImageUrl
                            );
                        }
                        if (services.Count > 2 && (req.Service3Title is not null || req.Service3Description is not null))
                        {
                            services[2] = new WebsiteServiceCard(
                                Title: req.Service3Title ?? services[2].Title,
                                Description: req.Service3Description ?? services[2].Description,
                                ImageUrl: services[2].ImageUrl
                            );
                        }

                        // FAQ
                        var faqs = content.Faqs?.ToList() ?? new List<WebsiteFaqCard>();
                        if (faqs.Count > 0 && (req.Faq1Question is not null || req.Faq1Answer is not null))
                        {
                            faqs[0] = new WebsiteFaqCard(
                                Icon: faqs[0].Icon,
                                Question: req.Faq1Question ?? faqs[0].Question,
                                Answer: req.Faq1Answer ?? faqs[0].Answer
                            );
                        }
                        if (faqs.Count > 1 && (req.Faq2Question is not null || req.Faq2Answer is not null))
                        {
                            faqs[1] = new WebsiteFaqCard(
                                Icon: faqs[1].Icon,
                                Question: req.Faq2Question ?? faqs[1].Question,
                                Answer: req.Faq2Answer ?? faqs[1].Answer
                            );
                        }
                        if (faqs.Count > 2 && (req.Faq3Question is not null || req.Faq3Answer is not null))
                        {
                            faqs[2] = new WebsiteFaqCard(
                                Icon: faqs[2].Icon,
                                Question: req.Faq3Question ?? faqs[2].Question,
                                Answer: req.Faq3Answer ?? faqs[2].Answer
                            );
                        }

                        var updatedContent = content with
                        {
                            Hero = hero!,
                            TrustBand = trustBand!,
                            About = about!,
                            Tagline = req.Tagline ?? content.Tagline,
                            Services = services,
                            Faqs = faqs
                        };

                        entity.GeneratedContentJson = JsonSerializer.Serialize(updatedContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kunde inte uppdatera GeneratedContentJson under UpdateContent");
                }
            }

            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Innehåll uppdaterat för {CompanyName}", entity.CompanyName);
            return Ok(new { message = "Innehåll uppdaterat." });
        }

        // ── Hjälpmetoder för HTML-manipulation ───────────────────────────────

        private static string ReplaceTagContent(string html, string tag, string newContent)
            => System.Text.RegularExpressions.Regex.Replace(
                html, $@"(<{tag}[^>]*>).*?(</\s*{tag}>)", $"$1{newContent}$2", System.Text.RegularExpressions.RegexOptions.Singleline);

        private static string ReplaceClassContent(string html, string cssClass, string newContent)
            => System.Text.RegularExpressions.Regex.Replace(
                html,
                $@"(<[a-zA-Z0-9]+[^>]*class=""[^""]*{System.Text.RegularExpressions.Regex.Escape(cssClass)}[^""]*""[^>]*>).*?(</[a-zA-Z0-9]+>)",
                $"$1{newContent}$2", System.Text.RegularExpressions.RegexOptions.Singleline);

        private static string ReplaceNthTrustItem(string html, int n, string newContent)
        {
            int count = 0;
            return System.Text.RegularExpressions.Regex.Replace(
                html,
                @"(<span>)(.*?)(</span>)",
                m =>
                {
                    // Only replace spans inside trust-band (heuristic: first 3 spans after trust-band)
                    count++;
                    return count == n ? $"{m.Groups[1].Value}{newContent}{m.Groups[3].Value}" : m.Value;
                },
                System.Text.RegularExpressions.RegexOptions.Singleline);
        }

        private static string PatchServiceCard(string html, int n, string? title, string? desc, Func<string, string> enc)
        {
            if (title is null && desc is null) return html;
            // Find nth service-card and replace its h3 and p
            int count = 0;
            return System.Text.RegularExpressions.Regex.Replace(
                html,
                @"(<article class='service-card[^']*'[^>]*>[\s\S]*?<div class='card-body'>[\s\S]*?<h3>)(.*?)(</h3>)([\s\S]*?<p>)(.*?)(</p>)([\s\S]*?</article>)",
                m =>
                {
                    count++;
                    if (count != n) return m.Value;
                    var h3 = title is not null ? enc(title) : m.Groups[2].Value;
                    var p  = desc  is not null ? enc(desc)  : m.Groups[5].Value;
                    return $"{m.Groups[1].Value}{h3}{m.Groups[3].Value}{m.Groups[4].Value}{p}{m.Groups[6].Value}{m.Groups[7].Value}";
                },
                System.Text.RegularExpressions.RegexOptions.Singleline);
        }

        private static string PatchFaqCard(string html, int n, string? question, string? answer, Func<string, string> enc)
        {
            if (question is null && answer is null) return html;
            int count = 0;
            return System.Text.RegularExpressions.Regex.Replace(
                html,
                @"(<div class='faq-card[^']*'[^>]*>[\s\S]*?<h3>.*?<\/i>\s*)(.*?)(</h3>[\s\S]*?<p>)(.*?)(</p>[\s\S]*?</div>)",
                m =>
                {
                    count++;
                    if (count != n) return m.Value;
                    var q = question is not null ? enc(question) : m.Groups[2].Value;
                    var a = answer   is not null ? enc(answer)   : m.Groups[4].Value;
                    return $"{m.Groups[1].Value}{q}{m.Groups[3].Value}{a}{m.Groups[5].Value}";
                },
                System.Text.RegularExpressions.RegexOptions.Singleline);
        }

        private bool HasWebsiteOwnership(CompanyWebsite entity)
        {
            var username = User.Identity?.Name;
            var isAdmin = User.IsInRole("Admin");
            return isAdmin || (username is not null && entity.CreatedBy == username);
        }

        /// <summary>
        /// Laddar ner alla genererade hemsidefiler som en ZIP-fil.
        /// </summary>
        /// <param name="id">ID för hemsidan</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>ZIP-fil som nedladdning</returns>
        [HttpGet("{id}/download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Download(int id, CancellationToken ct)
        {
            var entity = await _db.CompanyWebsites.FindAsync(new object[] { id }, ct);
            if (entity is null) return NotFound();

            if (!HasWebsiteOwnership(entity)) return Forbid();

            var sanitizedName = SanitizeName(entity.CompanyName);
            var siteRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "Site", "generated", sanitizedName));

            if (!Directory.Exists(siteRoot))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Filer saknas",
                    Detail = "Hemsidans filer kunde inte hittas på disken.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var files = Directory.GetFiles(siteRoot, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(siteRoot, file);
                    archive.CreateEntryFromFile(file, relativePath);
                }
            }

            memoryStream.Position = 0;
            return File(memoryStream, "application/zip", $"{sanitizedName}.zip");
        }

    }
}
