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

        // Den osäkra 1-argumentsmetoden har tagits bort för att tvinga fram separation av system/user prompt.

        public async Task<string> GenerateContentAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            var payload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = userPrompt } }
                    }
                },
                safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT",   threshold = "BLOCK_LOW_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_HARASSMENT",          threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH",         threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT",   threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.GenerateContentUrl)
            {
                Content = content
            };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);

            _logger.LogDebug("Skickar request med systemInstruction till Gemini: {Url}", _options.GenerateContentUrl);

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                // Trunkera felsvaret till max 200 tecken för att förhindra läckage av känslig data från externa API:er
                var sanitizedError = errorBody.Length > 200 ? errorBody.Substring(0, 200) + "..." : errorBody;
                _logger.LogWarning("Gemini API returnerade {StatusCode}: {ErrorBody}", response.StatusCode, sanitizedError);
                throw new HttpRequestException(
                    $"Gemini API returnerade status {response.StatusCode}. Detaljer: {sanitizedError}",
                    null,
                    response.StatusCode);
            }

            var responseString = await response.Content.ReadAsStringAsync(ct);
            var jsonNode = JsonNode.Parse(responseString);

            var reply = jsonNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();

            return reply ?? "Kunde inte tolka svaret från Gemini.";
        }
    }
}
