using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface IInteractionCheckerService
{
    Task<List<InteractionResult>> CheckInteractionsAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances);
}