using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface IPolishDrugRegistryService
{
    Task<List<PolishDrugRegistryItem>> SearchAsync(string query, int limit = 50);
    Task<PolishDrugRegistryItem?> GetByIdAsync(long id);
}