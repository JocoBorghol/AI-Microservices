namespace ISA.ContentEngine.DTOs
{
    /// <summary>
    /// Request för generering av AI-innehåll
    /// </summary>
    public record ContentRequest(
        // <example>Du är en expert på att skriva marknadsföringsinnehåll på svenska...</example>
        string SystemPrompt,
        // <example>Skapa ett facebook_post med följande specifikationer...</example>
        string UserPrompt,
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
