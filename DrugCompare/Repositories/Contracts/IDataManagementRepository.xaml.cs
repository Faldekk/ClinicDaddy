using DrugCompare.Models;

namespace DrugCompare.Repositories.Contracts;

public interface IDataManagementRepository
{
    Task<DataManagementStatusResult> GetDataManagementStatusAsync();
}