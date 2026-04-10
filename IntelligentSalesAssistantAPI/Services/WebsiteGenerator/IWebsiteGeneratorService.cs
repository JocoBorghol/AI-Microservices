using IntelligentSalesAssistantAPI.DTOs;

namespace IntelligentSalesAssistantAPI.Services.WebsiteGenerator
{
    /// <summary>
    /// Service för generering och hantering av företagshemsidor
    /// </summary>
    public interface IWebsiteGeneratorService
    {
        /// <summary>
        /// Genererar en ny hemsida baserat på organisationsnummer
        /// </summary>
        /// <param name="orgNumber">Organisationsnummer</param>
        /// <param name="customization">Valfria anpassningar</param>
        /// <param name="enrichmentData">Valfri enrichment data från /enrichment-preview (används som extra context)</param>
        /// <param name="ct">Cancellation token</param>
        Task<WebsiteResponse> GenerateWebsiteAsync(
            string orgNumber, 
            WebsiteCustomization? customization = null, 
            CompanyEnrichmentData? enrichmentData = null,
            CancellationToken ct = default);

        /// <summary>
        /// Regenererar en befintlig hemsida
        /// </summary>
        Task<WebsiteResponse> RegenerateWebsiteAsync(int websiteId, WebsiteCustomization? customization = null, CancellationToken ct = default);
    }
}
