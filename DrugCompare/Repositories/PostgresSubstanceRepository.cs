using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DrugCompare.Repositories;

public sealed class PostgresSubstanceRepository : ISubstanceRepository
{
    private readonly string _connectionString;

    public PostgresSubstanceRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
    }

    public async Task<ActiveSubstanceItem?> FindActiveSubstanceAsync(string substanceName)
    {
        if (string.IsNullOrWhiteSpace(substanceName))
            return null;

        var normalizedSearch = Normalize(substanceName);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                s.id,
                s.name,
                s.normalized_name,
                s.ddinter_id,
                s.source,
                0 AS match_priority,
                similarity(s.normalized_name, @search) AS score
            FROM active_substances s
            WHERE s.normalized_name = @search

            UNION ALL

            SELECT
                s.id,
                s.name,
                s.normalized_name,
                s.ddinter_id,
                s.source,
                1 AS match_priority,
                similarity(syn.normalized_synonym, @search) AS score
            FROM active_substance_synonyms syn
            JOIN active_substances s
                ON s.id = syn.active_substance_id
            WHERE syn.normalized_synonym = @search

            UNION ALL

            SELECT
                s.id,
                s.name,
                s.normalized_name,
                s.ddinter_id,
                s.source,
                2 AS match_priority,
                similarity(s.normalized_name, @search) AS score
            FROM active_substances s
            WHERE s.normalized_name ILIKE '%' || @search || '%'
               OR s.normalized_name % @search

            UNION ALL

            SELECT
                s.id,
                s.name,
                s.normalized_name,
                s.ddinter_id,
                s.source,
                3 AS match_priority,
                similarity(syn.normalized_synonym, @search) AS score
            FROM active_substance_synonyms syn
            JOIN active_substances s
                ON s.id = syn.active_substance_id
            WHERE syn.normalized_synonym ILIKE '%' || @search || '%'
               OR syn.normalized_synonym % @search

            ORDER BY match_priority, score DESC, name
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("search", normalizedSearch);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return new ActiveSubstanceItem
            {
                DatabaseId = null,
                Name = substanceName.Trim(),
                NormalizedName = normalizedSearch,
                DDInterId = null,
                Source = "Manual - not found in database"
            };
        }

        return new ActiveSubstanceItem
        {
            DatabaseId = reader.GetInt64(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            NormalizedName = reader.GetString(reader.GetOrdinal("normalized_name")),
            DDInterId = reader.IsDBNull(reader.GetOrdinal("ddinter_id"))
                ? null
                : reader.GetString(reader.GetOrdinal("ddinter_id")),
            Source = reader.IsDBNull(reader.GetOrdinal("source"))
                ? "PostgreSQL"
                : reader.GetString(reader.GetOrdinal("source"))
        };
    }

    public async Task AddSynonymAsync(
        long activeSubstanceId,
        string synonym,
        string source = "manual")
    {
        if (string.IsNullOrWhiteSpace(synonym))
            return;

        var normalizedSynonym = Normalize(synonym);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO active_substance_synonyms (
                active_substance_id,
                synonym,
                normalized_synonym,
                source
            )
            VALUES (
                @active_substance_id,
                @synonym,
                @normalized_synonym,
                @source
            )
            ON CONFLICT (normalized_synonym) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("active_substance_id", activeSubstanceId);
        command.Parameters.AddWithValue("synonym", synonym.Trim());
        command.Parameters.AddWithValue("normalized_synonym", normalizedSynonym);
        command.Parameters.AddWithValue("source", source);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ActiveSubstanceSynonymItem>> GetSynonymsAsync(long activeSubstanceId)
    {
        var items = new List<ActiveSubstanceSynonymItem>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                id,
                active_substance_id,
                synonym,
                normalized_synonym,
                source,
                created_at
            FROM active_substance_synonyms
            WHERE active_substance_id = @active_substance_id
            ORDER BY synonym;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("active_substance_id", activeSubstanceId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new ActiveSubstanceSynonymItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                ActiveSubstanceId = reader.GetInt64(reader.GetOrdinal("active_substance_id")),
                Synonym = reader.GetString(reader.GetOrdinal("synonym")),
                NormalizedSynonym = reader.GetString(reader.GetOrdinal("normalized_synonym")),
                Source = reader.GetString(reader.GetOrdinal("source")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
            });
        }

        return items;
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