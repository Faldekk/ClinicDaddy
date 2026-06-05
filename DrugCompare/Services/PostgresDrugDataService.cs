using DrugCompare.Models;
using Microsoft.Extensions.Configuration;
using DrugCompare.Services.Contracts;
using Npgsql;
using System.Text.Json;

namespace DrugCompare.Services;

public sealed class PostgresDrugDataService :
    IDrugLookupService,
    ISubstanceLookupService,
    IInteractionCheckerService,
    IDatabaseStatusService,
    IInteractionHistoryService,
    ISubstanceSynonymService
{
    private readonly string _connectionString;

    public PostgresDrugDataService(IConfiguration configuration)
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

        if (await reader.ReadAsync())
        {
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

        return new ActiveSubstanceItem
        {
            DatabaseId = null,
            Name = substanceName.Trim(),
            NormalizedName = normalizedSearch,
            DDInterId = null,
            Source = "Manual - not found in database"
        };
    }

    public async Task<List<InteractionResult>> CheckInteractionsAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances)
    {
        var items = substances
            .Where(x => x.DatabaseId.HasValue)
            .GroupBy(x => x.DatabaseId!.Value)
            .Select(x => x.First())
            .ToList();

        if (items.Count < 2)
            return new List<InteractionResult>();

        var results = new List<InteractionResult>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                sa.name AS substance_a,
                sb.name AS substance_b,
                si.severity,
                si.source
            FROM substance_interactions si
            JOIN active_substances sa
                ON sa.id = si.substance_a_id
            JOIN active_substances sb
                ON sb.id = si.substance_b_id
            WHERE si.substance_a_id = @first_id
              AND si.substance_b_id = @second_id
            LIMIT 1;
            """;

        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                var id1 = items[i].DatabaseId!.Value;
                var id2 = items[j].DatabaseId!.Value;

                var firstId = Math.Min(id1, id2);
                var secondId = Math.Max(id1, id2);

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("first_id", firstId);
                command.Parameters.AddWithValue("second_id", secondId);

                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    continue;

                var severity = reader.GetString(reader.GetOrdinal("severity"));

                results.Add(new InteractionResult
                {
                    SubstanceA = reader.GetString(reader.GetOrdinal("substance_a")),
                    SubstanceB = reader.GetString(reader.GetOrdinal("substance_b")),
                    Severity = severity,
                    Message = BuildMessage(severity),
                    Source = reader.IsDBNull(reader.GetOrdinal("source"))
                        ? "Local DDInter-based database"
                        : reader.GetString(reader.GetOrdinal("source"))
                });
            }
        }

        return results
            .OrderByDescending(x => GetSeverityScore(x.Severity))
            .ToList();
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
    private static string BuildMessage(string severity)
    {
        return severity switch
        {
            "Contraindicated" =>
                "Interaction found. Physician should verify this combination before use.",

            "Major" =>
                "Major interaction found. Physician should verify this interaction clinically.",

            "Moderate" =>
                "Moderate interaction found. Clinical verification is recommended.",

            "Minor" =>
                "Minor interaction found. Review if clinically relevant.",

            _ =>
                "Interaction found in local DDInter-based database. Verify clinically."
        };
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

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ");
    }
}

