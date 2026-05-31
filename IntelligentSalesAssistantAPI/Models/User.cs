using System;
using System.ComponentModel.DataAnnotations;

namespace IntelligentSalesAssistantAPI.Models
{
    /// <summary>
    /// Representerar en användare i systemet med krypterat lösenord och rolltilldelning.
    /// </summary>
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "Seller"; // Standardroll för nyregistrerade användare

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
