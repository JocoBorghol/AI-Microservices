using IntelligentSalesAssistantAPI.DTOs;

namespace IntelligentSalesAssistantAPI.Services.ContentDraft
{
    public interface IContentDraftService
    {
        Task<ContentDraftResponse> CreateDraftAsync(CreateContentDraftRequest request, CancellationToken ct);
        Task<ContentDraftListResponse> GetDraftsAsync(string? companyName = null);
        Task<string> GetDraftContentAsync(int id);
        Task DeleteDraftAsync(int id);
        Task<ContentDraftResponse> UpdateDraftAsync(int id, string content);
        Task<ContentDraftResponse> RestoreDraftAsync(int id);
    }
}
