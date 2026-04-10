using System.ComponentModel.DataAnnotations;

namespace IntelligentSalesAssistantAPI.DTOs
{
    public class GenerateContentRequest
    {
        [Required]
        public string Prompt { get; set; } = string.Empty;
    }
}
