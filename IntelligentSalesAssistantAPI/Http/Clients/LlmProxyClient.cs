using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelligentSalesAssistantAPI.DTOs;

namespace IntelligentSalesAssistantAPI.Http.Clients
{
    // Typed client för kommunikation med Service B (LLM Proxy API)
    public class LlmProxyClient
    {
        private readonly HttpClient _httpClient;

        public LlmProxyClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GenerateContentAsync(string systemPrompt, string userPrompt, CancellationToken ct, string clientId = "service-a")
        {
            var requestBody = new { systemPrompt = systemPrompt, userPrompt = userPrompt, clientId = clientId };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/content/generate", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Fel från Service B (LLM Proxy): {response.StatusCode} - {errorBody}", null, response.StatusCode);
            }

            var contentResponse = await response.Content.ReadFromJsonAsync<ContentResponse>(cancellationToken: ct);
            return contentResponse?.Reply ?? "Kunde inte tolka svar från proxy.";
        }

        public async Task<WebsiteContentResponse> GenerateWebsiteContentAsync(
            GenerateWebsiteContentRequest request,
            CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/content/websites", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Fel från Service B (LLM Proxy): {response.StatusCode} - {errorBody}", null, response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<WebsiteContentResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Tomt svar från Service B");
        }

        private record ContentResponse(string Reply);
    }
}