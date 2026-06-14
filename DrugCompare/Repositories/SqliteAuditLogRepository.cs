using DrugCompare.Database;
using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Repositories;

public class SqliteAuditLogRepository : IAuditLogRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteAuditLogRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task WriteAsync(string eventType, string? detailsJson = null)
    {
        
            const string sql = """
            INSERT INTO audit_logs (
                event_type,
                details_json,
                created_at
            )
            VALUES (
                @event_type,
                @details_json,
                datetime('now')
            );
            """;
           

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@event_type", eventType);
        command.Parameters.AddWithValue(
            "@details_json",
            string.IsNullOrWhiteSpace(detailsJson) ? DBNull.Value : detailsJson);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<AuditLogItem>> GetRecentAsync(int limit = 200)
    {
        var results = new List<AuditLogItem>();

        const string sql = """
            SELECT
                id,
                event_type,
                details_json,
                created_at
            FROM audit_logs
            ORDER BY id DESC
            LIMIT @limit;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new AuditLogItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                Action = GetString(reader, "event_type"),
                DetailsJson = GetNullableString(reader, "details_json"),
                CreatedAt = GetDateTime(reader, "created_at")
            });
        }

        return results;
    }

    private static string GetString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
    }

    private static string? GetNullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime GetDateTime(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);

        if (reader.IsDBNull(ordinal))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParse(reader.GetString(ordinal), out var value)
            ? value
            : DateTime.MinValue;
    }
}