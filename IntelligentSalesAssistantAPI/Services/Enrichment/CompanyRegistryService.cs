using System.Net.Http.Json;
using IntelligentSalesAssistantAPI.DTOs;
using Microsoft.Extensions.Caching.Hybrid;

namespace IntelligentSalesAssistantAPI.Services.Enrichment
{
    // Hämtar företagsinformation från externt register
    public interface ICompanyRegistryService
    {
        Task<CompanyRegistryResult?> GetCompanyByOrgNumberAsync(string orgNumber, CancellationToken cancellationToken = default);
    }

    public record CompanyRegistryResult(
        string OrgNumber,
        string CompanyName,
        string Address,
        string City,
        string PostCode,
        string? Website = null,
        string? ContactPerson = null,
        string? Phone = null,
        string? Email = null,
        string Industry = "Övriga",
        double? GoogleRating = null  // Från Google Places
    );

    // Implementation för hämtning av företagsinformation
    public class CompanyRegistryService : ICompanyRegistryService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CompanyRegistryService> _logger;
        private readonly HybridCache _cache;

        public CompanyRegistryService(
            IHttpClientFactory httpClientFactory,
            ILogger<CompanyRegistryService> logger,
            HybridCache cache)
        {
            _httpClient = httpClientFactory.CreateClient("CompanyApiClient");
            _logger = logger;
            _cache = cache;
        }

        public async Task<CompanyRegistryResult?> GetCompanyByOrgNumberAsync(string orgNumber, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"company:{orgNumber}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async ct => await FetchCompanyAsync(orgNumber, ct),
                new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromHours(24),
                    LocalCacheExpiration = TimeSpan.FromHours(24)
                },
                cancellationToken: cancellationToken);
        }

        private async Task<CompanyRegistryResult?> FetchCompanyAsync(string orgNumber, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync($"v1/company/{orgNumber}", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Kunde inte hämta data från Bolagsapi.se för orgnr {OrgNumber}. Status: {StatusCode}", orgNumber, response.StatusCode);
                    return null;
                }

                var apiData = await response.Content.ReadFromJsonAsync<BolagsApiResponse>(cancellationToken: cancellationToken);

                if (apiData == null)
                {
                    _logger.LogWarning("Inget data returnerades från Bolagsapi.se för orgnr {OrgNumber}.", orgNumber);
                    return null;
                }

                return new CompanyRegistryResult(
                    OrgNumber: orgNumber,
                    CompanyName: apiData.ResolvedName ?? "Okänt",
                    Address: apiData.Adress ?? string.Empty,
                    City: apiData.Ort ?? string.Empty,
                    PostCode: apiData.Postnummer ?? string.Empty,
                    Website: apiData.ResolvedWebsite,
                    ContactPerson: null,
                    Phone: null,
                    Email: null,
                    Industry: string.IsNullOrWhiteSpace(apiData.ResolvedIndustry) ? "Okänd bransch" : apiData.ResolvedIndustry!
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ett fel uppstod vid anrop till Bolagsapi.se för orgnr {OrgNumber}", orgNumber);
                return null;
            }
        }
    }
}
