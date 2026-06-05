using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface ISubstanceSynonymService
{
    Task AddSynonymAsync(
        long activeSubstanceId,
        string synonym,
        string source = "manual");

    Task<List<ActiveSubstanceSynonymItem>> GetSynonymsAsync(long activeSubstanceId);
}