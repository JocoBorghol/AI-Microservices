using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace IntelligentSalesAssistantAPI.Http.Handlers
{
    public class ApiKeyHandler : DelegatingHandler
    {
        private readonly IConfiguration _configuration;

        public ApiKeyHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var apiKey = _configuration["LlmProxySettings:ApiKey"];

            // Fail-Closed: blockera anropet omedelbart om nyckeln saknas i konfigurationen
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "Konfigurationsfel: Intern API-nyckel saknas för tjänst-till-tjänst-kommunikation. " +
                    "Sätt 'LlmProxySettings:ApiKey' via User Secrets (lokalt) eller miljövariabel (produktion).");
            }

            request.Headers.Add("X-Api-Key", apiKey);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
