using DrugCompare.Models;

namespace DrugCompare.Repositories.Contracts;

public interface IInteractionRepository
{
    Task<List<InteractionResult>> CheckInteractionsAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances);
}