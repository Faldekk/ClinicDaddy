using DrugCompare.Models;

namespace DrugCompare.Repositories.Contracts;

public interface IIcdCodeRepository
{
    Task<List<IcdCodeItem>> SearchAsync(
        string query,
        string? categoryFilter = null,
        int limit = 100);

    Task<IcdCodeItem?> GetByIdAsync(long id);
}