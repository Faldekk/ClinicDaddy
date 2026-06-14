using DrugCompare.Database;
using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Data.Sqlite;

namespace DrugCompare.Repositories;

public sealed class SqliteInteractionRepository : IInteractionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteInteractionRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<InteractionResult>> CheckInteractionsAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances)
    {
        var items = substances
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Name) ||
                !string.IsNullOrWhiteSpace(x.NormalizedName) ||
                x.DatabaseId.HasValue)
            .GroupBy(x => x.DatabaseId?.ToString() ?? Normalize(x.NormalizedName.Length > 0 ? x.NormalizedName : x.Name))
            .Select(x => x.First())
            .ToList();

        if (items.Count < 2)
        {
            return new List<InteractionResult>();
        }

        var results = new List<InteractionResult>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        System.Diagnostics.Debug.WriteLine($"INTERACTION DB: {connection.DataSource}");
        System.Diagnostics.Debug.WriteLine(
            "INTERACTION INPUT: " +
            string.Join(", ", items.Select(x =>
                $"{x.Name}:{x.DatabaseId?.ToString() ?? "NULL"}:{x.DDInterId ?? "NO_DDINTER"}")));

        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                var first = items[i];
                var second = items[j];

                var byId = await FindInteractionByIdAsync(connection, first, second);

                if (byId is not null)
                {
                    results.Add(byId);
                    continue;
                }

                var byName = await FindInteractionByNormalizedNameAsync(connection, first, second);

                if (byName is not null)
                {
                    results.Add(byName);
                }
            }
        }

        System.Diagnostics.Debug.WriteLine($"INTERACTION RESULTS: {results.Count}");

        return results
            .OrderByDescending(x => GetSeverityScore(x.Severity))
            .ToList();
    }

    private static async Task<InteractionResult?> FindInteractionByIdAsync(
        SqliteConnection connection,
        ActiveSubstanceItem first,
        ActiveSubstanceItem second)
    {
        if (!first.DatabaseId.HasValue || !second.DatabaseId.HasValue)
        {
            return null;
        }

        const string sql = """
            SELECT
                si.id,
                si.severity,
                si.source,
                a.name AS substance_a,
                b.name AS substance_b
            FROM substance_interactions si
            JOIN active_substances a ON a.id = si.substance_a_id
            JOIN active_substances b ON b.id = si.substance_b_id
            WHERE
                (
                    si.substance_a_id = @first_id
                    AND si.substance_b_id = @second_id
                )
                OR
                (
                    si.substance_a_id = @second_id
                    AND si.substance_b_id = @first_id
                )
            LIMIT 1;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@first_id", first.DatabaseId.Value);
        command.Parameters.AddWithValue("@second_id", second.DatabaseId.Value);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return BuildResult(reader, "ID match");
    }

    private static async Task<InteractionResult?> FindInteractionByNormalizedNameAsync(
        SqliteConnection connection,
        ActiveSubstanceItem first,
        ActiveSubstanceItem second)
    {
        var firstName = Normalize(
            !string.IsNullOrWhiteSpace(first.NormalizedName)
                ? first.NormalizedName
                : first.Name);

        var secondName = Normalize(
            !string.IsNullOrWhiteSpace(second.NormalizedName)
                ? second.NormalizedName
                : second.Name);

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(secondName))
        {
            return null;
        }

        const string sql = """
            SELECT
                si.id,
                si.severity,
                si.source,
                a.name AS substance_a,
                b.name AS substance_b
            FROM substance_interactions si
            JOIN active_substances a ON a.id = si.substance_a_id
            JOIN active_substances b ON b.id = si.substance_b_id
            WHERE
                (
                    a.normalized_name = @first_name
                    AND b.normalized_name = @second_name
                )
                OR
                (
                    a.normalized_name = @second_name
                    AND b.normalized_name = @first_name
                )
            ORDER BY
                CASE
                    WHEN si.severity IN ('Contraindicated', 'X', 'x') THEN 5
                    WHEN si.severity IN ('Major', 'D', 'd') THEN 4
                    WHEN si.severity IN ('Moderate', 'C', 'c') THEN 3
                    WHEN si.severity IN ('Minor', 'B', 'b') THEN 2
                    WHEN si.severity IN ('A', 'a') THEN 1
                    ELSE 0
                END DESC
            LIMIT 1;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@first_name", firstName);
        command.Parameters.AddWithValue("@second_name", secondName);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return BuildResult(reader, "name fallback");
    }

    private static InteractionResult BuildResult(SqliteDataReader reader, string matchMode)
    {
        var substanceA = GetString(reader, "substance_a");
        var substanceB = GetString(reader, "substance_b");
        var severity = GetString(reader, "severity");
        var source = GetNullableString(reader, "source") ?? "Local DDInter SQLite database";

        return new InteractionResult
        {
            SubstanceA = substanceA,
            SubstanceB = substanceB,
            Severity = severity,
            Message = $"Interaction found between {substanceA} and {substanceB}. Severity: {severity}. Match: {matchMode}. Verify clinically.",
            Source = source
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

    private static int GetSeverityScore(string severity)
    {
        var value = severity.Trim().ToLowerInvariant();

        return value switch
        {
            "contraindicated" => 5,
            "major" => 4,
            "moderate" => 3,
            "minor" => 2,
            "unknown" => 1,
            "x" => 5,
            "d" => 4,
            "c" => 3,
            "b" => 2,
            "a" => 1,
            _ => 0
        };
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
}