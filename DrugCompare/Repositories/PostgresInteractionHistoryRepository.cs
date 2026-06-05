using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;

namespace DrugCompare.Repositories;

public sealed class PostgresInteractionHistoryRepository : IInteractionHistoryRepository
{
    private readonly string _connectionString;

    public PostgresInteractionHistoryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");
    }

    public async Task SaveInteractionCheckAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances,
        IReadOnlyCollection<InteractionResult> results)
    {
        var acceptedSubstances = substances
            .Select(x => new
            {
                x.DatabaseId,
                x.Name,
                x.NormalizedName,
                x.DDInterId,
                x.Source
            })
            .ToList();

        var interactionResults = results
            .Select(x => new
            {
                x.SubstanceA,
                x.SubstanceB,
                x.Severity,
                x.Message,
                x.Source
            })
            .ToList();

        var highestSeverity = results.Count == 0
            ? null
            : results
                .OrderByDescending(x => GetSeverityScore(x.Severity))
                .First()
                .Severity;

        var substancesJson = JsonSerializer.Serialize(acceptedSubstances);
        var resultsJson = JsonSerializer.Serialize(interactionResults);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO interaction_check_history (
                accepted_substances_json,
                results_json,
                highest_severity,
                created_at
            )
            VALUES (
                @accepted_substances_json,
                @results_json,
                @highest_severity,
                now()
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("accepted_substances_json", substancesJson);
        command.Parameters.AddWithValue("results_json", resultsJson);
        command.Parameters.AddWithValue("highest_severity", (object?)highestSeverity ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<InteractionHistoryItem>> GetRecentHistoryAsync(int limit = 20)
    {
        var items = new List<InteractionHistoryItem>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                id,
                accepted_substances_json,
                results_json,
                highest_severity,
                created_at
            FROM interaction_check_history
            ORDER BY created_at DESC
            LIMIT @limit;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var substancesJson = reader.GetString(reader.GetOrdinal("accepted_substances_json"));
            var resultsJson = reader.GetString(reader.GetOrdinal("results_json"));

            items.Add(new InteractionHistoryItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                AcceptedSubstancesText = BuildSubstancesText(substancesJson),
                ResultsText = BuildResultsText(resultsJson),
                HighestSeverity = reader.IsDBNull(reader.GetOrdinal("highest_severity"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("highest_severity")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
            });
        }

        return items;
    }

    private static string BuildSubstancesText(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            var names = document.RootElement
                .EnumerateArray()
                .Select(x => x.GetProperty("Name").GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Join(", ", names);
        }
        catch
        {
            return "Could not parse substances.";
        }
    }

    private static string BuildResultsText(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            var results = document.RootElement
                .EnumerateArray()
                .Select(x =>
                {
                    var substanceA = x.GetProperty("SubstanceA").GetString();
                    var substanceB = x.GetProperty("SubstanceB").GetString();
                    var severity = x.GetProperty("Severity").GetString();

                    return $"{substanceA} + {substanceB}: {severity}";
                })
                .ToList();

            if (results.Count == 0)
                return "No known interactions found.";

            return string.Join(" | ", results);
        }
        catch
        {
            return "Could not parse results.";
        }
    }

    private static int GetSeverityScore(string severity)
    {
        return severity switch
        {
            "Contraindicated" => 4,
            "Major" => 3,
            "Moderate" => 2,
            "Minor" => 1,
            _ => 0
        };
    }
}