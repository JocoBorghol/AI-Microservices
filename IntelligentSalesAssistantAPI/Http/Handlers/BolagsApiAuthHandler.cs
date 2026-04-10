using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace IntelligentSalesAssistantAPI.Http.Handlers
{
    // Lägger till Bearer-token för BolagsAPI
    public class BolagsApiAuthHandler : DelegatingHandler
    {
        private readonly IConfiguration _config;
        public BolagsApiAuthHandler(IConfiguration config) => _config = config;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = _config["Apis:BolagsApi:ApiKey"];
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return base.SendAsync(request, cancellationToken);
        }
    }
}