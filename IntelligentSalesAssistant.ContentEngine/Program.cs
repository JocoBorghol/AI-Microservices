using ISA.ContentEngine.ApiClients;
using ISA.ContentEngine.Middleware;
using ISA.ContentEngine.Models.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── Options Pattern: bind GeminiSettings from appsettings / user-secrets ──
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

// ── Typed Client + Polly resilience ──
builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(60); // Optimerad timeout för snabbare felhantering
}).AddStandardResilienceHandler(options =>
{
    // Optimerad Polly timeout för Gemini API
    options.AttemptTimeout = new()
    {
        Timeout = TimeSpan.FromSeconds(40)
    };
    // Total timeout 60 sekunder
    options.TotalRequestTimeout = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };
    // Circuit breaker sampling duration måste vara minst dubbelt så lång som attempt timeout
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(80); // 40s * 2 = 80s

    // Retry configuration för Gemini API
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(3);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<CustomExceptionMiddleware>();

// Justerad för att använda .NET 9 OpenAPI istället för SwaggerGen
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithOpenApiRoutePattern("/openapi/v1.json");
    });
}

// Justerad för att förhindra oändliga HTTPS-loopar i Azure Container Apps
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

app.Run();