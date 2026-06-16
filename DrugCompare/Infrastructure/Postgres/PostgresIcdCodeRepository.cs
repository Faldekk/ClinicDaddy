using DrugCompare.Application.Models;
using DrugCompare.Application.Repositories.Contracts;

namespace DrugCompare.Infrastructure.Postgres;

public sealed class PostgresIcdCodeRepository : IIcdCodeRepository
{
    public Task<List<IcdCodeItem>> SearchAsync(string query, int limit = 50)
    {
        return Task.FromResult(new List<IcdCodeItem>());
    }
}