using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface IInteractionHistoryService
{
    Task SaveInteractionCheckAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances,
        IReadOnlyCollection<InteractionResult> results);

    Task<List<InteractionHistoryItem>> GetRecentHistoryAsync(int limit = 20);
}