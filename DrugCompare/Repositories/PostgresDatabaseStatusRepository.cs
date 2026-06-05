using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DrugCompare.Repositories;

public sealed class PostgresDatabaseStatusRepository : IDatabaseStatusRepository
{
    private readonly string _connectionString;

    public PostgresDatabaseStatusRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
    }

    public async Task<DatabaseStatusResult> GetDatabaseStatusAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM drugs) AS drugs_count,
                (SELECT COUNT(*) FROM active_substances) AS active_substances_count,
                (SELECT COUNT(*) FROM drug_active_substances) AS drug_active_substances_count,
                (SELECT COUNT(*) FROM substance_interactions) AS substance_interactions_count;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return new DatabaseStatusResult();

        return new DatabaseStatusResult
        {
            DrugsCount = reader.GetInt64(reader.GetOrdinal("drugs_count")),
            ActiveSubstancesCount = reader.GetInt64(reader.GetOrdinal("active_substances_count")),
            DrugActiveSubstancesCount = reader.GetInt64(reader.GetOrdinal("drug_active_substances_count")),
            SubstanceInteractionsCount = reader.GetInt64(reader.GetOrdinal("substance_interactions_count"))
        };
    }
}