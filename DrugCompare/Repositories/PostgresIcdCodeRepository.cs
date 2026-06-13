using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DrugCompare.Repositories;

public class PostgresIcdCodeRepository : IIcdCodeRepository
{
    private readonly string _connectionString;

    public PostgresIcdCodeRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing connection string: DefaultConnection");
    }

    public async Task<List<IcdCodeItem>> SearchAsync(
        string query,
        string? categoryFilter = null,
        int limit = 100)
    {
        var results = new List<IcdCodeItem>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        var normalizedQuery = NormalizeCode(query);

        const string sql = """
            SELECT
                id,
                code,
                normalized_code,
                title,
                normalized_title,
                description,
                chapter,
                parent_code,
                source,
                version,
                imported_at
            FROM icd_codes
            WHERE
                (
                    normalized_code ILIKE '%' || @normalized_query || '%'
                    OR normalized_title ILIKE '%' || lower(@query) || '%'
                    OR lower(COALESCE(description, '')) ILIKE '%' || lower(@query) || '%'
                )
                AND
                (
                    @category_filter IS NULL
                    OR @category_filter = ''
                    OR chapter = @category_filter
                )
            ORDER BY
                CASE
                    WHEN normalized_code = @normalized_query THEN 0
                    WHEN normalized_code LIKE @normalized_query || '%' THEN 1
                    WHEN normalized_title LIKE lower(@query) || '%' THEN 2
                    ELSE 3
                END,
                code
            LIMIT @limit;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("query", query.Trim());
        command.Parameters.AddWithValue("normalized_query", normalizedQuery);

        command.Parameters.AddWithValue(
            "category_filter",
            string.IsNullOrWhiteSpace(categoryFilter)
                ? DBNull.Value
                : categoryFilter.Trim());

        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(Map(reader));
        }

        return results;
    }

    public async Task<IcdCodeItem?> GetByIdAsync(long id)
    {
        const string sql = """
            SELECT
                id,
                code,
                normalized_code,
                title,
                normalized_title,
                description,
                chapter,
                parent_code,
                source,
                version,
                imported_at
            FROM icd_codes
            WHERE id = @id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return Map(reader);
        }

        return null;
    }

    private static IcdCodeItem Map(NpgsqlDataReader reader)
    {
        return new IcdCodeItem
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            Code = reader.GetString(reader.GetOrdinal("code")),
            NormalizedCode = reader.GetString(reader.GetOrdinal("normalized_code")),

            Title = reader.GetString(reader.GetOrdinal("title")),
            NormalizedTitle = reader.GetString(reader.GetOrdinal("normalized_title")),

            Description = reader["description"] as string,
            Chapter = reader["chapter"] as string,
            ParentCode = reader["parent_code"] as string,

            Source = reader["source"] as string ?? "ICD",
            Version = reader["version"] as string,

            ImportedAt = reader.GetDateTime(reader.GetOrdinal("imported_at"))
        };
    }

    private static string NormalizeCode(string value)
    {
        return value
            .Trim()
            .Replace(".", "")
            .Replace("-", "")
            .ToUpperInvariant();
    }
}