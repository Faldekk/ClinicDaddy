using DrugCompare.Application.Models;
using DrugCompare.Application.Repositories.Contracts;
using DrugCompare.Application.Services.Contracts;

namespace DrugCompare.Application.Services.Implementations;

public sealed class IcdCodeService : IIcdCodeService
{
    private readonly IIcdCodeRepository _repository;

    public IcdCodeService(IIcdCodeRepository repository)
    {
        _repository = repository;
    }

    public Task<List<IcdCodeItem>> SearchAsync(string query, int limit = 50)
    {
        return _repository.SearchAsync(query, limit);
    }
}