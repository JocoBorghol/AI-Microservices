using IntelligentSalesAssistantAPI.DTOs;
using IntelligentSalesAssistantAPI.Services.ContentDraft;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentSalesAssistantAPI.Controllers
{
    /// <summary>
    /// Hanterar skapande och hantering av AI-genererade innehållsutkast
    /// </summary>
    [ApiController]
    [Route("api/content/drafts")]
    [Authorize]
    public class ContentDraftController : ControllerBase
    {
        private readonly IContentDraftService _draftService;
        private readonly ILogger<ContentDraftController> _logger;

        public ContentDraftController(
            IContentDraftService draftService,
            ILogger<ContentDraftController> logger)
        {
            _draftService = draftService;
            _logger = logger;
        }

        /// <summary>
        /// Skapar ett nytt innehållsutkast med AI
        /// </summary>
        /// <param name="request">Specifikation för innehållet som ska genereras</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Det genererade innehållet och sökväg till sparad fil</returns>
        /// <response code="201">Utkastet skapades framgångsrikt</response>
        /// <response code="400">Ogiltiga parametrar</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        /// <response code="500">Fel från AI-tjänsten</response>
        /// <remarks>
        /// Skapa olika typer av innehåll med AI-hjälp. Exempel:
        /// 
        /// Exempel 1 - Facebook-inlägg med specifik hemsida (via ID):
        /// POST /api/content/drafts
        /// {
        ///   "contentType": "facebook_post",
        ///   "instructions": "Skapa ett roligt inlägg om våra sommaröppettider",
        ///   "purpose": "Info",
        ///   "targetAudience": "gäster",
        ///   "tone": "lättsam",
        ///   "length": "kort",
        ///   "websiteId": 11
        /// }
        /// 
        /// Exempel 2 - E-postmeddelande med senaste hemsidan:
        /// POST /api/content/drafts
        /// {
        ///   "contentType": "email",
        ///   "instructions": "Skriv ett meddelande till personalen om nya rutiner",
        ///   "targetAudience": "personal",
        ///   "tone": "professionell",
        ///   "useLatestWebsite": true
        /// }
        /// 
        /// Exempel 3 - Blogginlägg utan företagskontext:
        /// POST /api/content/drafts
        /// {
        ///   "contentType": "blog_post",
        ///   "instructions": "Skriv om vikten av regelbunden bilservice",
        ///   "purpose": "Marknadsföring",
        ///   "length": "lång"
        /// }
        /// 
        /// Content Types: facebook_post, instagram_post, email, blog_post, announcement, newsletter
        /// Purpose: Info, Kul, Marknadsföring, Internt
        /// Tone: professionell, lättsam, formell, vänlig
        /// Length: kort, medel, lång
        /// 
        /// Prioritet för företagskontext:
        /// 1. websiteId (om angivet) - använd specifik hemsida
        /// 2. useLatestWebsite (om true) - använd senaste hemsidan
        /// 3. Ingen kontext - generera generellt innehåll
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(ContentDraftResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ContentDraftResponse>> CreateDraft(
            [FromBody] CreateContentDraftRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation("Skapar innehållsutkast av typ {ContentType}", request.ContentType);

            var result = await _draftService.CreateDraftAsync(request, ct);

            return CreatedAtAction(
                nameof(GetDraftContent), 
                new { id = result.Id }, 
                result);
        }

        /// <summary>
        /// Hämtar alla sparade innehållsutkast
        /// </summary>
        /// <param name="companyName">Filtrera på företagsnamn (valfritt)</param>
        /// <returns>Lista med sparade utkast</returns>
        /// <response code="200">Returnerar listan med utkast</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        [HttpGet]
        [ProducesResponseType(typeof(ContentDraftListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ContentDraftListResponse>> GetDrafts(
            [FromQuery] string? companyName = null)
        {
            var result = await _draftService.GetDraftsAsync(companyName);
            return Ok(result);
        }

        /// <summary>
        /// Hämtar innehållet i ett specifikt utkast
        /// </summary>
        /// <param name="id">ID för utkastet (t.ex. 1, 2, 3)</param>
        /// <returns>Innehållet i utkastet</returns>
        /// <response code="200">Returnerar innehållet</response>
        /// <response code="404">Om utkastet inte finns</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        /// <remarks>
        /// Exempel: GET /api/content/drafts/5
        /// </remarks>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<string>> GetDraftContent(int id)
        {
            try
            {
                var content = await _draftService.GetDraftContentAsync(id);
                return Ok(content);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Utkast hittades inte",
                    Detail = ex.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }
        }

        /// <summary>
        /// Raderar ett specifikt utkast
        /// </summary>
        /// <param name="id">ID för utkastet</param>
        /// <returns>Ingen innehåll (204 No Content)</returns>
        /// <response code="204">Utkastet raderades framgångsrikt</response>
        /// <response code="404">Om utkastet inte finns</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        /// <remarks>
        /// Exempel: DELETE /api/content/drafts/5
        /// </remarks>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteDraft(int id)
        {
            try
            {
                await _draftService.DeleteDraftAsync(id);
                return NoContent();
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Utkast hittades inte",
                    Detail = ex.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }
        }

        /// <summary>
        /// Uppdaterar ett specifikt utkast manuellt (Non-Destructive)
        /// </summary>
        /// <param name="id">ID för utkastet</param>
        /// <param name="request">Det nya innehållet</param>
        /// <returns>Det uppdaterade utkastet med den nya sökvägen</returns>
        /// <response code="200">Utkastet uppdaterades framgångsrikt</response>
        /// <response code="400">Ogiltiga parametrar</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        /// <response code="404">Om utkastet inte finns</response>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ContentDraftResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ContentDraftResponse>> UpdateDraft(
            int id,
            [FromBody] UpdateContentDraftRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Felaktig begäran",
                    Detail = "Innehållet kan inte vara tomt",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            try
            {
                var result = await _draftService.UpdateDraftAsync(id, request.Content);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Utkast hittades inte",
                    Detail = ex.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }
        }

        /// <summary>
        /// Återställer ett utkast till sitt ursprungliga genererade skick
        /// </summary>
        /// <param name="id">ID för utkastet</param>
        /// <returns>Det återställda utkastet</returns>
        /// <response code="200">Utkastet återställdes framgångsrikt</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        /// <response code="404">Om utkastet inte finns</response>
        [HttpPost("{id:int}/restore")]
        [ProducesResponseType(typeof(ContentDraftResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ContentDraftResponse>> RestoreDraft(int id)
        {
            try
            {
                var result = await _draftService.RestoreDraftAsync(id);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Utkast hittades inte",
                    Detail = ex.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }
        }
    }
}
