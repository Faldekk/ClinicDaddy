using DrugCompare.Models;

namespace DrugCompare.Repositories.Contracts;

public interface IDatabaseStatusRepository
{
    Task<DatabaseStatusResult> GetDatabaseStatusAsync();
}