using System.ComponentModel.DataAnnotations;

namespace IntelligentSalesAssistantAPI.Models
{
    /// <summary>
    /// Representerar en genererad företagshemsida
    /// </summary>
    public class CompanyWebsite
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(20)]
        public string OrgNumber { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        public string CompanyName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty; // Site/generated/{företag}/index.html
        
        [StringLength(100)]
        public string Category { get; set; } = "Övriga"; // Bransch från BolagsAPI
        
        [StringLength(50)]
        public string? Tone { get; set; } // "professionell", "vänlig", "modig", "personlig"
        
        [StringLength(50)]
        public string? TargetAudience { get; set; } // "privatpersoner", "företag", "båda"
        
        public string? TopServicesJson { get; set; } // JSON-array med tjänster
        
        public string? KeywordsJson { get; set; } // JSON-array med nyckelord
        
        [Required]
        public string GeneratedContentJson { get; set; } = string.Empty; // Hela WebsiteContentResponse
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ContentDraft> ContentDrafts { get; set; } = new List<ContentDraft>();
    }
}
