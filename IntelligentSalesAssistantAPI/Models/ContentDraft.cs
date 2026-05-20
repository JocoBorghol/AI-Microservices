using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IntelligentSalesAssistantAPI.Models
{
    /// <summary>
    /// Representerar ett AI-genererat innehållsutkast
    /// </summary>
    public class ContentDraft
    {
        [Key]
        public int Id { get; set; }

        public int? WebsiteId { get; set; }

        [ForeignKey(nameof(WebsiteId))]
        public CompanyWebsite? Website { get; set; }

        [Required]
        [StringLength(50)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Instructions { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Purpose { get; set; }

        [StringLength(50)]
        public string? TargetAudience { get; set; }

        [StringLength(50)]
        public string? Tone { get; set; }

        [StringLength(50)]
        public string? Length { get; set; }

        [Required]
        [StringLength(500)]
        public string OriginalContentPath { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ModifiedContentPath { get; set; }

        [StringLength(200)]
        public string? CompanyName { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
