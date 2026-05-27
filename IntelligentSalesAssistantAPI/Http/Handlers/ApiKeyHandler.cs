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
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add("X-Api-Key", apiKey);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
