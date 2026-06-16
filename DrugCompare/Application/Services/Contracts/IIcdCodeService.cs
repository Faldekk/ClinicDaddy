using DrugCompare.Application.Models;

namespace DrugCompare.Application.Services.Contracts;

public interface IIcdCodeService
{
    Task<List<IcdCodeItem>> SearchAsync(string query, int limit = 50);
}