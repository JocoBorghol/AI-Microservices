using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ISA.ContentEngine.Middleware
{
    public class CustomExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CustomExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public CustomExceptionMiddleware(
            RequestDelegate next,
            ILogger<CustomExceptionMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException ex)
            {
                // Fångar TaskCanceledException (subklass) och OperationCanceledException vid timeout mot Gemini API
                _logger.LogWarning(ex, "Anropet till Gemini API avbröts eller tog för lång tid.");
                context.Response.ContentType = "application/problem+json";
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;

                var timeoutDetail = _environment.IsDevelopment()
                    ? $"Anropet till Gemini API avbröts efter för lång väntetid: {ex.Message}. Försök med en enklare prompt eller vänta och försök igen."
                    : "Hemsidan tog för lång tid att generera. Detta kan bero på hög belastning på AI-tjänsten. Försök igen om en stund eller kontakta support om problemet kvarstår.";

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status504GatewayTimeout,
                    Title = "Timeout i gateway",
                    Detail = timeoutDetail,
                    Type = "https://datatracker.ietf.org/doc/html/rfc7807",
                    Instance = context.Request.Path
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Externt API-fel: {Message}", ex.Message);
                int statusCode = ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : StatusCodes.Status502BadGateway;
                if (statusCode == 429) statusCode = StatusCodes.Status429TooManyRequests;

                var title = statusCode switch
                {
                    401 or 403 => "AI-tjänstens autentisering misslyckades. Kontrollera API-nyckeln.",
                    429 => "AI-tjänsten är överbelastad. Försök igen om en stund.",
                    _ => "Externt API-anrop misslyckades."
                };

                // Graceful Degradation: visa detaljer i Development, generellt meddelande i Production
                var detail = _environment.IsDevelopment()
                    ? ex.Message
                    : "Ett externt API-anrop misslyckades. Vänligen försök igen eller kontakta IT-support.";

                context.Response.ContentType = "application/problem+json";
                context.Response.StatusCode = statusCode;

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                    Type = "https://datatracker.ietf.org/doc/html/rfc7807",
                    Instance = context.Request.Path
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ett oväntat serverfel inträffade i API:et.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // Graceful Degradation: stack trace och detaljer visas endast i Development
            var detail = _environment.IsDevelopment()
                ? $"Ett oväntat fel inträffade: {exception.Message}"
                : "Ett oväntat fel inträffade. Vänligen försök igen eller kontakta IT-support.";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ett internt serverfel uppstod.",
                Detail = detail,
                Type = "https://datatracker.ietf.org/doc/html/rfc7807",
                Instance = context.Request.Path
            };

            var jsonResponse = JsonSerializer.Serialize(problemDetails);
            return context.Response.WriteAsync(jsonResponse);
        }
    }
}
