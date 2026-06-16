using DrugCompare.Application.Models;

namespace DrugCompare.Application.Repositories.Contracts;

public interface IIcdCodeRepository
{
    Task<List<IcdCodeItem>> SearchAsync(string query, int limit = 50);
}