using IntelligentSalesAssistantAPI.Data;
using IntelligentSalesAssistantAPI.DTOs;
using IntelligentSalesAssistantAPI.Http.Clients;
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
        private static int _nextId = 1;
        private static readonly Dictionary<int, string> _draftPaths = new();

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
            Models.CompanyWebsite? website = null;

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

            // Bygg prompt för Gemini
            var prompt = BuildPrompt(request, companyContext);

            // Anropa Service B för att generera innehåll
            var generatedContent = await _llmClient.GenerateContentAsync(prompt, ct, "content-draft-service");

            // Spara till fil och få tillbaka ID
            var (id, filePath) = await SaveDraftToFileAsync(
                generatedContent, 
                request.ContentType, 
                companyName ?? "general");

            _logger.LogInformation("Innehållsutkast sparat med ID {Id} till {FilePath}", id, filePath);

            return new ContentDraftResponse(
                id,
                generatedContent,
                filePath,
                request.ContentType,
                companyName,
                DateTime.UtcNow
            );
        }

        public Task<ContentDraftListResponse> GetDraftsAsync(string? companyName = null)
        {
            if (!Directory.Exists(_draftsBasePath))
            {
                return Task.FromResult(new ContentDraftListResponse(0, new List<ContentDraftInfo>()));
            }

            var drafts = new List<ContentDraftInfo>();

            // Sök i alla undermappar eller specifik företagsmapp
            var searchPath = string.IsNullOrEmpty(companyName) 
                ? _draftsBasePath 
                : Path.Combine(_draftsBasePath, SanitizeCompanyName(companyName));

            if (!Directory.Exists(searchPath))
            {
                return Task.FromResult(new ContentDraftListResponse(0, new List<ContentDraftInfo>()));
            }

            var files = Directory.GetFiles(searchPath, "*.txt", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                var relativePath = GetRelativePath(file);
                
                // Hitta eller skapa ID för denna fil
                var id = _draftPaths.FirstOrDefault(x => x.Value == relativePath).Key;
                if (id == 0)
                {
                    id = _nextId++;
                    _draftPaths[id] = relativePath;
                }
                
                // Extrahera content type och företagsnamn från sökvägen
                var contentType = ExtractContentTypeFromFileName(fileName);
                var company = ExtractCompanyNameFromPath(relativePath);

                drafts.Add(new ContentDraftInfo(
                    id,
                    fileInfo.Name,
                    relativePath,
                    contentType,
                    company,
                    fileInfo.CreationTimeUtc,
                    fileInfo.Length
                ));
            }

            // Sortera nyast först
            drafts = drafts.OrderByDescending(d => d.CreatedAt).ToList();

            return Task.FromResult(new ContentDraftListResponse(drafts.Count, drafts));
        }

        public async Task<string> GetDraftContentAsync(int id)
        {
            if (!_draftPaths.TryGetValue(id, out var relativePath))
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte");
            }

            var fullPath = Path.Combine(_draftsBasePath, relativePath.Replace("/", "\\"));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte på disk");
            }

            return await File.ReadAllTextAsync(fullPath);
        }

        public async Task DeleteDraftAsync(int id)
        {
            if (!_draftPaths.TryGetValue(id, out var relativePath))
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte");
            }

            var fullPath = Path.Combine(_draftsBasePath, relativePath.Replace("/", "\\"));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Utkast med ID {id} hittades inte på disk");
            }

            File.Delete(fullPath);
            _draftPaths.Remove(id);
            _logger.LogInformation("Utkast med ID {Id} raderat", id);

            await Task.CompletedTask;
        }

        private string BuildPrompt(CreateContentDraftRequest request, string? companyContext)
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
            sb.AppendLine("KRAV:");
            sb.AppendLine("- Skriv på svenska");
            sb.AppendLine("- Var kreativ och engagerande");
            sb.AppendLine("- Anpassa innehållet till den angivna målgruppen");
            sb.AppendLine("- Returnera ENDAST innehållet, ingen extra förklaring");

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

        private async Task<(int id, string filePath)> SaveDraftToFileAsync(string content, string contentType, string companyName)
        {
            var sanitizedCompanyName = SanitizeCompanyName(companyName);
            var companyFolder = Path.Combine(_draftsBasePath, sanitizedCompanyName);

            // Skapa mapp om den inte finns
            Directory.CreateDirectory(companyFolder);

            // Generera filnamn med timestamp (sanitera contentType)
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
            var sanitizedContentType = SanitizeFileName(contentType);
            var fileName = $"{sanitizedContentType}-{timestamp}.txt";
            var fullPath = Path.Combine(companyFolder, fileName);

            // Spara innehåll
            await File.WriteAllTextAsync(fullPath, content);

            // Skapa ID och spara mapping
            var id = _nextId++;
            var relativePath = Path.Combine(sanitizedCompanyName, fileName).Replace("\\", "/");
            _draftPaths[id] = relativePath;

            // Returnera ID och relativ sökväg
            return (id, relativePath);
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

        private string ExtractContentTypeFromFileName(string fileName)
        {
            // Format: contenttype-timestamp
            var parts = fileName.Split('-');
            return parts.Length > 0 ? parts[0] : "unknown";
        }

        private string? ExtractCompanyNameFromPath(string relativePath)
        {
            // Format: company-name/filename.txt
            var parts = relativePath.Split('/');
            return parts.Length > 0 ? parts[0] : null;
        }

        private string GetRelativePath(string fullPath)
        {
            return fullPath.Replace(_draftsBasePath, "").TrimStart('\\', '/').Replace("\\", "/");
        }
    }
}
