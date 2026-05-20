using System.ComponentModel.DataAnnotations;

namespace IntelligentSalesAssistantAPI.DTOs
{
    /// <summary>
    /// Request för att skapa ett innehållsutkast (draft)
    /// </summary>
    public record CreateContentDraftRequest(
        /// <example>facebook_post</example>
        [Required(ErrorMessage = "ContentType är obligatoriskt")]
        [StringLength(50, ErrorMessage = "ContentType får max vara 50 tecken")]
        string ContentType,

        /// <example>Skapa ett roligt inlägg om våra sommaröppettider</example>
        [Required(ErrorMessage = "Instructions är obligatoriskt")]
        [StringLength(500, ErrorMessage = "Instructions får max vara 500 tecken")]
        string Instructions,

        /// <example>Info</example>
        [StringLength(50)] string? Purpose = null,
        
        /// <example>gäster</example>
        [StringLength(50)] string? TargetAudience = null,
        
        /// <example>lättsam</example>
        [StringLength(20)] string? Tone = null,
        
        /// <example>kort</example>
        [StringLength(20)] string? Length = null,
        
        /// <example>11</example>
        int? WebsiteId = null,  // Använd specifik hemsida via ID
        
        /// <example>true</example>
        bool UseLatestWebsite = false  // Eller använd senaste hemsidan
    );

    /// <summary>
    /// Response med det genererade innehållsutkastet
    /// </summary>
    public record ContentDraftResponse(
        /// <example>1</example>
        int Id,
        
        /// <example>🌞 Sommarens öppettider är här! Vi håller öppet alla dagar 10-18. Välkomna!</example>
        string Content,
        
        /// <example>kandyz-ab/facebook_post-2026-04-09-080000.txt</example>
        string FilePath,
        
        /// <example>facebook_post</example>
        string ContentType,
        
        /// <example>Kandy'z AB</example>
        string? CompanyName,
        
        /// <example>2026-04-09T08:00:00Z</example>
        DateTime CreatedAt,

        /// <example>kandyz-ab/facebook_post-2026-04-09-080000-original.txt</example>
        string OriginalContentPath,

        /// <example>kandyz-ab/facebook_post-2026-04-09-080000-modified.txt</example>
        string? ModifiedContentPath
    );

    /// <summary>
    /// Lista med sparade utkast
    /// </summary>
    public record ContentDraftListResponse(
        /// <example>5</example>
        int TotalCount,
        
        List<ContentDraftInfo> Drafts
    );

    /// <summary>
    /// Information om ett sparat utkast
    /// </summary>
    public record ContentDraftInfo(
        /// <example>1</example>
        int Id,
        
        /// <example>facebook_post-2026-04-09-080000.txt</example>
        string FileName,
        
        /// <example>kandyz-ab/facebook_post-2026-04-09-080000.txt</example>
        string FilePath,
        
        /// <example>facebook_post</example>
        string ContentType,
        
        /// <example>Kandy'z AB</example>
        string? CompanyName,
        
        /// <example>2026-04-09T08:00:00Z</example>
        DateTime CreatedAt,
        
        /// <example>1024</example>
        long FileSizeBytes,

        /// <example>kandyz-ab/facebook_post-2026-04-09-080000-original.txt</example>
        string OriginalContentPath,

        /// <example>kandyz-ab/facebook_post-2026-04-09-080000-modified.txt</example>
        string? ModifiedContentPath
    );

    /// <summary>
    /// Request för att uppdatera ett innehållsutkast manuellt
    /// </summary>
    public record UpdateContentDraftRequest(
        [Required(ErrorMessage = "Content är obligatoriskt")]
        string Content
    );
}
