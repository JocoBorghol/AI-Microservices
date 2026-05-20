using IntelligentSalesAssistantAPI.Data;
using IntelligentSalesAssistantAPI.DTOs;
using IntelligentSalesAssistantAPI.Http.Clients;
using IntelligentSalesAssistantAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace IntelligentSalesAssistantAPI.Services.ContentDraft
{
    public class ContentDraftService : IContentDraftService
    {
        private readonly LlmProxyClient _llmClient;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ContentDraftService> _logger;
        private readonly string _draftsBasePath;

        public ContentDraftService(
            LlmProxyClient llmClient,
            ApplicationDbContext db,
            ILogger<ContentDraftService> logger,
            IWebHostEnvironment env)
        {
            _llmClient = llmClient;
            _db = db;
            _logger = logger;
            
            // Använd root Site/drafts istället för IntelligentSalesAssistantAPI/Site/drafts
            var rootPath = Directory.GetParent(env.ContentRootPath)?.FullName ?? env.ContentRootPath;
            _draftsBasePath = Path.Combine(rootPath, "Site", "drafts");
        }

        public async Task<ContentDraftResponse> CreateDraftAsync(CreateContentDraftRequest request, CancellationToken ct)
        {
            _logger.LogInformation("Skapar innehållsutkast av typ {ContentType}", request.ContentType);

            // Hämta hemsidans data baserat på prioritet: websiteId > useLatestWebsite > ingen kontext
            string? companyContext = null;
            string? companyName = null;
            CompanyWebsite? website = null;

            if (request.WebsiteId.HasValue)
            {
                // Prioritet 1: Använd specifik hemsida via ID
                website = await _db.CompanyWebsites.FindAsync(new object[] { request.WebsiteId.Value }, ct);
                
                if (website == null)
                {
                    throw new InvalidOperationException($"Hemsida med ID {request.WebsiteId.Value} hittades inte");
                }
                
                companyName = website.CompanyName;
                companyContext = BuildCompanyContext(website);
                _logger.LogInformation("Använder kontext från hemsida ID {WebsiteId}: {CompanyName}", request.WebsiteId.Value, companyName);
            }
            else if (request.UseLatestWebsite)
            {
                // Prioritet 2: Använd senaste hemsidan
                website = await _db.CompanyWebsites
                    .OrderByDescending(w => w.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (website != null)
                {
                    companyName = website.CompanyName;
                    companyContext = BuildCompanyContext(website);
                    _logger.LogInformation("Använder kontext från senaste hemsida: {CompanyName}", companyName);
                }
                else
                {
                    _logger.LogWarning("Ingen hemsida hittades för kontext");
                }
            }
            else
            {
                _logger.LogInformation("Skapar innehåll utan företagskontext");
            }

            // Hämta tidigare utkast som AI-minne för Context-Awareness
            var previousContentContext = await BuildPreviousContentContextAsync(website?.Id, ct);

            // Bygg prompt för Gemini
            var prompt = BuildPrompt(request, companyContext, previousContentContext);

            // Anropa Service B för att generera innehåll
            var generatedContent = await _llmClient.GenerateContentAsync(prompt, ct, "content-draft-service");

            // Spara till fil (original)
            var (relativeOriginalPath, _) = await SaveOriginalDraftToFileAsync(
                generatedContent, 
                request.ContentType, 
                companyName ?? "general");

            // Spara utkast-entitet i databasen
            var draft = new Models.ContentDraft
            {
                WebsiteId = website?.Id,
                ContentType = request.ContentType,
                Instructions = request.Instructions,
                Purpose = request.Purpose,
                TargetAudience = request.TargetAudience,
                Tone = request.Tone,
                Length = request.Length,
                OriginalContentPath = relativeOriginalPath,
                ModifiedContentPath = null,
                CompanyName = companyName ?? "general",
                CreatedAt = DateTime.UtcNow
            };

            _db.ContentDrafts.Add(draft);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Innehållsutkast sparat i databas med ID {Id} och originalfil {FilePath}", draft.Id, relativeOriginalPath);

            return new ContentDraftResponse(
                draft.Id,
                generatedContent,
                relativeOriginalPath,
                draft.ContentType,
                draft.CompanyName,
                draft.CreatedAt,
                draft.OriginalContentPath,
                draft.ModifiedContentPath
            );
        }

        public async Task<ContentDraftListResponse> GetDraftsAsync(string? companyName = null)
        {
            var query = _db.ContentDrafts.AsQueryable();

            if (!string.IsNullOrEmpty(companyName))
            {
                query = query.Where(d => d.CompanyName != null && d.CompanyName.ToLower().Contains(companyName.ToLower()));
            }

            var dbDrafts = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
            var drafts = new List<ContentDraftInfo>();

            foreach (var draft in dbDrafts)
            {
                var activePath = draft.ModifiedContentPath ?? draft.OriginalContentPath;
                var fullPath = Path.Combine(_draftsBasePath, activePath.Replace("/", "\\"));
                
                long fileSizeBytes = 0;
                if (File.Exists(fullPath))
                {
                    fileSizeBytes = new FileInfo(fullPath).Length;
                }

                var fileName = Path.GetFileName(activePath);

                drafts.Add(new ContentDraftInfo(
                    draft.Id,
                    fileName,
                    activePath,
                    draft.ContentType,
                    draft.CompanyName,
                    draft.CreatedAt,
                    fileSizeBytes,
                    draft.OriginalContentPath,
                    draft.ModifiedContentPath
                ));
            }

            return new ContentDraftListResponse(drafts.Count, drafts);
        }

        public async Task<string> GetDraftContentAsync(int id)
        {
            var draft = await _db.ContentDrafts.FindAsync(id);
            if (draft == null)
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte i databasen");
            }

            var activePath = draft.ModifiedContentPath ?? draft.OriginalContentPath;
            var fullPath = Path.Combine(_draftsBasePath, activePath.Replace("/", "\\"));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte på disk");
            }

            return await File.ReadAllTextAsync(fullPath);
        }

        public async Task DeleteDraftAsync(int id)
        {
            var draft = await _db.ContentDrafts.FindAsync(id);
            if (draft == null)
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte i databasen");
            }

            // Radera originalfilen
            var originalFullPath = Path.Combine(_draftsBasePath, draft.OriginalContentPath.Replace("/", "\\"));
            if (File.Exists(originalFullPath))
            {
                File.Delete(originalFullPath);
            }

            // Radera modifierad fil om den finns
            if (!string.IsNullOrEmpty(draft.ModifiedContentPath))
            {
                var modifiedFullPath = Path.Combine(_draftsBasePath, draft.ModifiedContentPath.Replace("/", "\\"));
                if (File.Exists(modifiedFullPath))
                {
                    File.Delete(modifiedFullPath);
                }
            }

            _db.ContentDrafts.Remove(draft);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Utkast med ID {Id} raderat från databas och disk", id);
        }

        public async Task<ContentDraftResponse> UpdateDraftAsync(int id, string content)
        {
            var draft = await _db.ContentDrafts.FindAsync(id);
            if (draft == null)
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte i databasen");
            }

            // Hitta mapp för att spara modifierad fil
            var originalFullPath = Path.Combine(_draftsBasePath, draft.OriginalContentPath.Replace("/", "\\"));
            var folder = Path.GetDirectoryName(originalFullPath);
            if (string.IsNullOrEmpty(folder))
            {
                throw new InvalidOperationException("Ogiltig sökväg till originalfilen");
            }

            Directory.CreateDirectory(folder);

            // Generera modifierat filnamn baserat på originalets namn
            var originalFileName = Path.GetFileNameWithoutExtension(originalFullPath);
            string modifiedFileName;
            if (originalFileName.EndsWith("-original"))
            {
                modifiedFileName = originalFileName.Substring(0, originalFileName.Length - "-original".Length) + "-modified.txt";
            }
            else
            {
                modifiedFileName = $"{originalFileName}-modified.txt";
            }

            var modifiedFullPath = Path.Combine(folder, modifiedFileName);

            // Spara nytt innehåll utan att röra originalet
            await File.WriteAllTextAsync(modifiedFullPath, content);

            // Uppdatera sökväg i databasen
            var companyDirName = Path.GetFileName(folder);
            var relativeModifiedPath = Path.Combine(companyDirName, modifiedFileName).Replace("\\", "/");
            
            draft.ModifiedContentPath = relativeModifiedPath;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Utkast med ID {Id} uppdaterat manuellt (originalet bevarat)", id);

            return new ContentDraftResponse(
                draft.Id,
                content,
                relativeModifiedPath,
                draft.ContentType,
                draft.CompanyName,
                draft.CreatedAt,
                draft.OriginalContentPath,
                draft.ModifiedContentPath
            );
        }

        public async Task<ContentDraftResponse> RestoreDraftAsync(int id)
        {
            var draft = await _db.ContentDrafts.FindAsync(id);
            if (draft == null)
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte i databasen");
            }

            // Radera den modifierade filen om den finns på disk
            if (!string.IsNullOrEmpty(draft.ModifiedContentPath))
            {
                var modifiedFullPath = Path.Combine(_draftsBasePath, draft.ModifiedContentPath.Replace("/", "\\"));
                if (File.Exists(modifiedFullPath))
                {
                    File.Delete(modifiedFullPath);
                }
            }

            // Återställ databassökvägen
            draft.ModifiedContentPath = null;
            await _db.SaveChangesAsync();

            // Hämta originalinnehållet
            var originalFullPath = Path.Combine(_draftsBasePath, draft.OriginalContentPath.Replace("/", "\\"));
            if (!File.Exists(originalFullPath))
            {
                throw new FileNotFoundException($"Originalfilen för utkast {id} saknas på disk");
            }
            var originalContent = await File.ReadAllTextAsync(originalFullPath);

            _logger.LogInformation("Utkast med ID {Id} återställt till originalutförande", id);

            return new ContentDraftResponse(
                draft.Id,
                originalContent,
                draft.OriginalContentPath,
                draft.ContentType,
                draft.CompanyName,
                draft.CreatedAt,
                draft.OriginalContentPath,
                draft.ModifiedContentPath
            );
        }

        private async Task<string> BuildPreviousContentContextAsync(int? websiteId, CancellationToken ct)
        {
            if (!websiteId.HasValue)
            {
                return string.Empty;
            }

            // Hämta de 3 senaste utkasten för hemsidan
            var latestDrafts = await _db.ContentDrafts
                .Where(d => d.WebsiteId == websiteId.Value)
                .OrderByDescending(d => d.CreatedAt)
                .Take(3)
                .ToListAsync(ct);

            if (!latestDrafts.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("TIDIGARE GENERERAT INNEHÅLL (AI-MINNE):");
            sb.AppendLine("Följande är utdrag från de senaste utkasten som har genererats för detta företag. Använd detta för att skapa ett konsekvent sammanhang, undvika upprepningar eller bygga vidare på tidigare information om det passar.");

            foreach (var draft in latestDrafts)
            {
                var activePath = draft.ModifiedContentPath ?? draft.OriginalContentPath;
                var fullPath = Path.Combine(_draftsBasePath, activePath.Replace("/", "\\"));

                if (File.Exists(fullPath))
                {
                    var rawText = await File.ReadAllTextAsync(fullPath, ct);
                    // Begränsa till max 200 tecken
                    var snippet = rawText.Length > 200 ? rawText.Substring(0, 200) + "..." : rawText;
                    sb.AppendLine($"- Typ: {draft.ContentType}, Skapat: {draft.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"  Innehåll: \"{snippet.Replace("\n", " ").Replace("\r", "")}\"");
                }
            }
            sb.AppendLine();
            return sb.ToString();
        }

        private string BuildPrompt(CreateContentDraftRequest request, string? companyContext, string? previousContentContext)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Du är en expert på att skriva marknadsföringsinnehåll på svenska.");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(companyContext))
            {
                sb.AppendLine("FÖRETAGSKONTEXT:");
                sb.AppendLine(companyContext);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(previousContentContext))
            {
                sb.AppendLine(previousContentContext);
            }

            sb.AppendLine("UPPGIFT:");
            sb.AppendLine($"Skapa ett {request.ContentType} med följande specifikationer:");
            sb.AppendLine();
            sb.AppendLine($"Instruktioner: {request.Instructions}");

            if (!string.IsNullOrEmpty(request.Purpose))
                sb.AppendLine($"Syfte: {request.Purpose}");

            if (!string.IsNullOrEmpty(request.TargetAudience))
                sb.AppendLine($"Målgrupp: {request.TargetAudience}");

            if (!string.IsNullOrEmpty(request.Tone))
                sb.AppendLine($"Ton: {request.Tone}");

            if (!string.IsNullOrEmpty(request.Length))
                sb.AppendLine($"Längd: {request.Length}");

            sb.AppendLine();
            sb.AppendLine("FEW-SHOT EXEMPEL:");
            sb.AppendLine("Följande är ett exempel på hur du ska formatera och formulera dina utkast:");
            sb.AppendLine("Input ContentType: Facebook-inlägg, Ton: Professionell men säljig, Instruktioner: \"Volvo V60 D4 2019, 12 000 mil, välvårdad\"");
            sb.AppendLine("Expected Output:");
            sb.AppendLine("🔥 NYINKOMMEN FAMILJEFAVORIT! 🔥");
            sb.AppendLine();
            sb.AppendLine("Vi har precis fått in en fantastiskt välvårdad **Volvo V60 D4 (2019)** som rullat 12 000 mil. En perfekt kombination av svensk säkerhet, komfort och bränsleekonomi!");
            sb.AppendLine();
            sb.AppendLine("**Höjdpunkter:**");
            sb.AppendLine("- Bränslesnål D4-motor");
            sb.AppendLine("- Väl dokumenterad servicehistorik");
            sb.AppendLine("- Rymligt bagageutrymme för hela familjen");
            sb.AppendLine();
            sb.AppendLine("Kom förbi oss på Bilcenter Syd för en provkörning, eller kontakta en av våra säljare idag! 🚗💨");
            sb.AppendLine();

            sb.AppendLine("KRAV OCH BEGRÄNSNINGAR (GUARDRAILS):");
            sb.AppendLine("- Skriv på svenska.");
            sb.AppendLine("- Var kreativ och engagerande.");
            sb.AppendLine("- Anpassa innehållet till den angivna målgruppen.");
            sb.AppendLine("- Returnera ENDAST det genererade innehållet, lägg inte till någon extra förklaring, introduktion eller kommentar.");
            sb.AppendLine("- Formatera ALLTID utdatan i ren och snygg Markdown (använd korrekta rubriker och punktlistor för specifikationer och höjdpunkter).");
            sb.AppendLine("- HÅRDA REGLER MOT HALLUCINATIONER: Du får INTE hitta på eller gissa utrustningspaket (t.ex. R-Design, M-Sport, AMG, S Line), miltal, priser eller garantivillkor som inte uttryckligen finns angivna i säljarens instruktioner eller bifogad fordonsdata.");
            sb.AppendLine("- OSÄKERHETS-GUARDRAIL: Om instruktionerna är motsägelsefulla, eller om du saknar tillräcklig fordonsdata för att skapa ett trovärdigt utkast, ska du inte gissa utan svara uttryckligen: \"Information saknas. Ange mer specifik fordonsdata för att generera ett säljutkast.\"");

            return sb.ToString();
        }

        private string BuildCompanyContext(Models.CompanyWebsite website)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Företag: {website.CompanyName}");
            sb.AppendLine($"Bransch: {website.Category}");
            
            if (!string.IsNullOrEmpty(website.Tone))
                sb.AppendLine($"Ton: {website.Tone}");
            
            if (!string.IsNullOrEmpty(website.TargetAudience))
                sb.AppendLine($"Målgrupp: {website.TargetAudience}");

            return sb.ToString();
        }

        private async Task<(string relativePath, string fullPath)> SaveOriginalDraftToFileAsync(string content, string contentType, string companyName)
        {
            var sanitizedCompanyName = SanitizeCompanyName(companyName);
            var companyFolder = Path.Combine(_draftsBasePath, sanitizedCompanyName);

            // Skapa mapp om den inte finns
            Directory.CreateDirectory(companyFolder);

            // Generera filnamn med timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
            var sanitizedContentType = SanitizeFileName(contentType);
            var fileName = $"{sanitizedContentType}-{timestamp}-original.txt";
            var fullPath = Path.Combine(companyFolder, fileName);

            // Spara innehåll
            await File.WriteAllTextAsync(fullPath, content);

            var relativePath = Path.Combine(sanitizedCompanyName, fileName).Replace("\\", "/");

            return (relativePath, fullPath);
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "content";
            
            // Ersätt svenska tecken
            var sanitized = fileName.ToLowerInvariant()
                .Replace("å", "a").Replace("ä", "a").Replace("ö", "o")
                .Replace("é", "e").Replace("è", "e").Replace("ü", "u")
                .Replace(" ", "-")
                .Replace("_", "-");
            
            // Ta bort alla tecken som inte är bokstäver, siffror eller bindestreck
            sanitized = Regex.Replace(sanitized, @"[^a-z0-9\-]", "");
            
            // Ta bort flera bindestreck i rad
            sanitized = Regex.Replace(sanitized, @"-{2,}", "-").Trim('-');
            
            return string.IsNullOrEmpty(sanitized) ? "content" : sanitized;
        }

        private string SanitizeCompanyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "general";
            
            var sanitized = name.ToLowerInvariant()
                .Replace("å", "a").Replace("ä", "a").Replace("ö", "o")
                .Replace(" ", "-");
            
            sanitized = Regex.Replace(sanitized, @"[^a-z0-9\-]", "");
            sanitized = Regex.Replace(sanitized, @"-{2,}", "-").Trim('-');
            
            return string.IsNullOrEmpty(sanitized) ? "general" : sanitized;
        }
    }
}
