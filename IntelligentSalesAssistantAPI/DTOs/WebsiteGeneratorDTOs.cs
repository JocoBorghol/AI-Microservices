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
        [StringLength(50)] string? Tone = null,
        
        // <example>familjer och barnfamiljer</example>
        [StringLength(50)] string? TargetAudience = null,
        
        // <example>["Godis", "Choklad", "Presentkort"]</example>
        [MaxLength(6)] List<string>? TopServices = null,
        
        // <example>["kvalitet", "tradition", "glädje"]</example>
        [MaxLength(10)] List<string>? Keywords = null
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
        DateTime? UpdatedAt
    );

    /// <summary>
    /// Response med lista av genererade hemsidor
    /// </summary>
    public record WebsiteListResponse(
        // <example>10</example>
        int TotalCount,
        
        List<WebsiteResponse> Websites
    );
}
