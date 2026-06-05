using DrugCompare.Models;

namespace DrugCompare.Repositories.Contracts;

public interface IInteractionHistoryRepository
{
    Task SaveInteractionCheckAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances,
        IReadOnlyCollection<InteractionResult> results);

    Task<List<InteractionHistoryItem>> GetRecentHistoryAsync(int limit = 20);
}