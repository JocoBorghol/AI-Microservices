using IntelligentSalesAssistantAPI.Services.Enrichment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace IntelligentSalesAssistantAPI.Controllers
{
    /// <summary>
    /// API för företagsforskning - hämtar data från BolagsAPI
    /// </summary>
    [ApiController]
    [Route("api/research")]
    [Authorize]
    public class CompanyResearchController : ControllerBase
    {
        private readonly ICompanyResearchService _researchService;
        private readonly ILogger<CompanyResearchController> _logger;
        
        // In-memory cache för senaste sökningen per användare
        private static readonly ConcurrentDictionary<string, EnrichmentPreviewResult> _latestSearchCache = new();

        public CompanyResearchController(
            ICompanyResearchService researchService,
            ILogger<CompanyResearchController> logger)
        {
            _researchService = researchService;
            _logger = logger;
        }

        /// <summary>
        /// Statisk metod för att hämta latest search från andra controllers
        /// </summary>
        public static bool TryGetLatestSearch(string userId, out EnrichmentPreviewResult? result)
        {
            return _latestSearchCache.TryGetValue(userId, out result);
        }

        /// <summary>
        /// Hämtar enrichment data från BolagsAPI OCH sparar som "latest search"
        /// </summary>
        /// <param name="request">Request med organisationsnummer</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Enrichment data från BolagsAPI</returns>
        /// <response code="200">Returnerar enrichment data och sparar som "latest"</response>
        /// <response code="400">Om organisationsnumret är ogiltigt</response>
        /// <response code="401">Om användaren inte är autentiserad</response>
        /// <response code="404">Om företaget inte hittas</response>
        [HttpPost]
        [ProducesResponseType(typeof(EnrichmentPreviewResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EnrichmentPreviewResult>> GetAndSaveEnrichment(
            [FromBody] EnrichmentRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation("Hämtar och sparar enrichment för orgnr {OrgNumber}", request.OrgNumber);

            try
            {
                var result = await _researchService.GetEnrichmentPreviewAsync(request.OrgNumber, ct);
                
                // Spara i cache med användarens ID som nyckel
                var userId = User.Identity?.Name ?? "anonymous";
                _latestSearchCache[userId] = result;
                
                _logger.LogInformation("Enrichment data hämtad och sparad för användare {UserId}", userId);
                
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Företag med orgnr {OrgNumber} hittades inte", request.OrgNumber);
                return NotFound(new ProblemDetails
                {
                    Title = "Företag hittades inte",
                    Detail = ex.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }
        }

        /// <summary>
        /// Hämtar senaste sparade enrichment search
        /// </summary>
        /// <returns>Senaste enrichment data eller 404 om ingen finns</returns>
        /// <response code="200">Returnerar senaste sökningen</response>
        /// <response code="404">Om ingen senaste sökning finns</response>
        [HttpGet("cache")]
        [ProducesResponseType(typeof(EnrichmentPreviewResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public ActionResult<EnrichmentPreviewResult> GetLatestSearch()
        {
            var userId = User.Identity?.Name ?? "anonymous";
            
            if (_latestSearchCache.TryGetValue(userId, out var result))
            {
                _logger.LogInformation("Returnerar latest search för användare {UserId}", userId);
                return Ok(result);
            }
            
            _logger.LogWarning("Ingen latest search hittades för användare {UserId}", userId);
            return NotFound(new ProblemDetails
            {
                Title = "Ingen senaste sökning",
                Detail = "Du har inte sparat någon enrichment search ännu",
                Status = StatusCodes.Status404NotFound
            });
        }

        /// <summary>
        /// Raderar senaste sparade enrichment search
        /// </summary>
        /// <returns>Bekräftelse att sökningen raderades</returns>
        /// <response code="200">Sökningen raderades</response>
        /// <response code="404">Om ingen senaste sökning finns</response>
        [HttpDelete("cache")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public ActionResult DeleteLatestSearch()
        {
            var userId = User.Identity?.Name ?? "anonymous";
            
            if (_latestSearchCache.TryRemove(userId, out _))
            {
                _logger.LogInformation("Latest search raderad för användare {UserId}", userId);
                return Ok(new { message = "Latest search raderad" });
            }
            
            _logger.LogWarning("Ingen latest search att radera för användare {UserId}", userId);
            return NotFound(new ProblemDetails
            {
                Title = "Ingen senaste sökning",
                Detail = "Du har inte sparat någon enrichment search ännu",
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// Request för enrichment endpoint
    /// </summary>
    public record EnrichmentRequest(
        string OrgNumber
    );
}
