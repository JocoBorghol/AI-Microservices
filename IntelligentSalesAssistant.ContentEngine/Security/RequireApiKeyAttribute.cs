using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace ISA.ContentEngine.Security;

public class RequireApiKeyAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetService<IConfiguration>();
        if (configuration == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var expectedApiKey = configuration["ServiceAuth:ApiKey"];

        // Om nyckeln är tom/null: returnera 500 Internal Server Error för att förhindra bypass
        if (string.IsNullOrEmpty(expectedApiKey))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = 500,
                Title = "Configuration Error",
                Detail = "API key verification configuration is missing. Access denied for safety."
            })
            {
                StatusCode = 500
            };
            return;
        }

        // Läs inkommande header "X-Api-Key"
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var incomingApiKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Om header != förväntad nyckel
        if (incomingApiKey != expectedApiKey)
        {
            context.Result = new UnauthorizedResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        // Tom metod
    }
}