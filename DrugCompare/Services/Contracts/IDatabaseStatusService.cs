using DrugCompare.Models;

namespace DrugCompare.Services.Contracts;

public interface IDatabaseStatusService
{
    Task<DatabaseStatusResult> GetDatabaseStatusAsync();
}