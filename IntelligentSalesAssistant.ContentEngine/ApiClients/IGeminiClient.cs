namespace ISA.ContentEngine.ApiClients
{
    public interface IGeminiClient
    {
        Task<string> GenerateContentAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    }
}
