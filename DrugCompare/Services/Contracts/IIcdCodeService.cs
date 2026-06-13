using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface IIcdCodeService
{
    Task<List<IcdCodeItem>> SearchAsync(
        string query,
        string? categoryFilter = null,
        int limit = 100);

    Task<IcdCodeItem?> GetByIdAsync(long id);
}