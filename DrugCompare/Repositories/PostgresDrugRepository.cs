using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DrugCompare.Repositories;

public sealed class PostgresDrugRepository : IDrugRepository
{
    private readonly string _connectionString;

    public PostgresDrugRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
    }

    public async Task<DrugLookupResult?> FindDrugAsync(string drugName)
    {
        if (string.IsNullOrWhiteSpace(drugName))
            return null;

        var normalizedSearch = Normalize(drugName);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                d.name AS drug_name,
                d.manufacturer,
                s.id AS substance_id,
                s.name AS substance_name,
                s.normalized_name,
                s.ddinter_id,
                s.source,
                similarity(d.normalized_name, @search) AS score
            FROM drugs d
            JOIN drug_active_substances das
                ON das.drug_id = d.id
            JOIN active_substances s
                ON s.id = das.active_substance_id
            WHERE d.normalized_name ILIKE '%' || @search || '%'
               OR d.normalized_name % @search
            ORDER BY score DESC, d.name, s.name
            LIMIT 50;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("search", normalizedSearch);

        await using var reader = await command.ExecuteReaderAsync();

        var substances = new List<ActiveSubstanceItem>();
        string? foundDrugName = null;

        while (await reader.ReadAsync())
        {
            foundDrugName ??= reader.GetString(reader.GetOrdinal("drug_name"));

            substances.Add(new ActiveSubstanceItem
            {
                DatabaseId = reader.GetInt64(reader.GetOrdinal("substance_id")),
                Name = reader.GetString(reader.GetOrdinal("substance_name")),
                NormalizedName = reader.GetString(reader.GetOrdinal("normalized_name")),
                DDInterId = reader.IsDBNull(reader.GetOrdinal("ddinter_id"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("ddinter_id")),
                Source = reader.IsDBNull(reader.GetOrdinal("source"))
                    ? "PostgreSQL"
                    : reader.GetString(reader.GetOrdinal("source"))
            });
        }

        if (foundDrugName is null || substances.Count == 0)
            return null;

        return new DrugLookupResult
        {
            DrugName = foundDrugName,
            ActiveSubstances = substances
                .GroupBy(x => x.DatabaseId)
                .Select(x => x.First())
                .ToList()
        };
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ");
    }
}