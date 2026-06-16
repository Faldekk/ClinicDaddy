using DrugCompare.Application.Models;

namespace DrugCompare.Application.Services.Contracts;

public interface IPolishDrugRegistryService
{
    Task<List<PolishDrugRegistryItem>> SearchAsync(string query, int limit = 50);
}