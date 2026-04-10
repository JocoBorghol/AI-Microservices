using IntelligentSalesAssistantAPI.DTOs;

namespace IntelligentSalesAssistantAPI.Services.ContentDraft
{
    public interface IContentDraftService
    {
        Task<ContentDraftResponse> CreateDraftAsync(CreateContentDraftRequest request, CancellationToken ct);
        Task<ContentDraftListResponse> GetDraftsAsync(string? companyName = null);
        Task<string> GetDraftContentAsync(int id);
        Task DeleteDraftAsync(int id);
    }
}
