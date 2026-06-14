using DrugCompare.Database;
using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Repositories;

public sealed class SqliteDrugRepository : IDrugRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteDrugRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DrugLookupResult?> FindDrugAsync(string drugName)
    {
        if (string.IsNullOrWhiteSpace(drugName) || drugName.Trim().Length < 2)
        {
            return null;
        }

        var query = drugName.Trim();
        var normalizedQuery = Normalize(query);

        const string sql = """
            SELECT
                d.id AS drug_id,
                d.name AS drug_name,
                a.id AS substance_id,
                a.name AS substance_name,
                a.normalized_name AS substance_normalized_name,
                a.ddinter_id,
                a.source AS substance_source
            FROM drugs d
            JOIN drug_active_substances das ON das.drug_id = d.id
            JOIN active_substances a ON a.id = das.active_substance_id
            
                WHERE
            LOWER(d.name) LIKE '%' || LOWER(@query) || '%'
            OR LOWER(d.normalized_name) LIKE '%' || LOWER(@query) || '%'
            OR LOWER(d.normalized_name) LIKE '%' || LOWER(@normalized_query) || '%'
            ORDER BY
                CASE
                    WHEN d.normalized_name = @normalized_query THEN 0
                    WHEN d.normalized_name LIKE @normalized_query || '%' THEN 1
                    WHEN d.name LIKE @query || '%' THEN 2
                    ELSE 3
                END,
                d.name,
                a.name
            LIMIT 20;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        // Debug for database SQLite
        System.Diagnostics.Debug.WriteLine($"SQLite DB: {connection.DataSource}");
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM drugs;";
            var count = await countCommand.ExecuteScalarAsync();

            System.Diagnostics.Debug.WriteLine($"SQLite DB: {connection.DataSource}");
            System.Diagnostics.Debug.WriteLine($"SQLite drugs count: {count}");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@query", query);
        command.Parameters.AddWithValue("@normalized_query", normalizedQuery);

        await using var reader = await command.ExecuteReaderAsync();

        string? foundDrugName = null;
        var substances = new List<ActiveSubstanceItem>();

        while (await reader.ReadAsync())
        {
            foundDrugName ??= GetString(reader, "drug_name");

            substances.Add(new ActiveSubstanceItem
            {
                DatabaseId = reader.GetInt64(reader.GetOrdinal("substance_id")),
                Name = GetString(reader, "substance_name"),
                NormalizedName = GetString(reader, "substance_normalized_name"),
                DDInterId = GetNullableString(reader, "ddinter_id"),
                Source = GetNullableString(reader, "substance_source") ?? "EMA + DDInter SQLite"
            });
        }

        if (foundDrugName is null || substances.Count == 0)
        {
            return null;
        }

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

    private static string GetString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string? GetNullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}