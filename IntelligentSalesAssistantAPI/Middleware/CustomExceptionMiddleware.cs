using Microsoft.AspNetCore.Mvc;
using System.Net.Sockets;
using System.Text.Json;

namespace IntelligentSalesAssistantAPI.Middleware
{
    // Fångar och hanterar undantag globalt i API:et
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
            catch (IntelligentSalesAssistantAPI.Exceptions.UnauthorizedException ex)
            {
                _logger.LogWarning("Unauthorized: {Message}", ex.Message);
                await WriteProblemDetails(context, StatusCodes.Status401Unauthorized, "Obehörig", ex.Message);
            }
            catch (IntelligentSalesAssistantAPI.Exceptions.ValidationException ex)
            {
                _logger.LogInformation("Validation error: {Message}", ex.Message);
                await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Valideringsfel", ex.Message);
            }
            catch (IntelligentSalesAssistantAPI.Exceptions.CompanyNotFoundException ex)
            {
                _logger.LogInformation("Company not found: {Message}", ex.Message);
                await WriteProblemDetails(context, StatusCodes.Status404NotFound, "Företaget hittades inte.", ex.Message);
            }
            catch (IntelligentSalesAssistantAPI.Exceptions.NotFoundException ex)
            {
                _logger.LogInformation("Resource not found: {Message}", ex.Message);
                await WriteProblemDetails(context, StatusCodes.Status404NotFound, "Resursen hittades inte.", ex.Message);
            }
            catch (IntelligentSalesAssistantAPI.Exceptions.TemplateException ex)
            {
                _logger.LogError(ex, "Template error: {Message}", ex.Message);
                await WriteProblemDetails(context, StatusCodes.Status500InternalServerError, "Mallfel", ex.Message);
            }
            catch (IntelligentSalesAssistantAPI.Exceptions.FileOperationException ex)
            {
                _logger.LogError(ex, "File operation error: {Message}", ex.Message);
                await WriteProblemDetails(context, StatusCodes.Status500InternalServerError, "Filoperationsfel", ex.Message);
            }
            catch (HttpRequestException ex)
            {
                // Microservice-kommunikationsfel (Service B är nere eller kan inte nås)
                _logger.LogError(ex, "Microservice communication failure: {Message}", ex.Message);
                
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    await WriteProblemDetails(
                        context, 
                        StatusCodes.Status502BadGateway, 
                        "AI-tjänstautentisering misslyckades", 
                        "Autentiseringen mot AI-tjänsten misslyckades. Kontrollera systemets konfiguration.");
                }
                else if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await WriteProblemDetails(
                        context, 
                        StatusCodes.Status429TooManyRequests, 
                        "AI-tjänsten är överbelastad", 
                        "AI-tjänsten är för närvarande överbelastad. Vänligen försök igen om en stund.");
                }
                else
                {
                    await HandleMicroserviceFailureAsync(context, ex);
                }
            }
            catch (OperationCanceledException ex)
            {
                // Fångar upp TaskCanceledException vid timeout mot externa anrop
                _logger.LogError(ex, "The request to the AI Service timed out.");
                await WriteProblemDetails(
                    context, 
                    StatusCodes.Status504GatewayTimeout, 
                    "Timeout i gateway", 
                    "Anropet till AI-tjänsten tog för lång tid och avbröts.");
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "The request to the AI Service timed out.");
                await WriteProblemDetails(
                    context, 
                    StatusCodes.Status504GatewayTimeout, 
                    "Timeout i gateway", 
                    "Anropet till AI-tjänsten tog för lång tid och avbröts.");
            }
            catch (Exception ex) when (ex.InnerException is SocketException)
            {
                // Nätverksfel (Socket-nivå)
                _logger.LogError(ex, "Network socket error: {Message}", ex.InnerException.Message);
                await HandleMicroserviceFailureAsync(context, ex);
            }
            catch (Exception ex)
            {
                // Generiskt serverfel (bug i vår kod)
                _logger.LogError(ex, "Ett oväntat serverfel inträffade i API:et.");
                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// Hanterar microservice-kommunikationsfel (Service B nere)
        /// Returnerar 503 Service Unavailable enligt RFC 7807
        /// </summary>
        private Task HandleMicroserviceFailureAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            // Environment-aware detail message
            var detail = _environment.IsDevelopment()
                ? $"Kunde inte ansluta till Content Engine (Service B). Kontrollera att tjänsten körs på port 5006. Fel: {exception.Message}"
                : "En extern tjänst är för närvarande otillgänglig. Vänligen försök igen om en stund.";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Tjänsten är tillfälligt otillgänglig",
                Detail = detail,
                Type = "https://datatracker.ietf.org/doc/html/rfc7807",
                Instance = context.Request.Path
            };

            var jsonResponse = JsonSerializer.Serialize(problemDetails);
            return context.Response.WriteAsync(jsonResponse);
        }

        /// <summary>
        /// Hanterar generiska serverfel (500 Internal Server Error)
        /// </summary>
        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Standardized error handling following RFC 7807 (Problem Details) for API consumers
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // Environment-aware detail message
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

        private static Task WriteProblemDetails(HttpContext context, int statusCode, string title, string detail)
        {
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
            var jsonResponse = JsonSerializer.Serialize(problemDetails);
            return context.Response.WriteAsync(jsonResponse);
        }
    }
}