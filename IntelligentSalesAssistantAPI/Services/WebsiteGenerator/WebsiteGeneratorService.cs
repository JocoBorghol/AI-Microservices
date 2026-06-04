using System.Text.Json;
using IntelligentSalesAssistantAPI.Data;
using IntelligentSalesAssistantAPI.DTOs;
using IntelligentSalesAssistantAPI.Exceptions;
using IntelligentSalesAssistantAPI.Http.Clients;
using IntelligentSalesAssistantAPI.Models;
using IntelligentSalesAssistantAPI.Services.Enrichment;
using Microsoft.EntityFrameworkCore;

namespace IntelligentSalesAssistantAPI.Services.WebsiteGenerator
{
    /// <summary>
    /// Orkestrerar generering och hantering av företagshemsidor
    /// </summary>
    public class WebsiteGeneratorService : IWebsiteGeneratorService
    {
        private readonly ICompanyResearchService _researchService;
        private readonly LlmProxyClient _llmProxyClient;
        private readonly ITemplateService _templateService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WebsiteGeneratorService> _logger;

        private static readonly System.Text.RegularExpressions.Regex OrgNumberRegex =
            new(@"^\d{6}-?\d{4}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public WebsiteGeneratorService(
            ICompanyResearchService researchService,
            LlmProxyClient llmProxyClient,
            ITemplateService templateService,
            ApplicationDbContext context,
            ILogger<WebsiteGeneratorService> logger)
        {
            _researchService = researchService;
            _llmProxyClient = llmProxyClient;
            _templateService = templateService;
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<WebsiteResponse> GenerateWebsiteAsync(
            string orgNumber,
            WebsiteCustomization? customization = null,
            CompanyEnrichmentData? enrichmentData = null,
            string? createdBy = null,
            CancellationToken ct = default)
        {
            // Steg 1: Validera organisationsnummer
            if (!OrgNumberRegex.IsMatch(orgNumber))
                throw new ValidationException($"Ogiltigt organisationsnummer: '{orgNumber}'. Förväntat format: XXXXXX-XXXX");

            _logger.LogInformation("Genererar hemsida för orgnr {OrgNumber}", orgNumber);

            // Steg 2: Hämta företagsdata
            // Om enrichmentData finns, använd den som extra context
            // Annars, forska om företaget (aggregerar data från BolagsApi)
            CompanyResearchResult company;
            
            if (enrichmentData != null)
            {
                _logger.LogInformation(
                    "Använder medskickad enrichment data för {CompanyName}",
                    enrichmentData.Bolag.CompanyName);
                
                // Konvertera enrichment data till CompanyResearchResult format
                company = new CompanyResearchResult(
                    OrgNumber: enrichmentData.Bolag.OrgNumber,
                    CompanyName: enrichmentData.Bolag.CompanyName,
                    Address: enrichmentData.Bolag.Address,
                    City: enrichmentData.Bolag.City,
                    PostCode: enrichmentData.Bolag.PostCode,
                    Industry: enrichmentData.Bolag.Industry,
                    Website: enrichmentData.Bolag.Website,
                    ContactPerson: enrichmentData.Bolag.ContactPerson,
                    Phone: enrichmentData.Bolag.Phone,
                    Email: enrichmentData.Bolag.Email,
                    GoogleRating: null, // Kan hämtas från place_id senare
                    LinkedInUrl: null,
                    TwitterUrl: null,
                    FacebookUrl: null,
                    EmployeeCount: null,
                    Sources: new ResearchSources(
                        BolagsApi: true,
                        GooglePlaces: enrichmentData.Google?.Candidates?.Any() == true
                    )
                );
            }
            else
            {
                company = await _researchService.ResearchCompanyAsync(orgNumber, ct);
                
                _logger.LogInformation(
                    "Företagsdata hämtad för {CompanyName}. Källor: BolagsApi={Bolag}",
                    company.CompanyName, company.Sources.BolagsApi);
            }

            // Steg 3: Bygg request till Service B med ALL forskningsdata
            var request = new GenerateWebsiteContentRequest(
                CompanyName: company.CompanyName,
                Industry: company.Industry ?? "Okänd bransch",
                City: company.City ?? "Okänd stad",
                Ceo: company.ContactPerson,
                Employees: company.EmployeeCount,
                Founded: null,
                Phone: company.Phone,
                Email: company.Email,
                Website: company.Website,
                Address: company.Address,
                Tone: customization?.Tone,
                TargetAudience: customization?.TargetAudience,
                TopServices: customization?.TopServices,
                Keywords: customization?.Keywords,
                ClientId: "service-a"
            );

            // Steg 4: Anropa Service B
            var content = await _llmProxyClient.GenerateWebsiteContentAsync(request, ct);

            // Steg 5: Sanitera företagsnamn INNAN rendering
            var sanitizedName = _templateService.SanitizeCompanyName(content.CompanyName);

            // Steg 6: Läs och rendera mall med sanitizedName
            var template = await _templateService.LoadTemplateAsync(ct);
            var html = await _templateService.RenderTemplateAsync(template, content, sanitizedName, ct);

            // Steg 7: Spara filer
            await _templateService.SaveWebsiteAsync(content.CompanyName, html, clearImages: true, ct);
            // Steg 8: Spara metadata i databas
            var website = new CompanyWebsite
            {
                OrgNumber = orgNumber,
                CompanyName = content.CompanyName,
                FilePath = $"Site/generated/{sanitizedName}/index.html",
                Category = company.Industry ?? "Okänd bransch",
                Tone = customization?.Tone,
                TargetAudience = customization?.TargetAudience,
                TopServicesJson = customization?.TopServices is not null
                    ? JsonSerializer.Serialize(customization.TopServices)
                    : null,
                KeywordsJson = customization?.Keywords is not null
                    ? JsonSerializer.Serialize(customization.Keywords)
                    : null,
                CreatedBy = createdBy ?? "anonymous",
                GeneratedContentJson = JsonSerializer.Serialize(content),
                CreatedAt = DateTime.UtcNow
            };

            _context.CompanyWebsites.Add(website);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Hemsida genererad för {CompanyName} (id={Id})", content.CompanyName, website.Id);

            return MapToResponse(website, sanitizedName);
        }

        /// <inheritdoc/>
        public async Task<WebsiteResponse> RegenerateWebsiteAsync(
            int websiteId,
            WebsiteCustomization? customization = null,
            CancellationToken ct = default)
        {
            var website = await _context.CompanyWebsites.FindAsync(new object[] { websiteId }, ct)
                ?? throw new NotFoundException("Hemsida", websiteId.ToString());

            _logger.LogInformation("Regenererar hemsida id={Id} för {CompanyName}", websiteId, website.CompanyName);

            // Radera gamla filer
            await _templateService.DeleteWebsiteAsync(website.CompanyName, ct);

            // Forska om företaget (aggregerar data från BolagsApi)
            var company = await _researchService.ResearchCompanyAsync(website.OrgNumber, ct);

            _logger.LogInformation(
                "Företagsdata hämtad för {CompanyName}. Källor: BolagsApi={Bolag}",
                company.CompanyName, company.Sources.BolagsApi);

            // Bygg request med ALL forskningsdata
            var request = new GenerateWebsiteContentRequest(
                CompanyName: company.CompanyName,
                Industry: company.Industry ?? "Okänd bransch",
                City: company.City ?? "Okänd stad",
                Ceo: company.ContactPerson,
                Employees: company.EmployeeCount,
                Founded: null,
                Phone: company.Phone,
                Email: company.Email,
                Website: company.Website,
                Address: company.Address,
                Tone: customization?.Tone ?? website.Tone,
                TargetAudience: customization?.TargetAudience ?? website.TargetAudience,
                TopServices: customization?.TopServices,
                Keywords: customization?.Keywords,
                ClientId: "service-a"
            );

            // Generera nytt innehåll
            var content = await _llmProxyClient.GenerateWebsiteContentAsync(request, ct);
            
            // Sanitera företagsnamn INNAN rendering
            var sanitizedName = _templateService.SanitizeCompanyName(content.CompanyName);
            
            var template = await _templateService.LoadTemplateAsync(ct);
            var html = await _templateService.RenderTemplateAsync(template, content, sanitizedName, ct);
            await _templateService.SaveWebsiteAsync(content.CompanyName, html, ct);

            // Uppdatera entitet
            website.CompanyName = content.CompanyName;
            website.FilePath = $"Site/generated/{sanitizedName}/index.html";
            website.Category = company.Industry ?? "Okänd bransch";
            website.Tone = customization?.Tone ?? website.Tone;
            website.TargetAudience = customization?.TargetAudience ?? website.TargetAudience;
            website.TopServicesJson = customization?.TopServices is not null
                ? JsonSerializer.Serialize(customization.TopServices)
                : website.TopServicesJson;
            website.KeywordsJson = customization?.Keywords is not null
                ? JsonSerializer.Serialize(customization.Keywords)
                : website.KeywordsJson;
            website.GeneratedContentJson = JsonSerializer.Serialize(content);
            website.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Hemsida regenererad för {CompanyName} (id={Id})", content.CompanyName, website.Id);

            return MapToResponse(website, sanitizedName);
        }

        private static WebsiteResponse MapToResponse(CompanyWebsite website, string sanitizedName) =>
            new(
                Id: website.Id,
                CompanyName: website.CompanyName,
                OrgNumber: website.OrgNumber,
                WebsiteUrl: $"/generated/{sanitizedName}/index.html",
                Category: website.Category,
                Tone: website.Tone,
                TargetAudience: website.TargetAudience,
                CreatedAt: website.CreatedAt,
                UpdatedAt: website.UpdatedAt,
                Theme: website.Theme,
                GeneratedContentJson: website.GeneratedContentJson
            );
    }
}
