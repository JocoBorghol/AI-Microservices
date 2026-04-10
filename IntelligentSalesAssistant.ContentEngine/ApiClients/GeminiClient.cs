using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ISA.ContentEngine.Models.Options;
using Microsoft.Extensions.Options;

namespace ISA.ContentEngine.ApiClients
{
    public class GeminiClient : IGeminiClient
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiOptions _options;
        private readonly ILogger<GeminiClient> _logger;

        public GeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<string> GenerateContentAsync(string prompt, CancellationToken ct = default)
        {
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Sätt API-nyckeln som header istället för query-parameter (säkrare)
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.GenerateContentUrl)
            {
                Content = content
            };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);

            _logger.LogDebug("Skickar request till Gemini: {Url}", _options.GenerateContentUrl);

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini API returnerade {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"Gemini API returnerade status {response.StatusCode}. Detaljer: {errorBody}",
                    null,
                    response.StatusCode);
            }

            var responseString = await response.Content.ReadAsStringAsync(ct);
            var jsonNode = JsonNode.Parse(responseString);

            // Extrahera text från Googles svar: {"candidates":[{"content":{"parts":[{"text":"..."}]}}]}
            var reply = jsonNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

            return reply ?? "Kunde inte tolka svaret från Gemini.";
        }
    }
}
