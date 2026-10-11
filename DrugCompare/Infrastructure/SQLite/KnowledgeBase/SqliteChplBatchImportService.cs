using System.IO;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;

namespace DrugCompare.Infrastructure.SQLite.KnowledgeBase;

public sealed class SqliteChplBatchImportService(
    SqliteConnectionFactory factory,
    IChplProductImporter importer,
    ILocalDatabaseBackupService backup) : IChplBatchImportService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Continue beyond failed URLs during this app session; retries use explicit IDs.
    private long _lastProcessedProductId;

    public async Task<ChplBatchImportResult> ImportAsync(int limit, IProgress<ChplImportProgress>? progress,
        CancellationToken cancellationToken, IReadOnlyCollection<long>? retryProductIds = null)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit), "Partia musi liczyć od 1 do 500 produktów.");
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Import ChPL jest już uruchomiony.");

        var imported = 0;
        var skipped = 0;
        var completed = 0;
        string? backupPath = null;
        var failures = new List<ChplImportFailure>();
        try
        {
            var products = await LoadProductsAsync(limit, retryProductIds, cancellationToken);
            progress?.Report(new(0, products.Count, 0, 0, 0, "Tworzenie kopii zapasowej"));
            if (products.Count == 0) return new(0, 0, false, null, failures);
            var backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicDaddy", "backups");
            var pendingBackupPath = Path.Combine(backupDirectory, $"medcompare-before-chpl-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
            await backup.CreateBackupAsync(pendingBackupPath, cancellationToken);
            backupPath = pendingBackupPath;

            foreach (var product in products)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new(completed, products.Count, imported, skipped, failures.Count, product.ProductName));
                try
                {
                    var result = await importer.ImportAsync(product, cancellationToken);
                    if (result.WasAlreadyImported) skipped++;
                    else imported++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    failures.Add(new(product.Id, product.ProductName, exception.Message));
                }
                completed++;
                if (retryProductIds is null) _lastProcessedProductId = product.Id;
                progress?.Report(new(completed, products.Count, imported, skipped, failures.Count, product.ProductName));
            }
            return new(imported, skipped, false, backupPath, failures);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(imported, skipped, true, backupPath, failures);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<PolishDrugRegistryItem>> LoadProductsAsync(int limit, IReadOnlyCollection<long>? retryIds, CancellationToken token)
    {
        var products = new List<PolishDrugRegistryItem>();
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        var filter = "";
        if (retryIds is not null)
        {
            var ids = retryIds.Distinct().Take(500).ToArray();
            if (ids.Length == 0) return products;
            filter = " AND p.id IN (" + string.Join(",", ids.Select((_, i) => "$id" + i)) + ")";
            for (var i = 0; i < ids.Length; i++) command.Parameters.AddWithValue("$id" + i, ids[i]);
        }
        command.CommandText = """
            SELECT p.id, p.product_name, p.active_substance_text, p.chpl_url
            FROM polish_drug_registry_items p
            WHERE TRIM(COALESCE(p.chpl_url, '')) <> ''
              AND NOT EXISTS (SELECT 1 FROM chpl_documents d WHERE d.rpl_product_id = p.id)
              AND p.id > $afterId
            """ + filter + " ORDER BY p.id LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$afterId", retryIds is null ? _lastProcessedProductId : 0L);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
            products.Add(new()
            {
                Id = reader.GetInt64(0),
                ProductName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ActiveSubstanceText = reader.IsDBNull(2) ? null : reader.GetString(2),
                ChplUrl = reader.GetString(3)
            });
        return products;
    }
}
