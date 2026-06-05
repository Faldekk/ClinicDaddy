using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface IDataManagementService
{
    Task<DataManagementStatusResult> GetDataManagementStatusAsync();
}