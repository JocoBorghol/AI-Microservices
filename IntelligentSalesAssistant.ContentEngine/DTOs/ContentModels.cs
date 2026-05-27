namespace ISA.ContentEngine.DTOs
{
    /// <summary>
    /// Request för generering av AI-innehåll
    /// </summary>
    public record ContentRequest(
        // <example>Skriv en välkomnande text för en godisbutik i Stockholm</example>
        string Prompt,
        // <example>ServiceA</example>
        string ClientId
    );
    
    /// <summary>
    /// Response med AI-genererat innehåll
    /// </summary>
    public record ContentResponse(
        // <example>Välkommen till vår mysiga godisbutik i hjärtat av Stockholm!</example>
        string Reply
    );
}
