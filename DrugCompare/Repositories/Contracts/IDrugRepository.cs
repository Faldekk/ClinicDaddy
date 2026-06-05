using DrugCompare.Models;

namespace DrugCompare.Repositories.Contracts;

public interface IDrugRepository
{
    Task<DrugLookupResult?> FindDrugAsync(string drugName);
}