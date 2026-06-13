using DrugCompare.Models;

namespace DrugCompare.Repositories.Contracts;

public interface IPolishDrugRegistryRepository
{
    Task<List<PolishDrugRegistryItem>> SearchAsync(string query, int limit = 50);
    Task<PolishDrugRegistryItem?> GetByIdAsync(long id);
}