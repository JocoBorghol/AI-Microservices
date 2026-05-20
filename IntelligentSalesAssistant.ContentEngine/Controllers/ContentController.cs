using ISA.ContentEngine.ApiClients;
using ISA.ContentEngine.DTOs;
using ISA.ContentEngine.Security;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace ISA.ContentEngine.Controllers
{
    [ApiController]
    [Route("api/content")]
    [RequireApiKey] // Säkerhetskrav för service-to-service kommunikation
    public class ContentController : ControllerBase
    {
        private readonly IGeminiClient _geminiClient;
        private readonly ILogger<ContentController> _logger;

        public ContentController(
            IGeminiClient geminiClient,
            ILogger<ContentController> logger)
        {
            _geminiClient = geminiClient;
            _logger = logger;
        }

        /// <summary>
        /// Genererar AI-innehåll via Google Gemini API baserat på en textprompt.
        /// </summary>
        [HttpPost("generate")]
        [ProducesResponseType(typeof(ContentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ContentResponse>> GenerateContent([FromBody] ContentRequest request, CancellationToken ct)
        {
            _logger.LogDebug("Mottog prompt från {ClientId}: {Prompt}", request.ClientId, request.Prompt);

            var reply = await _geminiClient.GenerateContentAsync(request.Prompt, ct);
            var response = new ContentResponse(reply);

            return Ok(response);
        }

        /// <summary>
        /// Genererar komplett hemsideinnehåll baserat på företagsdata
        /// </summary>
        [HttpPost("websites")]
        [ProducesResponseType(typeof(WebsiteContentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<WebsiteContentResponse>> GenerateWebsiteContent(
            [FromBody] GenerateWebsiteContentRequest request,
            CancellationToken ct)
        {
            _logger.LogDebug(
                "Genererar hemsideinnehåll för {CompanyName} från {ClientId}",
                request.CompanyName,
                request.ClientId);

            var prompt = BuildPrompt(request);
            var aiText = await _geminiClient.GenerateContentAsync(prompt, ct);
            var content = ParseAiResponse(aiText, request);

            _logger.LogDebug(
                "Hemsideinnehåll genererat för {CompanyName} med bilder från Gemini",
                request.CompanyName);

            return Ok(content);
        }

        /// <summary>
        /// Bygger en rik prompt för Gemini baserad på företagsdata och anpassningar
        /// </summary>
        private string BuildPrompt(GenerateWebsiteContentRequest request)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Du är en expert på att skriva hemsideinnehåll för svenska företag.");
            sb.AppendLine();
            sb.AppendLine("FÖRETAGSINFORMATION (från officiella register):");
            sb.AppendLine($"- Namn: {request.CompanyName}");
            sb.AppendLine($"- Bransch: {request.Industry}");
            sb.AppendLine($"- Stad: {request.City}");

            if (!string.IsNullOrEmpty(request.Address))
                sb.AppendLine($"- Adress: {request.Address}");

            if (!string.IsNullOrEmpty(request.Ceo))
                sb.AppendLine($"- VD/Kontaktperson: {request.Ceo}");

            if (request.Employees.HasValue)
                sb.AppendLine($"- Anställda: {request.Employees}");

            if (!string.IsNullOrEmpty(request.Founded))
                sb.AppendLine($"- Grundat: {request.Founded}");

            if (!string.IsNullOrEmpty(request.Website))
                sb.AppendLine($"- Webbplats: {request.Website}");

            if (!string.IsNullOrEmpty(request.Phone))
                sb.AppendLine($"- Telefon: {request.Phone}");

            if (!string.IsNullOrEmpty(request.Email))
                sb.AppendLine($"- E-post: {request.Email}");

            sb.AppendLine();
            sb.AppendLine("ANPASSNINGAR FRÅN ANVÄNDAREN:");
            sb.AppendLine($"- Ton: {request.Tone ?? "professionell"}");
            sb.AppendLine($"- Målgrupp: {request.TargetAudience ?? "både privatpersoner och företag"}");

            if (request.TopServices?.Count > 0)
                sb.AppendLine($"- Fokustjänster: {string.Join(", ", request.TopServices)}");

            if (request.Keywords?.Count > 0)
                sb.AppendLine($"- Nyckelord att inkludera: {string.Join(", ", request.Keywords)}");

            if (!string.IsNullOrEmpty(request.OwnerQuote))
                sb.AppendLine($"- Personligt citat från ägaren: \"{request.OwnerQuote}\"");

            sb.AppendLine();
            sb.AppendLine("UPPGIFT:");
            sb.AppendLine("Generera komplett hemsideinnehåll i JSON-format med duplikationsfria fält enligt instruktioner.");
            sb.AppendLine("- Returnera ENDAST JSON, ingen annan text");

            return sb.ToString();
        }

        /// <summary>
        /// Parsar AI-svar från Gemini till strukturerad data
        /// </summary>
        private WebsiteContentResponse ParseAiResponse(string aiText, GenerateWebsiteContentRequest request)
        {
            try
            {
                var jsonText = aiText.Trim();
                if (jsonText.StartsWith("```json")) jsonText = jsonText.Substring(7);
                if (jsonText.StartsWith("```")) jsonText = jsonText.Substring(3);
                if (jsonText.EndsWith("```")) jsonText = jsonText.Substring(0, jsonText.Length - 3);
                jsonText = jsonText.Trim();

                var jsonDoc = JsonDocument.Parse(jsonText);
                var root = jsonDoc.RootElement;

                var tagline = GetStringProperty(root, "tagline", $"Din partner i {request.City}");
                var logoIcon = GetStringProperty(root, "logoIcon", "fas fa-building");
                var heroTitle = GetStringProperty(root, "heroTitle", $"Välkommen till {request.CompanyName}");
                var heroText = GetStringProperty(root, "heroText", $"Vi är ett {request.Industry.ToLower()}-företag i {request.City}.");
                var ctaPrimary = GetStringProperty(root, "ctaPrimary", "Kontakta oss");
                var ctaSecondary = GetStringProperty(root, "ctaSecondary", "Läs mer");

                var values = new List<ValueCard>();
                if (root.TryGetProperty("values", out var valuesElement) && valuesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var valueElement in valuesElement.EnumerateArray())
                    {
                        values.Add(new ValueCard(
                            GetStringProperty(valueElement, "icon", "fas fa-check"),
                            GetStringProperty(valueElement, "title", "Värdering"),
                            GetStringProperty(valueElement, "text", "Beskrivning")
                        ));
                    }
                }

                if (values.Count == 0)
                {
                    values.Add(new ValueCard("fas fa-check", "Kvalitet", "Vi levererar högsta kvalitet"));
                    values.Add(new ValueCard("fas fa-heart", "Engagemang", "Vi bryr oss om våra kunder"));
                    values.Add(new ValueCard("fas fa-clock", "Punktlighet", "Vi håller våra tidsramar"));
                }
                values = values.Take(3).ToList();

                var aboutSubtitle = GetStringProperty(root, "aboutSubtitle", "Om oss");
                var aboutTitle = GetStringProperty(root, "aboutTitle", $"Välkommen till {request.CompanyName}");
                var aboutParagraphs = new List<string>();
                if (root.TryGetProperty("aboutParagraphs", out var paragraphsElement) && paragraphsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var paragraph in paragraphsElement.EnumerateArray())
                    {
                        if (paragraph.ValueKind == JsonValueKind.String)
                        {
                            aboutParagraphs.Add(paragraph.GetString() ?? "");
                        }
                    }
                }
                if (aboutParagraphs.Count == 0)
                {
                    aboutParagraphs.Add($"{request.CompanyName} är ett {request.Industry.ToLower()}-företag i {request.City}.");
                }

                var aboutCtaText = GetStringProperty(root, "aboutCtaText", "Kontakta oss");
                var ownerName = GetStringProperty(root, "ownerName", request.Ceo ?? "VD");
                var ownerTitle = GetStringProperty(root, "ownerTitle", "VD");

                var about = new AboutContent(aboutSubtitle, aboutTitle, aboutParagraphs, "", aboutCtaText, ownerName, ownerTitle);

                var services = new List<ServiceCard>();
                if (root.TryGetProperty("services", out var servicesElement) && servicesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var serviceElement in servicesElement.EnumerateArray())
                    {
                        services.Add(new ServiceCard(
                            GetStringProperty(serviceElement, "title", "Tjänst"),
                            GetStringProperty(serviceElement, "description", "Beskrivning"),
                            ""
                        ));
                    }
                }

                if (services.Count == 0 && request.TopServices?.Count > 0)
                {
                    foreach (var service in request.TopServices)
                    {
                        services.Add(new ServiceCard(service, $"Vi erbjuder {service.ToLower()}", ""));
                    }
                }
                else if (services.Count == 0)
                {
                    services.Add(new ServiceCard("Våra tjänster", "Vi erbjuder professionella tjänster", ""));
                }
                services = services.Take(3).ToList();

                var faqs = new List<FaqCard>();
                if (root.TryGetProperty("faqs", out var faqsElement) && faqsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var faqElement in faqsElement.EnumerateArray())
                    {
                        faqs.Add(new FaqCard(
                            GetStringProperty(faqElement, "icon", "fas fa-question"),
                            GetStringProperty(faqElement, "question", "Fråga?"),
                            GetStringProperty(faqElement, "answer", "Svar")
                        ));
                    }
                }

                if (faqs.Count == 0)
                {
                    faqs.Add(new FaqCard("fas fa-question", "Hur kontaktar jag er?", "Du kan ringa oss eller skicka ett meddelande via kontaktformuläret."));
                }
                faqs = faqs.Take(3).ToList();

                var contactIntro = GetStringProperty(root, "contactIntro", "Kontakta oss för mer information");
                var phone = GetStringProperty(root, "phone", "");
                var email = GetStringProperty(root, "email", "");
                var contact = new ContactInfo(contactIntro, phone, email);

                var trustBand = new TrustBand(
                    GetStringProperty(root, "trust1", "Erfaren och pålitlig"),
                    GetStringProperty(root, "trust2", "Professionell service"),
                    GetStringProperty(root, "trust3", "Nöjda kunder")
                );

                var hero = new HeroContent(heroTitle, heroText, "", ctaPrimary, ctaSecondary);

                return new WebsiteContentResponse(request.CompanyName, tagline, logoIcon, hero, trustBand, values, about, services, faqs, contact);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Kunde inte parsa AI-svar som JSON: {AiText}", aiText);
                return CreateFallbackContent(request);
            }
        }

        private string GetStringProperty(JsonElement element, string propertyName, string defaultValue)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? defaultValue;
            }
            return defaultValue;
        }

        private WebsiteContentResponse CreateFallbackContent(GenerateWebsiteContentRequest request)
        {
            _logger.LogWarning("Använder fallback-innehåll för {CompanyName}", request.CompanyName);

            var hero = new HeroContent($"Välkommen till {request.CompanyName}", $"Vi är ett {request.Industry.ToLower()}-företag i {request.City}.", "", "Kontakta oss", "Läs mer");

            var values = new List<ValueCard>
            {
                new ValueCard("fas fa-check", "Kvalitet", "Vi levererar högsta kvalitet"),
                new ValueCard("fas fa-heart", "Engagemang", "Vi bryr oss om våra kunder"),
                new ValueCard("fas fa-clock", "Punktlighet", "Vi håller våra tidsramar")
            };

            var about = new AboutContent("Om oss", $"Välkommen till {request.CompanyName}", new List<string> { $"{request.CompanyName} är ett {request.Industry.ToLower()}-företag i {request.City}." }, "", "Kontakta oss", request.Ceo ?? "VD", "VD");

            var services = new List<ServiceCard>();
            if (request.TopServices?.Count > 0)
            {
                foreach (var service in request.TopServices.Take(3))
                {
                    services.Add(new ServiceCard(service, $"Vi erbjuder {service.ToLower()}", ""));
                }
            }
            else
            {
                services.Add(new ServiceCard("Våra tjänster", "Vi erbjuder professionella tjänster", ""));
            }

            var faqs = new List<FaqCard>
            {
                new FaqCard("fas fa-question", "Hur kontaktar jag er?", "Du kan ringa oss eller skicka ett meddelande via kontaktformuläret."),
                new FaqCard("fas fa-clock", "Hur lång tid tar det?", "Det beror på projektets omfattning, men vi give alltid en tidsuppskattning."),
                new FaqCard("fas fa-money-bill", "Hur får jag en offert?", "Kontakta oss så ger vi dig ett prisförslag helt kostnadsfritt.")
            };

            var contact = new ContactInfo("Kontakta oss för mer information", "", "");
            var trustBand = new TrustBand("Erfaren och pålitlig", "Professionell service", "Nöjda kunder");

            return new WebsiteContentResponse(request.CompanyName, $"Din partner i {request.City}", "fas fa-building", hero, trustBand, values, about, services, faqs, contact);
        }
    }
}