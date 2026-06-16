using DrugCompare.Application.Models;
using DrugCompare.Application.Repositories.Contracts;
using DrugCompare.Application.Services.Contracts;

namespace DrugCompare.Application.Services.Implementations;

public sealed class PolishDrugRegistryService : IPolishDrugRegistryService
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
}