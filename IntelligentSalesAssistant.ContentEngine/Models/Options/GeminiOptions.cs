namespace ISA.ContentEngine.Models.Options
{
    public class GeminiOptions
    {
        public const string SectionName = "GeminiSettings";

        public required string ApiKey { get; set; }

        public string GenerateContentUrl { get; set; }
            = "v1beta/models/gemini-3.1-flash-lite-preview:generateContent";
    }
}
