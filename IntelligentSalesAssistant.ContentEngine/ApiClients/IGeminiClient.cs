namespace ISA.ContentEngine.ApiClients
{
    public interface IGeminiClient
    {
        Task<string> GenerateContentAsync(string prompt, CancellationToken ct = default);
    }
}
