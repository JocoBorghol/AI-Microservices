using System.Text;
using System.Text.RegularExpressions;
using IntelligentSalesAssistantAPI.DTOs;
using IntelligentSalesAssistantAPI.Exceptions;

namespace IntelligentSalesAssistantAPI.Services.WebsiteGenerator
{
    /// <summary>
    /// Hanterar HTML-mallar: läsning, rendering och filsparning
    /// </summary>
    public class TemplateService : ITemplateService
    {
        // Sökvägar relativt till projektroten (en nivå upp från IntelligentSalesAssistantAPI/)
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        private static readonly string TemplateBasePath = Path.Combine(ProjectRoot, "Site", "template");
        private static readonly string TemplatePath = Path.Combine(ProjectRoot, "Site", "template", "index.html");
        private static readonly string TemplateStylesPath = Path.Combine(ProjectRoot, "Site", "template", "styles.css");
        private static readonly string TemplateJsPath = Path.Combine(ProjectRoot, "Site", "template", "app.js");
        private static readonly string TemplateThemesPath = Path.Combine(ProjectRoot, "Site", "template", "themes");
        private static readonly string GeneratedBasePath = Path.Combine(ProjectRoot, "Site", "generated");
        private readonly ILogger<TemplateService> _logger;

        public TemplateService(ILogger<TemplateService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string> LoadTemplateAsync(CancellationToken ct = default)
        {
            if (!File.Exists(TemplatePath))
                throw new TemplateException($"Mall-filen hittades inte: {TemplatePath}");

            try
            {
                return await File.ReadAllTextAsync(TemplatePath, ct);
            }
            catch (Exception ex) when (ex is not TemplateException)
            {
                throw new TemplateException($"Kunde inte läsa mall-filen: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Renderar HTML-mall med innehåll
        /// </summary>
        public Task<string> RenderTemplateAsync(
            string template, 
            WebsiteContentResponse content,
            string sanitizedCompanyName,
            CancellationToken ct = default)
        {
            try
            {
                var html = template;
                
                // Skapa sökväg till företagets bildmapp för local override
                var folderPath = Path.Combine(GeneratedBasePath, sanitizedCompanyName, "images");

                // Enkla placeholders
                html = html.Replace("{{COMPANY_NAME}}", content.CompanyName);
                html = html.Replace("{{TAGLINE}}", content.Tagline);
                html = html.Replace("{{LOGO_ICON}}", content.LogoIcon);
                html = html.Replace("{{YEAR}}", DateTime.Now.Year.ToString());

                // Hero-sektion med manual image only
                html = html.Replace("{{HERO_TITLE}}", content.Hero.Title);
                html = html.Replace("{{HERO_TEXT}}", content.Hero.Text);
                
                // Manual image only: Kontrollera om hero.jpg finns och är större än 1KB
                var heroFileInfo = new FileInfo(Path.Combine(folderPath, "hero.jpg"));
                if (heroFileInfo.Exists && heroFileInfo.Length > 1000)
                {
                    // Bild finns - använd den
                    html = html.Replace("{{HERO_BG_URL}}", "images/hero.jpg");
                }
                else
                {
                    // Ingen bild - använd gradient bakgrund
                    html = html.Replace("{{HERO_BG_URL}}", "");
                }
                
                html = html.Replace("{{CTA_PRIMARY}}", content.Hero.CtaPrimary);
                html = html.Replace("{{CTA_SECONDARY}}", content.Hero.CtaSecondary);

                // Trust Band
                html = html.Replace("{{TRUST_1}}", content.TrustBand.Trust1);
                html = html.Replace("{{TRUST_2}}", content.TrustBand.Trust2);
                html = html.Replace("{{TRUST_3}}", content.TrustBand.Trust3);

                // Om oss-sektion med manual image only
                html = html.Replace("{{ABOUT_SUBTITLE}}", content.About.Subtitle);
                html = html.Replace("{{ABOUT_TITLE}}", content.About.Title);
                
                // Manual image only: Kontrollera om about.jpg finns och är större än 1KB
                var aboutFileInfo = new FileInfo(Path.Combine(folderPath, "about.jpg"));
                if (aboutFileInfo.Exists && aboutFileInfo.Length > 1000)
                {
                    // Bild finns - rendera img-taggen
                    html = html.Replace("{{ABOUT_IMAGE_URL}}", "images/about.jpg");
                }
                else
                {
                    // Ingen bild - ta bort img-taggen helt (hanteras i template)
                    html = html.Replace("{{ABOUT_IMAGE_URL}}", "");
                }
                
                html = html.Replace("{{ABOUT_CTA}}", content.About.CtaText);
                html = html.Replace("{{OWNER_NAME}}", content.About.OwnerName);
                html = html.Replace("{{OWNER_TITLE}}", content.About.OwnerTitle);

                // Kontakt
                html = html.Replace("{{CONTACT_INTRO}}", content.Contact.IntroText);
                html = html.Replace("{{PHONE}}", content.Contact.Phone);
                html = html.Replace("{{EMAIL}}", content.Contact.Email);

                // Adress (valfri — visas bara om den finns)
                var address = content.Contact.Address ?? "";
                if (!string.IsNullOrWhiteSpace(address))
                {
                    html = html.Replace("{{ADDRESS_ITEM}}", $@"<div class='dc-item'>
                            <i class='fas fa-map-marker-alt'></i>
                            <div><strong>Adress </strong><span>{address}</span></div>
                        </div>");
                }
                else
                {
                    html = html.Replace("{{ADDRESS_ITEM}}", "");
                }

                // Sociala medier (valfria — visas bara om URL finns)
                var facebookUrl = content.Contact.FacebookUrl ?? "";
                var instagramUrl = content.Contact.InstagramUrl ?? "";
                html = html.Replace("{{FACEBOOK_LINK}}", !string.IsNullOrWhiteSpace(facebookUrl)
                    ? $@"<a href=""{facebookUrl}"" target=""_blank"" rel=""noopener"" class=""social-link""><i class=""fab fa-facebook""></i></a>"
                    : "");
                html = html.Replace("{{INSTAGRAM_LINK}}", !string.IsNullOrWhiteSpace(instagramUrl)
                    ? $@"<a href=""{instagramUrl}"" target=""_blank"" rel=""noopener"" class=""social-link""><i class=""fab fa-instagram""></i></a>"
                    : "");

                // Värderingar (dynamisk HTML)
                var valuesHtml = new StringBuilder();
                foreach (var value in content.Values)
                {
                    valuesHtml.Append($@"
                        <div class='value-card fade-in'>
                            <i class='{value.Icon}'></i>
                            <h3>{value.Title}</h3>
                            <p>{value.Text}</p>
                        </div>");
                }
                html = html.Replace("{{VALUES}}", valuesHtml.ToString());

                // Om oss-innehåll (paragrafer)
                var aboutContentHtml = new StringBuilder();
                foreach (var paragraph in content.About.Paragraphs)
                {
                    aboutContentHtml.Append($"<p>{paragraph}</p>");
                }
                html = html.Replace("{{ABOUT_CONTENT}}", aboutContentHtml.ToString());

                // Tjänster (dynamisk HTML) med manual image only
                var servicesHtml = new StringBuilder();
                int serviceIndex = 1;
                foreach (var service in content.Services)
                {
                    // Manual image only: Kontrollera om service-bild finns och är större än 1KB
                    var serviceFileInfo1 = new FileInfo(Path.Combine(folderPath, $"service-{serviceIndex}-1.jpg"));
                    var serviceFileInfo2 = new FileInfo(Path.Combine(folderPath, $"service{serviceIndex}.jpg"));
                    var serviceExists = (serviceFileInfo1.Exists && serviceFileInfo1.Length > 1000) || 
                                        (serviceFileInfo2.Exists && serviceFileInfo2.Length > 1000);
                    
                    if (serviceExists)
                    {
                        var imgUrl = serviceFileInfo1.Exists && serviceFileInfo1.Length > 1000
                            ? $"images/service-{serviceIndex}-1.jpg"
                            : $"images/service{serviceIndex}.jpg";

                        // Bild finns - rendera med bild
                        servicesHtml.Append($@"
                        <article class='service-card fade-in service-card-has-image'>
                            <img src=""{imgUrl}"" alt=""{service.Title}"" class=""service-img"" style=""cursor: pointer;"" />
                            <div class='card-body'>
                                <h3>{service.Title}</h3>
                                <p>{service.Description}</p>
                            </div>
                        </article>");
                    }
                    else
                    {
                        // Ingen bild - rendera endast text
                        servicesHtml.Append($@"
                        <article class='service-card service-card-no-image fade-in'>
                            <div class='card-body'>
                                <h3>{service.Title}</h3>
                                <p>{service.Description}</p>
                            </div>
                        </article>");
                    }
                    serviceIndex++;
                }
                html = html.Replace("{{SERVICES}}", servicesHtml.ToString());

                // FAQ (ersätt individuella placeholders)
                int faqIndex = 1;
                foreach (var faq in content.Faqs)
                {
                    html = html.Replace($"{{{{FAQ{faqIndex}_QUESTION}}}}", faq.Question);
                    html = html.Replace($"{{{{FAQ{faqIndex}_ANSWER}}}}", faq.Answer);
                    faqIndex++;
                }

                // Fallback om det genererats färre än 3 FAQs
                for (int i = faqIndex; i <= 3; i++)
                {
                    html = html.Replace($"{{{{FAQ{i}_QUESTION}}}}", "Fråga?");
                    html = html.Replace($"{{{{FAQ{i}_ANSWER}}}}", "Svar kommer inom kort.");
                }

                return Task.FromResult(html);
            }
            catch (Exception ex) when (ex is not TemplateException)
            {
                throw new TemplateException($"Kunde inte rendera mallen: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task SaveWebsiteAsync(string companyName, string html, CancellationToken ct = default)
        {
            var sanitizedName = SanitizeCompanyName(companyName);
            var folderPath = Path.Combine(GeneratedBasePath, sanitizedName);

            try
            {
                Directory.CreateDirectory(folderPath);

                await File.WriteAllTextAsync(Path.Combine(folderPath, "index.html"), html, ct);

                var cssSource = TemplateStylesPath;
                var jsSource = TemplateJsPath;

                if (File.Exists(cssSource))
                {
                    var cssContent = await File.ReadAllBytesAsync(cssSource, ct);
                    await File.WriteAllBytesAsync(Path.Combine(folderPath, "styles.css"), cssContent, ct);
                }

                if (File.Exists(jsSource))
                {
                    var jsContent = await File.ReadAllBytesAsync(jsSource, ct);
                    await File.WriteAllBytesAsync(Path.Combine(folderPath, "app.js"), jsContent, ct);
                }

                // Kopiera themes-mappen
                if (Directory.Exists(TemplateThemesPath))
                {
                    var themesDestPath = Path.Combine(folderPath, "themes");
                    Directory.CreateDirectory(themesDestPath);
                    
                    foreach (var themeFile in Directory.GetFiles(TemplateThemesPath, "*.css"))
                    {
                        var fileName = Path.GetFileName(themeFile);
                        var destFile = Path.Combine(themesDestPath, fileName);
                        var themeContent = await File.ReadAllBytesAsync(themeFile, ct);
                        await File.WriteAllBytesAsync(destFile, themeContent, ct);
                    }
                    
                    _logger.LogInformation("Themes-mappen kopierad: {ThemesCount} teman", Directory.GetFiles(TemplateThemesPath, "*.css").Length);
                }

                // Skapa images-mappen med instruktionsfil
                var imagesDestPath = Path.Combine(folderPath, "images");
                Directory.CreateDirectory(imagesDestPath);
                
                // Skapa instruktionsfil för bilder
                var instructionsPath = Path.Combine(imagesDestPath, "INSTRUCTIONS_FOR_IMAGES.txt");
                var instructions = @"To add your own images, place them in this folder and name them exactly:
- hero.jpg (for the hero/header background)
- about.jpg (for the about section)
- service1.jpg (for the first service)
- service2.jpg (for the second service)
- service3.jpg (for the third service)

Files must be in .jpg format and larger than 1KB.

The system will automatically detect and use these images when you regenerate the website.";
                
                await File.WriteAllTextAsync(instructionsPath, instructions, ct);
                _logger.LogInformation("Images-mappen skapad med instruktionsfil");

                _logger.LogInformation("Hemsida sparad: {FolderPath}", folderPath);
            }
            catch (Exception ex) when (ex is not FileOperationException)
            {
                throw new FileOperationException($"Kunde inte spara hemsidan för '{companyName}': {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public Task DeleteWebsiteAsync(string companyName, CancellationToken ct = default)
        {
            var sanitizedName = SanitizeCompanyName(companyName);
            var folderPath = Path.Combine(GeneratedBasePath, sanitizedName);

            try
            {
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, recursive: true);
                    _logger.LogInformation("Hemsida raderad: {FolderPath}", folderPath);
                }
                return Task.CompletedTask;
            }
            catch (Exception ex) when (ex is not FileOperationException)
            {
                throw new FileOperationException($"Kunde inte radera hemsidan för '{companyName}': {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public string SanitizeCompanyName(string companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName))
                return "okant-foretag";

            var name = companyName.ToLowerInvariant();

            // Svenska tecken
            name = name.Replace("å", "a").Replace("ä", "a").Replace("ö", "o");

            // Mellanslag till bindestreck
            name = name.Replace(" ", "-");

            // Ta bort ogiltiga filnamntecken
            name = Regex.Replace(name, @"[^a-z0-9\-]", "");

            // Ta bort dubbla bindestreck
            name = Regex.Replace(name, @"-{2,}", "-");

            // Trimma bindestreck i början/slutet
            name = name.Trim('-');

            return string.IsNullOrEmpty(name) ? "okant-foretag" : name;
        }
    }
}
