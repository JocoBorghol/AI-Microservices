using System;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.FileProviders;
using IntelligentSalesAssistantAPI.Http.Handlers;
using IntelligentSalesAssistantAPI.Http.Clients;
using IntelligentSalesAssistantAPI.Data;
using IntelligentSalesAssistantAPI.Middleware;
using IntelligentSalesAssistantAPI.Services;
using IntelligentSalesAssistantAPI.Services.Enrichment;
using IntelligentSalesAssistantAPI.Services.WebsiteGenerator;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Identity;
using IntelligentSalesAssistantAPI.Models;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    // Detta kopplar in dina XML-kommentarer dynamiskt (utan hårdkodade sökvägar)
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFilename);
    options.IncludeXmlComments(xmlPath);

    // Lägg till JWT Bearer
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Authorization: Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// --- 4. LLM Proxy Client (Service B) ---
builder.Services.AddHttpClient<LlmProxyClient>(client =>
{
    var proxyUrl = builder.Configuration["LlmProxySettings:BaseUrl"] ?? "http://localhost:5006";
    client.BaseAddress = new Uri(proxyUrl);
    client.Timeout = TimeSpan.FromSeconds(60); // Optimerad timeout för snabbare felhantering
})
.AddHttpMessageHandler<ApiKeyHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(80); // Dubbelt så lång som AttemptTimeout (40s * 2 = 80s)
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    options.AttemptTimeout = new HttpTimeoutStrategyOptions
    {
        Timeout = TimeSpan.FromSeconds(40) // Optimerad timeout för Gemini API
    };
    options.TotalRequestTimeout = new HttpTimeoutStrategyOptions
    {
        Timeout = TimeSpan.FromSeconds(60) // Total timeout 60 sekunder
    };
});




// --- 5. Rate Limiter (Begränsar antalet anrop per minut för att skydda API:et mot överbelastning) ---
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("FixedWindow", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });
});

// --- CORS-policy (Tillåter frontend att kommunicera med API:et från angivna domäner) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiPolicy", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000", 
            "https://localhost:3000", 
            "https://jocoborghol.se", 
            "https://www.jocoborghol.se", 
            "https://isa-frontend-three.vercel.app")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // Dev-policy för Demo UI (file:// skickar origin: null)
    options.AddPolicy("DevPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// --- JWT-autentisering och behörighetskontroll (skyddar API:et med tokens och roller) ---
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!))
        };
    });
builder.Services.AddAuthorization();

// --- 1. Databaskonfiguration (SQLite används som databas, konfigureras här) ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection saknas i konfigurationen.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// --- 2. Registrering av tjänster (Dependency Injection av alla applikationstjänster) ---
builder.Services.AddScoped<ICompanyRegistryService, CompanyRegistryService>();
builder.Services.AddScoped<ICompanyResearchService, CompanyResearchService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IWebsiteGeneratorService, WebsiteGeneratorService>();
builder.Services.AddScoped<IntelligentSalesAssistantAPI.Services.ContentDraft.IContentDraftService, IntelligentSalesAssistantAPI.Services.ContentDraft.ContentDraftService>();

// --- 3. HybridCache (Snabbar upp produktlistan och minskar belastning på databasen) ---
#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018

// --- 4. HTTP-klienter med Resilience-mönster och delegating handlers (för robusta externa API-anrop) ---
builder.Services.AddTransient<BolagsApiAuthHandler>();
builder.Services.AddTransient<ApiKeyHandler>();

builder.Services.AddHttpClient("CompanyApiClient", client =>
{
    client.BaseAddress = new Uri("https://api.bolagsapi.se/");
})
.AddHttpMessageHandler<BolagsApiAuthHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    options.AttemptTimeout = new HttpTimeoutStrategyOptions
    {
        Timeout = TimeSpan.FromSeconds(3)
    };
    options.TotalRequestTimeout = new HttpTimeoutStrategyOptions
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
});


var app = builder.Build();

// ==========================================
// Middleware-pipeline (ordningen är viktig: felhantering först, sedan säkerhet, routing och övrigt)
// ==========================================

// 1. Felhantering: fångar alla fel och returnerar standardiserade fel enligt RFC 7807 (ProblemDetails)
app.UseMiddleware<CustomExceptionMiddleware>();


// 2. Utvecklingsverktyg och testmiljö: Scalar UI aktiveras för både lokal- och produktionsmiljö
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger(); // Genererar openapi.json
    app.MapScalarApiReference(options =>
    {
        options.WithOpenApiRoutePattern("/swagger/v1/swagger.json");
    });
}

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// Servera genererade hemsidor som statiska filer
var projectRoot = Directory.GetCurrentDirectory(); // Redan i IntelligentSalesAssistantAPI-mappen
var generatedPath = Path.Combine(projectRoot, "Data", "generated");
generatedPath = Path.GetFullPath(generatedPath); // Normalisera sökvägen
Directory.CreateDirectory(generatedPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(generatedPath),
    RequestPath = "/generated"
});

// 3. Routing: styr inkommande HTTP-anrop till rätt controller
app.UseRouting();

// 4. CORS: tillåter anrop från frontend
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevPolicy");
}
else
{
    app.UseCors("ApiPolicy");
}

// 5. Rate Limiter: skyddar API:et mot överbelastning, placeras direkt efter CORS
app.UseRateLimiter();

// 6. Autentisering: kontrollerar JWT-token för skyddade endpoints
app.UseAuthentication();

// 7. Behörighetskontroll: ser till att användaren har rätt roll/rättigheter
app.UseAuthorization();

// 8. Kopplar controllers och aktiverar rate limiting på alla endpoints
app.MapControllers().RequireRateLimiting("FixedWindow");

// 9. Automatisk databasmigration och seeding av användare
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    // Kör eventuella utestående databasmigrationer automatiskt vid uppstart
    context.Database.Migrate();

    // Uppdatera befintliga rader som saknar ägare till "admin"
    var websitesToFix = context.CompanyWebsites.Where(w => w.CreatedBy == "" || w.CreatedBy == null).ToList();
    if (websitesToFix.Any())
    {
        foreach (var w in websitesToFix)
        {
            w.CreatedBy = "admin";
        }
        context.SaveChanges();
    }
    
    var hasher = new PasswordHasher<User>();
    
    // Seeda Admin-användare om konfiguration finns och användaren saknas
    var adminPassword = app.Configuration["AdminPassword"];
    if (!string.IsNullOrEmpty(adminPassword) && !context.Users.Any(u => u.Username == "admin"))
    {
        var adminUser = new User
        {
            Username = "admin",
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, adminPassword);
        context.Users.Add(adminUser);
    }

    // Seeda testsäljare (seller1) för bakåtkompatibilitet
    var sellerPassword = app.Configuration["SellerPassword"];
    if (!string.IsNullOrEmpty(sellerPassword) && !context.Users.Any(u => u.Username == "seller1"))
    {
        var sellerUser = new User
        {
            Username = "seller1",
            Role = "Seller",
            CreatedAt = DateTime.UtcNow
        };
        sellerUser.PasswordHash = hasher.HashPassword(sellerUser, sellerPassword);
        context.Users.Add(sellerUser);
    }

    context.SaveChanges();
}

// 10. Startar webbservern
app.Run();