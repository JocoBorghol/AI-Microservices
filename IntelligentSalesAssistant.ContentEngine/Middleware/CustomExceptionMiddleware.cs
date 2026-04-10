using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ISA.ContentEngine.Middleware
{
    public class CustomExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CustomExceptionMiddleware> _logger;

        public CustomExceptionMiddleware(RequestDelegate next, ILogger<CustomExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Externt API-fel: {Message}", ex.Message);
                int statusCode = ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : StatusCodes.Status502BadGateway;
                if (statusCode == 429) statusCode = StatusCodes.Status429TooManyRequests;
                
                context.Response.ContentType = "application/problem+json";
                context.Response.StatusCode = statusCode;

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Externt API-anrop misslyckades.",
                    Detail = ex.Message,
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

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ett internt serverfel uppstod.",
                Detail = "Ett oväntat fel inträffade. Vänligen försök igen eller kontakta IT-support.",
                Type = "https://datatracker.ietf.org/doc/html/rfc7807",
                Instance = context.Request.Path
            };

            var jsonResponse = JsonSerializer.Serialize(problemDetails);
            return context.Response.WriteAsync(jsonResponse);
        }
    }
}
