using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface IDrugLookupService
{
    Task<DrugLookupResult?> FindDrugAsync(string drugName);
}