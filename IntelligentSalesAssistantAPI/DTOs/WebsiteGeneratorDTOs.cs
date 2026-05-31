using System.ComponentModel.DataAnnotations;

namespace IntelligentSalesAssistantAPI.DTOs
{
    /// <summary>
    /// Request för att generera en ny hemsida (använder cached enrichment data)
    /// </summary>
    public record GenerateWebsiteSimpleRequest(
        WebsiteCustomization? Customization = null
    );

    /// <summary>
    /// Enrichment data från BolagsAPI och Google Places (från /enrichment-preview endpoint)
    /// Används som extra context för att generera bättre hemsidor
    /// </summary>
    public record CompanyEnrichmentData(
        BolagInfo Bolag,
        GooglePlacesInfo? Google
    );

    /// <summary>
    /// Företagsinformation från BolagsAPI
    /// </summary>
    public record BolagInfo(
        // <example>5565093902</example>
        string OrgNumber,
        
        // <example>Kandy'z AB</example>
        string CompanyName,
        
        // <example>Storgatan 12</example>
        string? Address,
        
        // <example>Stockholm</example>
        string? City,
        
        // <example>11122</example>
        string? PostCode,
        
        // <example>https://kandyz.se</example>
        string? Website,
        
        // <example>Anna Andersson</example>
        string? ContactPerson,
        
        // <example>08-123 45 67</example>
        string? Phone,
        
        // <example>info@kandyz.se</example>
        string? Email,
        
        // <example>Detaljhandel med godis</example>
        string? Industry
    );

    /// <summary>
    /// Google Places information med candidates
    /// </summary>
    public record GooglePlacesInfo(
        List<GooglePlaceCandidate>? Candidates,
        string? Status
    );

    /// <summary>
    /// En Google Places candidate med place_id för rating-lookup
    /// </summary>
    public record GooglePlaceCandidate(
        string? Name,
        string? FormattedAddress,
        string? PlaceId
    );

    /// <summary>
    /// Anpassningar för hemsidegenerering (alla fält är valfria)
    /// </summary>
    public record WebsiteCustomization(
        // <example>professionell och välkomnande</example>
        [StringLength(200)] string? Tone = null,
        
        // <example>familjer och barnfamiljer</example>
        [StringLength(200)] string? TargetAudience = null,
        
        // <example>["Godis", "Choklad", "Presentkort"]</example>
        [MaxLength(6)] List<string>? TopServices = null,
        
        // <example>["kvalitet", "tradition", "glädje"]</example>
        [MaxLength(10)] List<string>? Keywords = null
    );

    /// <summary>
    /// Request för att byta CSS-tema på en befintlig hemsida (utan regenerering)
    /// </summary>
    public record ApplyThemeRequest(
        // <example>ocean</example>
        [Required]
        [StringLength(50)]
        string Theme
    );

    /// <summary>
    /// Request för att uppdatera/regenerera en hemsida
    /// </summary>
    public record UpdateWebsiteRequest(
        // <example>5565093902</example>
        [Required]
        [RegularExpression(@"^\d{6}-?\d{4}$")]
        string OrgNumber,
        
        WebsiteCustomization? Customization = null
    );

    /// <summary>
    /// Response med information om en genererad hemsida
    /// </summary>
    public record WebsiteResponse(
        // <example>1</example>
        int Id,
        
        // <example>Kandy'z AB</example>
        string CompanyName,
        
        // <example>5565093902</example>
        string OrgNumber,
        
        // <example>/generated/kandyz-ab/index.html</example>
        string WebsiteUrl,
        
        // <example>Detaljhandel med godis</example>
        string Category,
        
        // <example>professionell och välkomnande</example>
        string? Tone,
        
        // <example>familjer och barnfamiljer</example>
        string? TargetAudience,
        
        // <example>2026-04-09T08:00:00Z</example>
        DateTime CreatedAt,
        
        // <example>2026-04-09T10:30:00Z</example>
        DateTime? UpdatedAt,
        
        // <example>ocean</example>
        string Theme = "original",
        
        // <example>{}</example>
        string? GeneratedContentJson = null
    );

    /// <summary>
    /// Response med lista av genererade hemsidor
    /// </summary>
    public record WebsiteListResponse(
        // <example>10</example>
        int TotalCount,
        
        List<WebsiteResponse> Websites
    );

    /// <summary>
    /// Request för att uppdatera kontaktuppgifter direkt i HTML utan regenerering.
    /// Alla fält är valfria — skicka bara de du vill ändra.
    /// </summary>
    public record UpdateContactRequest(
        string? Phone = null,
        string? Email = null,
        string? Address = null,
        string? FacebookUrl = null,
        string? InstagramUrl = null
    );

    /// <summary>
    /// Request för att uppdatera textinnehåll direkt i HTML utan regenerering.
    /// Alla fält är valfria — skicka bara de du vill ändra.
    /// </summary>
    public record UpdateContentRequest(
        // Hero-sektion
        string? HeroTitle = null,
        string? HeroText = null,
        string? CtaPrimary = null,
        string? CtaSecondary = null,

        // Trust band (tre korta förtroendefraser)
        string? Trust1 = null,
        string? Trust2 = null,
        string? Trust3 = null,

        // Om oss
        string? AboutSubtitle = null,
        string? AboutTitle = null,
        string? AboutParagraph1 = null,
        string? AboutParagraph2 = null,
        string? AboutParagraph3 = null,
        string? AboutCta = null,
        string? OwnerName = null,
        string? OwnerTitle = null,

        // Tagline (visas i footer och title)
        string? Tagline = null,

        // Tjänster (titel + beskrivning per tjänst)
        string? Service1Title = null,
        string? Service1Description = null,
        string? Service2Title = null,
        string? Service2Description = null,
        string? Service3Title = null,
        string? Service3Description = null,

        // FAQ (fråga + svar per FAQ)
        string? Faq1Question = null,
        string? Faq1Answer = null,
        string? Faq2Question = null,
        string? Faq2Answer = null,
        string? Faq3Question = null,
        string? Faq3Answer = null
    );
}