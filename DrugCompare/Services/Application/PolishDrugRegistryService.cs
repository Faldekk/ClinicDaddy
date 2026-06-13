using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using DrugCompare.Services.Contracts;

namespace DrugCompare.Services.Application;

public class PolishDrugRegistryService : IPolishDrugRegistryService
{
    private readonly IPolishDrugRegistryRepository _repository;

    public PolishDrugRegistryService(IPolishDrugRegistryRepository repository)
    {
        _repository = repository;
    }

    public Task<List<PolishDrugRegistryItem>> SearchAsync(string query, int limit = 50)
    {
        return _repository.SearchAsync(query, limit);
    }

    public Task<PolishDrugRegistryItem?> GetByIdAsync(long id)
    {
        return _repository.GetByIdAsync(id);
    }
}