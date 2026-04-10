using IntelligentSalesAssistantAPI.Http.Clients;

namespace IntelligentSalesAssistantAPI.Services.Enrichment
{
    /// <summary>
    /// Aggregerar företagsinformation från BolagsAPI (Bolagsverket)
    /// </summary>
    public interface ICompanyResearchService
    {
        Task<CompanyResearchResult> ResearchCompanyAsync(string orgNumber, CancellationToken ct = default);
        Task<EnrichmentPreviewResult> GetEnrichmentPreviewAsync(string orgNumber, CancellationToken ct = default);
    }

    /// <summary>
    /// Resultat från företagsforskning - innehåller ENDAST data som faktiskt hittades (inga null-värden)
    /// </summary>
    public record CompanyResearchResult(
        string OrgNumber,
        string CompanyName,
        string? Address,
        string? City,
        string? PostCode,
        string? Industry,
        string? Website,
        string? ContactPerson,
        string? Phone,
        string? Email,
        double? GoogleRating,
        string? LinkedInUrl,
        string? TwitterUrl,
        string? FacebookUrl,
        int? EmployeeCount,
        ResearchSources Sources
    );

    /// <summary>
    /// Visar vilka källor som bidrog med data
    /// </summary>
    public record ResearchSources(
        bool BolagsApi,
        bool GooglePlaces
    );

    /// <summary>
    /// Enrichment preview - rådata från BolagsAPI (för debugging och inspektion)
    /// </summary>
    public record EnrichmentPreviewResult(
        BolagData Bolag
    );

    public record BolagData(
        /// <example>5565093902</example>
        string OrgNumber,
        
        /// <example>Kandy'z AB</example>
        string CompanyName,
        
        /// <example>Storgatan 12</example>
        string? Address,
        
        /// <example>Stockholm</example>
        string? City,
        
        /// <example>11122</example>
        string? PostCode,
        
        /// <example>https://kandyz.se</example>
        string? Website,
        
        /// <example>Anna Andersson</example>
        string? ContactPerson,
        
        /// <example>08-123 45 67</example>
        string? Phone,
        
        /// <example>info@kandyz.se</example>
        string? Email,
        
        /// <example>Detaljhandel med godis</example>
        string? Industry
    );

    public class CompanyResearchService : ICompanyResearchService
    {
        private readonly ICompanyRegistryService _companyRegistry;
        private readonly ILogger<CompanyResearchService> _logger;

        public CompanyResearchService(
            ICompanyRegistryService companyRegistry,
            ILogger<CompanyResearchService> logger)
        {
            _companyRegistry = companyRegistry;
            _logger = logger;
        }

        public async Task<CompanyResearchResult> ResearchCompanyAsync(string orgNumber, CancellationToken ct = default)
        {
            _logger.LogInformation("Startar företagsforskning för orgnr {OrgNumber}", orgNumber);

            // Steg 1: Hämta grunddata från BolagsApi (obligatorisk)
            var companyData = await _companyRegistry.GetCompanyByOrgNumberAsync(orgNumber, ct);
            if (companyData == null)
            {
                throw new InvalidOperationException($"Kunde inte hitta företag med orgnr {orgNumber}");
            }

            _logger.LogInformation(
                "BolagsApi data: Namn={Name}, Stad={City}, Website={Website}",
                companyData.CompanyName, companyData.City, companyData.Website ?? "saknas");

            var sources = new ResearchSources(
                BolagsApi: true,
                GooglePlaces: false
            );

            string? email = null;
            string? phone = companyData.Phone;
            string? website = companyData.Website;
            double? googleRating = null;
            string? linkedInUrl = null;
            string? twitterUrl = null;
            string? facebookUrl = null;
            int? employeeCount = null;

            _logger.LogInformation(
                "Företagsforskning klar för {OrgNumber}. Källor: BolagsApi={Bolag}, GooglePlaces={Google}",
                orgNumber, sources.BolagsApi, sources.GooglePlaces);

            return new CompanyResearchResult(
                OrgNumber: orgNumber,
                CompanyName: companyData.CompanyName,
                Address: companyData.Address,
                City: companyData.City,
                PostCode: companyData.PostCode,
                Industry: companyData.Industry,
                Website: website,
                ContactPerson: companyData.ContactPerson,
                Phone: phone,
                Email: email,
                GoogleRating: googleRating,
                LinkedInUrl: linkedInUrl,
                TwitterUrl: twitterUrl,
                FacebookUrl: facebookUrl,
                EmployeeCount: employeeCount,
                Sources: sources
            );
        }

        /// <summary>
        /// Extraherar domännamn från en URL
        /// </summary>
        private string? ExtractDomain(string url)
        {
            try
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Hämtar enrichment preview med rådata från BolagsAPI
        /// </summary>
        public async Task<EnrichmentPreviewResult> GetEnrichmentPreviewAsync(string orgNumber, CancellationToken ct = default)
        {
            _logger.LogInformation("Startar enrichment preview för orgnr {OrgNumber}", orgNumber);

            // Hämta grunddata från BolagsApi
            var companyData = await _companyRegistry.GetCompanyByOrgNumberAsync(orgNumber, ct);
            if (companyData == null)
            {
                throw new InvalidOperationException($"Kunde inte hitta företag med orgnr {orgNumber}");
            }

            var bolagData = new BolagData(
                OrgNumber: orgNumber,
                CompanyName: companyData.CompanyName,
                Address: companyData.Address,
                City: companyData.City,
                PostCode: companyData.PostCode,
                Website: companyData.Website,
                ContactPerson: companyData.ContactPerson,
                Phone: companyData.Phone,
                Email: companyData.Email,
                Industry: companyData.Industry
            );

            _logger.LogInformation("Enrichment preview klar för {OrgNumber}", orgNumber);

            return new EnrichmentPreviewResult(
                Bolag: bolagData
            );
        }
    }
}
