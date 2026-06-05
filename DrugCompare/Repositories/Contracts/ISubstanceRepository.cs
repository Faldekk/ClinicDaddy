using DrugCompare.Models;

namespace DrugCompare.Repositories.Contracts;

public interface ISubstanceRepository
{
    Task<ActiveSubstanceItem?> FindActiveSubstanceAsync(string substanceName);

    Task AddSynonymAsync(
        long activeSubstanceId,
        string synonym,
        string source = "manual");

    Task<List<ActiveSubstanceSynonymItem>> GetSynonymsAsync(long activeSubstanceId);
}