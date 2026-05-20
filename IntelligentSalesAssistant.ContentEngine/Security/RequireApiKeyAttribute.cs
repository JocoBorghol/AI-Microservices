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

        // Om nyckeln är tom/null: returnera (inaktiverad i dev)
        if (string.IsNullOrEmpty(expectedApiKey))
        {
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