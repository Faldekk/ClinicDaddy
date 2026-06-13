using DrugCompare.Models;
using DrugCompare.Repositories.Contracts;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DrugCompare.Repositories;

public class PostgresPolishDrugRegistryRepository : IPolishDrugRegistryRepository
{
    private readonly string _connectionString;

    public PostgresPolishDrugRegistryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing connection string: DefaultConnection");
    }

    public async Task<List<PolishDrugRegistryItem>> SearchAsync(string query, int limit = 50)
    {
        var results = new List<PolishDrugRegistryItem>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        const string sql = """
            SELECT
                id,
                rpl_id,
                product_name,
                normalized_product_name,
                active_substance_text,
                strength,
                pharmaceutical_form,
                marketing_authorization_holder,
                authorization_number,
                authorization_validity,
                product_type,
                procedure_type,
                chpl_url,
                leaflet_url,
                source,
                source_version,
                imported_at
            FROM polish_drug_registry_items
            WHERE
                normalized_product_name ILIKE '%' || lower(@query) || '%'
                OR product_name ILIKE '%' || @query || '%'
                OR lower(active_substance_text) ILIKE '%' || lower(@query) || '%'
                OR authorization_number ILIKE '%' || @query || '%'
            ORDER BY
                CASE
                    WHEN lower(product_name) = lower(@query) THEN 0
                    WHEN normalized_product_name = lower(@query) THEN 1
                    WHEN normalized_product_name LIKE lower(@query) || '%' THEN 2
                    ELSE 3
                END,
                product_name
            LIMIT @limit;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("query", query.Trim());
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(Map(reader));
        }

        return results;
    }

    public async Task<PolishDrugRegistryItem?> GetByIdAsync(long id)
    {
        const string sql = """
            SELECT
                id,
                rpl_id,
                product_name,
                normalized_product_name,
                active_substance_text,
                strength,
                pharmaceutical_form,
                marketing_authorization_holder,
                authorization_number,
                authorization_validity,
                product_type,
                procedure_type,
                chpl_url,
                leaflet_url,
                source,
                source_version,
                imported_at
            FROM polish_drug_registry_items
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

    private static PolishDrugRegistryItem Map(NpgsqlDataReader reader)
    {
        return new PolishDrugRegistryItem
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            RplId = reader["rpl_id"] as string,
            ProductName = reader.GetString(reader.GetOrdinal("product_name")),
            NormalizedProductName = reader.GetString(reader.GetOrdinal("normalized_product_name")),
            ActiveSubstanceText = reader["active_substance_text"] as string,
            Strength = reader["strength"] as string,
            PharmaceuticalForm = reader["pharmaceutical_form"] as string,
            MarketingAuthorizationHolder = reader["marketing_authorization_holder"] as string,
            AuthorizationNumber = reader["authorization_number"] as string,
            AuthorizationValidity = reader["authorization_validity"] as string,
            ProductType = reader["product_type"] as string,
            ProcedureType = reader["procedure_type"] as string,
            ChplUrl = reader["chpl_url"] as string,
            LeafletUrl = reader["leaflet_url"] as string,
            Source = reader["source"] as string ?? "RPL",
            SourceVersion = reader["source_version"] as string,
            ImportedAt = reader.GetDateTime(reader.GetOrdinal("imported_at"))
        };
    }
}