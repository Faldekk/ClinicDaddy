using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using DrugCompare.Services.Contracts;

namespace DrugCompare.Services.Application;

public class IcdCodeService : IIcdCodeService
{
    private readonly IIcdCodeRepository _repository;

    public IcdCodeService(IIcdCodeRepository repository)
    {
        _repository = repository;
    }

    public Task<List<IcdCodeItem>> SearchAsync(
        string query,
        string? categoryFilter = null,
        int limit = 100)
    {
        return _repository.SearchAsync(query, categoryFilter, limit);
    }

    public Task<IcdCodeItem?> GetByIdAsync(long id)
    {
        return _repository.GetByIdAsync(id);
    }
}