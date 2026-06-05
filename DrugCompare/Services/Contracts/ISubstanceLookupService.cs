using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface ISubstanceLookupService
{
    Task<ActiveSubstanceItem?> FindActiveSubstanceAsync(string substanceName);
}