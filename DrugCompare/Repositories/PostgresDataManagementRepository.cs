using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using DrugCompare.Services.Contracts;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DrugCompare.Repositories;

public sealed class PostgresDataManagementRepository : IDataManagementRepository
{
    private readonly string _connectionString;

    public PostgresDataManagementRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
    }

    public async Task<DataManagementStatusResult> GetDataManagementStatusAsync()
    {
        var recentImports = await GetRecentImportsAsync();

        return new DataManagementStatusResult
        {
            LatestEmaImport = recentImports
                .Where(x => x.SourceName.Equals("EMA", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.ImportedAt)
                .FirstOrDefault(),

            LatestDdinterImport = recentImports
                .Where(x => x.SourceName.Equals("DDInter", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.ImportedAt)
                .FirstOrDefault(),

            RecentImports = recentImports
        };
    }

    private async Task<List<DataSourceVersionItem>> GetRecentImportsAsync()
    {
        var items = new List<DataSourceVersionItem>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                id,
                source_name,
                file_name,
                imported_at,
                records_imported,
                notes,
                source_url,
                checksum,
                COALESCE(import_status, 'Unknown') AS import_status,
                error_message
            FROM data_source_versions
            ORDER BY imported_at DESC
            LIMIT 50;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(new DataSourceVersionItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                SourceName = reader.GetString(reader.GetOrdinal("source_name")),
                FileName = reader.GetString(reader.GetOrdinal("file_name")),
                ImportedAt = reader.GetDateTime(reader.GetOrdinal("imported_at")),
                RecordsImported = reader.GetInt32(reader.GetOrdinal("records_imported")),
                Notes = reader.IsDBNull(reader.GetOrdinal("notes"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("notes")),
                SourceUrl = reader.IsDBNull(reader.GetOrdinal("source_url"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("source_url")),
                Checksum = reader.IsDBNull(reader.GetOrdinal("checksum"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("checksum")),
                ImportStatus = reader.GetString(reader.GetOrdinal("import_status")),
                ErrorMessage = reader.IsDBNull(reader.GetOrdinal("error_message"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("error_message"))
            });
        }

        return items;
    }
}