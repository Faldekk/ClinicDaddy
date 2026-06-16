using DrugCompare.Application.Models;
using DrugCompare.Application.Repositories.Contracts;

namespace DrugCompare.Infrastructure.SQLite;

public sealed class SqliteIcdCodeRepository : IIcdCodeRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteIcdCodeRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<IcdCodeItem>> SearchAsync(string query, int limit = 50)
    {
        var result = new List<IcdCodeItem>();

        if (string.IsNullOrWhiteSpace(query))
            return result;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT
                id,
                code,
                title,
                description
            FROM icd_codes
            WHERE code LIKE @query
               OR title LIKE @query
               OR description LIKE @query
            ORDER BY code
            LIMIT @limit;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@query", $"%{query.Trim()}%");
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new IcdCodeItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                Code = reader.GetString(reader.GetOrdinal("code")),
                Title = reader.GetString(reader.GetOrdinal("title")),
                Description = reader.IsDBNull(reader.GetOrdinal("description"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("description"))
            });
        }

        return result;
    }
}