using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Infrastructure.SQLite;
using DrugCompare.Infrastructure.SQLite.KnowledgeBase;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class ChplBatchImportTests
{
    [TestMethod]
    public async Task Import_SkipsExistingDocuments_ReportsFailure_AndBacksUpFirst()
    {
        await WithDatabaseAsync(async factory =>
        {
            var backup = new FakeBackup();
            var importer = new FakeImporter(backup) { FailId = 2 };
            var service = new SqliteChplBatchImportService(factory, importer, backup);
            var result = await service.ImportAsync(10, null, CancellationToken.None);
            CollectionAssert.AreEqual(new long[] { 1, 2 }, importer.Ids.ToArray());
            Assert.AreEqual(1, result.Imported);
            Assert.AreEqual(2L, result.Failures.Single().ProductId);
            Assert.IsFalse(result.Cancelled);
            Assert.IsNotNull(result.BackupPath);
        });
    }

    [TestMethod]
    public async Task Retry_OnlyAttemptsRequestedFailedProducts()
    {
        await WithDatabaseAsync(async factory =>
        {
            var backup = new FakeBackup();
            var importer = new FakeImporter(backup);
            var result = await new SqliteChplBatchImportService(factory, importer, backup)
                .ImportAsync(10, null, CancellationToken.None, [2]);
            CollectionAssert.AreEqual(new long[] { 2 }, importer.Ids.ToArray());
            Assert.AreEqual(1, result.Imported);
        });
    }

    [TestMethod]
    public async Task Cancellation_PreservesCompletedCount_AndDoesNotImportNextProduct()
    {
        await WithDatabaseAsync(async factory =>
        {
            using var cancellation = new CancellationTokenSource();
            var backup = new FakeBackup();
            var importer = new FakeImporter(backup) { AfterImport = () => cancellation.Cancel() };
            var result = await new SqliteChplBatchImportService(factory, importer, backup)
                .ImportAsync(10, null, cancellation.Token);
            Assert.IsTrue(result.Cancelled);
            Assert.AreEqual(1, result.Imported);
            CollectionAssert.AreEqual(new long[] { 1 }, importer.Ids.ToArray());
        });
    }

    [TestMethod]
    public async Task NextBatch_ContinuesPastFailedProducts_InsteadOfRepeatingThem()
    {
        await WithDatabaseAsync(async factory =>
        {
            var backup = new FakeBackup();
            var importer = new FakeImporter(backup) { FailId = 1 };
            var service = new SqliteChplBatchImportService(factory, importer, backup);
            var first = await service.ImportAsync(1, null, CancellationToken.None);
            var second = await service.ImportAsync(1, null, CancellationToken.None);
            Assert.AreEqual(1, first.Failures.Count);
            Assert.AreEqual(1, second.Imported);
            CollectionAssert.AreEqual(new long[] { 1, 2 }, importer.Ids.ToArray());
        });
    }

    [TestMethod]
    public async Task BackupFailure_PreventsAnyImport()
    {
        await WithDatabaseAsync(async factory =>
        {
            var backup = new FakeBackup { Fail = true };
            var importer = new FakeImporter(backup);
            var service = new SqliteChplBatchImportService(factory, importer, backup);
            await Assert.ThrowsExceptionAsync<IOException>(() => service.ImportAsync(10, null, CancellationToken.None));
            Assert.AreEqual(0, importer.Ids.Count);
        });
    }

    private static async Task WithDatabaseAsync(Func<SqliteConnectionFactory, Task> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"chpl-batch-{Guid.NewGuid():N}.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE polish_drug_registry_items(id INTEGER PRIMARY KEY, product_name TEXT, active_substance_text TEXT, chpl_url TEXT);
                    CREATE TABLE chpl_documents(id INTEGER PRIMARY KEY, rpl_product_id INTEGER);
                    INSERT INTO polish_drug_registry_items VALUES (1,'Lek A','A','https://example.test/a.pdf'),
                      (2,'Lek B','B','https://example.test/b.pdf'), (3,'Lek C','C','https://example.test/c.pdf'), (4,'Bez linku','D',NULL);
                    INSERT INTO chpl_documents VALUES (1,3);
                    """;
                await command.ExecuteNonQueryAsync();
            }
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = $"Data Source={path}" }).Build();
            await action(new SqliteConnectionFactory(config));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class FakeBackup : ILocalDatabaseBackupService
    {
        public bool Created { get; private set; }
        public bool Fail { get; init; }
        public Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new IOException("Brak miejsca na kopię.");
            Created = true;
            return Task.CompletedTask;
        }
        public Task<string> RestoreAsync(string sourcePath, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeImporter(FakeBackup backup) : IChplProductImporter
    {
        public List<long> Ids { get; } = [];
        public long? FailId { get; init; }
        public Action? AfterImport { get; init; }
        public Task<KnowledgeBaseIngestionResult> ImportAsync(PolishDrugRegistryItem product, CancellationToken cancellationToken)
        {
            Assert.IsTrue(backup.Created, "Import must not start before backup.");
            Ids.Add(product.Id);
            if (product.Id == FailId) throw new InvalidOperationException("Nieprawidłowy PDF.");
            AfterImport?.Invoke();
            return Task.FromResult(new KnowledgeBaseIngestionResult { ChplDocumentId = product.Id });
        }
    }
}
