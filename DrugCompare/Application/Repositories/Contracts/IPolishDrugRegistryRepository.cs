using DrugCompare.Application.Models;

namespace DrugCompare.Application.Repositories.Contracts;

public interface IPolishDrugRegistryRepository
{
    Task<List<PolishDrugRegistryItem>> SearchAsync(string query, int limit = 50);
}