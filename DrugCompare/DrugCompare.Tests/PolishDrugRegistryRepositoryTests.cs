using DrugCompare.Infrastructure.SQLite;
using DrugCompare.Infrastructure.SQLite.KnowledgeBase;
using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Implementations.Rag;
using DrugCompare.Features.ChPLNavigator.Services;
using DrugCompare.Infrastructure.SQLite.Rag;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class PolishDrugRegistryRepositoryTests
{
    [TestMethod]
    public void QueryIntentClassifier_PrefersInteractionSectionForInteractionQuestion()
    {
        var intent = QueryIntentClassifier.Classify("Czy można łączyć ten lek z innym?");

        Assert.AreEqual(QueryIntent.Interaction, intent);
        CollectionAssert.AreEqual(new[] { "4.5", "4.4", "4.3", "5.2", "4.8" }, QueryIntentClassifier.PreferredSections(intent).ToArray());
    }

    [TestMethod]
    public void RagAnswerValidator_RejectsCitationOutsideReviewedRetrievedSources()
    {
        var validator = new RagAnswerValidator();
        var sources = new[]
        {
            new KnowledgeChunkResult { Id = 10, ReviewStatus = "verified", ChunkText = "Źródło" },
            new KnowledgeChunkResult { Id = 20, ReviewStatus = "needs_review", ChunkText = "Nieweryfikowane" }
        };
        var answer = new RagAnswer
        {
            Summary = "Podsumowanie",
            Findings = [new RagFinding { Claim = "Twierdzenie", SourceIds = [20] }]
        };

        var result = validator.Validate(answer, sources);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void RagAnswerValidator_AcceptsInsufficientEvidenceWithoutFindings()
    {
        var result = new RagAnswerValidator().Validate(
            new RagAnswer { Summary = "Brak źródeł.", InsufficientEvidence = true },
            []);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void ChplSectionParser_ExportsRealInteractionHeaderAndIgnoresReferenceInText()
    {
        var parser = new ChplSectionParser();
        var sections = parser.ParseSections("""
            W opisie wspomniano: patrz punkt 4.5 w dalszej części dokumentu.

            4.5 Interakcje z innymi produktami leczniczymi i inne rodzaje interakcji
            Właściwa treść interakcji.

            4.6 Wpływ na płodność, ciążę i laktację
            Właściwa treść dotycząca ciąży.
            """);

        Assert.AreEqual(2, sections.Count);
        Assert.AreEqual("4.5", sections[0].SectionNumber);
        StringAssert.Contains(sections[0].Text, "Właściwa treść interakcji");
    }

    [TestMethod]
    public void ChplSectionParser_UsesSection62AsBoundaryWithoutExportingIt()
    {
        var parser = new ChplSectionParser();
        var sections = parser.ParseSections("""
            6.1 Wykaz substancji pomocniczych
            Laktoza jednowodna.

            6.2 Niezgodności farmaceutyczne
            Tej sekcji nie wolno eksportować.
            """);

        Assert.AreEqual(1, sections.Count);
        Assert.AreEqual("6.1", sections[0].SectionNumber);
        Assert.IsFalse(sections[0].Text.Contains("Tej sekcji", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RagRetriever_FiltersNeedsReviewAndPrefersInteractionSection()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-rag-{Guid.NewGuid():N}.db");
        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO knowledge_chunks (id, source_type, source_id, parent_section_id, chunk_index, source_title, product_name, section_number, chunk_text, chunk_hash, review_status, clinical_priority, evidence_kind, created_at)
                    VALUES (1, 'ChPL', 1, 1, 0, 'ChPL Test', 'Test', '4.3', 'interakcje lekowe', 'rag-1', 'reviewed', 100, 'chpl_section', '2026-01-01'),
                           (2, 'ChPL', 2, 2, 0, 'ChPL Test', 'Test', '4.5', 'interakcje lekowe', 'rag-2', 'verified', 100, 'chpl_section', '2026-01-01'),
                           (3, 'ChPL', 3, 3, 0, 'ChPL Test', 'Test', '4.5', 'ukryty wynik', 'rag-3', 'needs_review', 100, 'chpl_section', '2026-01-01');
                    INSERT INTO knowledge_chunks_fts (rowid, source_title, product_name, section_number, chunk_text) VALUES
                    (1, 'ChPL Test', 'Test', '4.3', 'interakcje lekowe'), (2, 'ChPL Test', 'Test', '4.5', 'interakcje lekowe'), (3, 'ChPL Test', 'Test', '4.5', 'ukryty wynik');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var retriever = new SqliteFtsRagRetriever(CreateConnectionFactory(databasePath));
            var interaction = await retriever.RetrieveAsync("interakcje", new RagRetrievalOptions());
            var question = await retriever.RetrieveAsync("Jakie interakcje dotyczą stosowania Test?", new RagRetrievalOptions());
            var hidden = await retriever.RetrieveAsync("ukryty", new RagRetrievalOptions());

            Assert.AreEqual(2, interaction.Count);
            Assert.AreEqual(2, question.Count, "Natural-language filler and punctuation must not hide matching evidence.");
            Assert.AreEqual("4.5", interaction[0].SectionNumber);
            Assert.AreEqual(0, hidden.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task RagRetriever_PreservesSeverity_AndHandlesPolishInflection()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-rag-inflection-{Guid.NewGuid():N}.db");
        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO knowledge_chunks (id, source_type, source_title, product_name, section_number, chunk_text, chunk_hash, review_status, created_at)
                VALUES (1, 'ChPL', 'Przeciwwskazania', 'Test', '4.3', 'Przeciwwskazane w ciężkiej niewydolności wątroby.', 'one', 'reviewed', '2026-01-01'),
                       (2, 'ChPL', 'Ostrzeżenia', 'Test', '4.4', 'Ostrożnie przy łagodnej niewydolności wątroby.', 'two', 'reviewed', '2026-01-01');
                INSERT INTO knowledge_chunks_fts (rowid, source_title, product_name, section_number, chunk_text)
                SELECT id, source_title, product_name, section_number, chunk_text FROM knowledge_chunks;
                """;
            await command.ExecuteNonQueryAsync();
            var retriever = new SqliteFtsRagRetriever(CreateConnectionFactory(databasePath));
            var results = await retriever.RetrieveAsync("Test ciężka niewydolność wątroby", new RagRetrievalOptions());
            Assert.AreEqual(1, results.Count, "Severity must not be discarded while normalizing inflection.");
            Assert.AreEqual(1L, results[0].Id);
            Assert.AreEqual(1, (await retriever.RetrieveAsync("Jakie przeciwwskazania ma Test?", new RagRetrievalOptions())).Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task RagRetriever_ReturnsNoResultsForQueryWithOnlyShortTerms()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-rag-short-{Guid.NewGuid():N}.db");
        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            var retriever = new SqliteFtsRagRetriever(CreateConnectionFactory(databasePath));

            var results = await retriever.RetrieveAsync("a i", new RagRetrievalOptions());

            Assert.AreEqual(0, results.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task LocalDatabaseBackupService_RestoresBackupAfterDatabaseChanges()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-backup-{Guid.NewGuid():N}.db");
        var backupPath = Path.Combine(Path.GetTempPath(), $"drugcompare-backup-copy-{Guid.NewGuid():N}.db");
        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            var service = new SqliteLocalDatabaseBackupService(CreateConnectionFactory(databasePath));
            await service.CreateBackupAsync(backupPath);

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO audit_logs (event_type, created_at) VALUES ('after_backup', '2026-01-01');";
                await command.ExecuteNonQueryAsync();
            }

            var safetyCopy = await service.RestoreAsync(backupPath);
            Assert.IsTrue(File.Exists(safetyCopy));
            await using var restored = new SqliteConnection($"Data Source={databasePath}");
            await restored.OpenAsync();
            await using var restoredCommand = restored.CreateCommand();
            restoredCommand.CommandText = "SELECT COUNT(*) FROM audit_logs WHERE event_type = 'after_backup';";
            Assert.AreEqual(0L, Convert.ToInt64(await restoredCommand.ExecuteScalarAsync()));
            if (File.Exists(safetyCopy)) File.Delete(safetyCopy);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    [TestMethod]
    public async Task SqliteDatabaseInitializer_AppliesAllKnowledgeBaseMigrations()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-migrations-{Guid.NewGuid():N}.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE polish_drug_registry_items (id INTEGER PRIMARY KEY);";
                await command.ExecuteNonQueryAsync();
            }

            var initializer = new SqliteDatabaseInitializer(CreateConnectionFactory(databasePath));
            await initializer.InitializeAsync();

            await using var verified = new SqliteConnection($"Data Source={databasePath}");
            await verified.OpenAsync();
            await using var verifyCommand = verified.CreateCommand();
            verifyCommand.CommandText = "SELECT COUNT(*) FROM schema_migrations;";
            Assert.AreEqual(4L, Convert.ToInt64(await verifyCommand.ExecuteScalarAsync()));
            verifyCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('knowledge_chunks') WHERE name IN ('clinical_category', 'reviewed_by', 'parent_section_id', 'chunk_index');";
            Assert.AreEqual(4L, Convert.ToInt64(await verifyCommand.ExecuteScalarAsync()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task SearchAsync_FindsProductByPartialName()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-{Guid.NewGuid():N}.db");

        try
        {
            await CreateRegistryDatabaseAsync(databasePath);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
                })
                .Build();

            var repository = new SqlitePolishDrugRegistryRepository(
                new SqliteConnectionFactory(configuration));

            var results = await repository.SearchAsync("Apap");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Apap Test", results[0].ProductName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static async Task CreateRegistryDatabaseAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE polish_drug_registry_items (
                id INTEGER PRIMARY KEY,
                rpl_id TEXT,
                product_name TEXT,
                normalized_product_name TEXT,
                active_substance_text TEXT,
                strength TEXT,
                pharmaceutical_form TEXT,
                marketing_authorization_holder TEXT,
                authorization_number TEXT,
                authorization_validity TEXT,
                product_type TEXT,
                procedure_type TEXT,
                chpl_url TEXT,
                leaflet_url TEXT,
                source TEXT,
                source_version TEXT,
                imported_at TEXT
            );

            INSERT INTO polish_drug_registry_items (
                id, product_name, normalized_product_name, active_substance_text, source, imported_at
            ) VALUES (1, 'Apap Test', 'apap test', 'Paracetamolum', 'RPL', '2026-01-01');
            """;
        await command.ExecuteNonQueryAsync();
    }

    [TestMethod]
    public async Task IngestAsync_RollsBackDocumentAndSections_WhenChunkInsertFails()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-kb-{Guid.NewGuid():N}.db");

        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            var repository = new SqliteAtomicKnowledgeBaseIngestionRepository(CreateConnectionFactory(databasePath));
            var now = DateTime.UtcNow;
            var document = new ChplDocumentRecord { ProductName = "Test", SourceFile = "test.pdf", DocumentType = "ChPL", Language = "pl", ReviewStatus = "needs_review", ImportedAt = now };
            var sections = new[]
            {
                new ChplSectionRecord { SectionNumber = "4.1", Text = "Treść", TextHash = "same", ReviewStatus = "needs_review", CreatedAt = now },
                new ChplSectionRecord { SectionNumber = "4.1", Text = "Treść", TextHash = "same", ReviewStatus = "needs_review", CreatedAt = now }
            };

            await Assert.ThrowsExceptionAsync<SqliteException>(() => repository.IngestAsync(document, sections));

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT (SELECT COUNT(*) FROM chpl_documents) + (SELECT COUNT(*) FROM chpl_sections) + (SELECT COUNT(*) FROM knowledge_chunks);";
            Assert.AreEqual(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task IngestAsync_AssignsClinicalMetadataToChplChunk()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-kb-{Guid.NewGuid():N}.db");

        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            var repository = new SqliteAtomicKnowledgeBaseIngestionRepository(CreateConnectionFactory(databasePath));
            var now = DateTime.UtcNow;
            var result = await repository.IngestAsync(
                new ChplDocumentRecord { ProductName = "Test", ActiveSubstanceText = "Paracetamolum", SourceFile = "test.pdf", DocumentType = "ChPL", Language = "pl", ReviewStatus = "needs_review", ImportedAt = now },
                [new ChplSectionRecord { SectionNumber = "4.5", Text = "Interakcje", TextHash = "unique", ReviewStatus = "needs_review", CreatedAt = now }]);

            Assert.AreEqual(1, result.ChunksSaved);
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT clinical_category, clinical_priority, evidence_kind, active_substance FROM knowledge_chunks;";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual("interaction", reader.GetString(0));
            Assert.AreEqual(100L, reader.GetInt64(1));
            Assert.AreEqual("chpl_section", reader.GetString(2));
            Assert.AreEqual("Paracetamolum", reader.GetString(3));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task ReviewDocumentForChunkAsync_UpdatesDocumentSectionsChunksAndAuditLog()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-review-{Guid.NewGuid():N}.db");
        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            var factory = CreateConnectionFactory(databasePath);
            var ingestion = new SqliteAtomicKnowledgeBaseIngestionRepository(factory);
            var now = DateTime.UtcNow;
            await ingestion.IngestAsync(new ChplDocumentRecord { ProductName = "Test", SourceFile = "review.pdf", DocumentType = "ChPL", Language = "pl", ReviewStatus = "needs_review", ImportedAt = now }, [new ChplSectionRecord { SectionNumber = "4.3", Text = "Przeciwwskazania", TextHash = "review", ReviewStatus = "needs_review", CreatedAt = now }]);

            var reviewer = new SqliteKnowledgeBaseReviewRepository(factory);
            await reviewer.ReviewDocumentForChunkAsync(1, "verified", "tester", "sprawdzone");

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT (SELECT review_status FROM chpl_documents), (SELECT review_status FROM chpl_sections), (SELECT review_status FROM knowledge_chunks), (SELECT COUNT(*) FROM audit_logs WHERE event_type = 'ChplDocumentReviewed');";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.IsTrue(await reader.ReadAsync());
            Assert.AreEqual("verified", reader.GetString(0));
            Assert.AreEqual("verified", reader.GetString(1));
            Assert.AreEqual("verified", reader.GetString(2));
            Assert.AreEqual(1L, reader.GetInt64(3));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task IngestAsync_SplitsLongSectionIntoOrderedChunks()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-chunks-{Guid.NewGuid():N}.db");
        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            var repository = new SqliteAtomicKnowledgeBaseIngestionRepository(CreateConnectionFactory(databasePath));
            var now = DateTime.UtcNow;
            var text = string.Join("\n\n", Enumerable.Range(1, 4).Select(number => $"Akapit {number}: {new string('x', 500)}"));
            var result = await repository.IngestAsync(new ChplDocumentRecord { ProductName = "Test", SourceFile = "chunks.pdf", DocumentType = "ChPL", Language = "pl", ReviewStatus = "needs_review", ImportedAt = now }, [new ChplSectionRecord { SectionNumber = "4.5", Text = text, TextHash = "chunks", ReviewStatus = "needs_review", CreatedAt = now }]);

            Assert.IsTrue(result.ChunksSaved > 1);
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT chunk_index, parent_section_id FROM knowledge_chunks ORDER BY chunk_index;";
            await using var reader = await command.ExecuteReaderAsync();
            var index = 0;
            while (await reader.ReadAsync())
            {
                Assert.AreEqual(index++, reader.GetInt32(0));
                Assert.AreEqual(1L, reader.GetInt64(1));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task IngestAsync_SplitsSingleLongParagraphWithinChunkLimit()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"drugcompare-long-paragraph-{Guid.NewGuid():N}.db");
        try
        {
            await CreateKnowledgeBaseDatabaseAsync(databasePath);
            var repository = new SqliteAtomicKnowledgeBaseIngestionRepository(CreateConnectionFactory(databasePath));
            var now = DateTime.UtcNow;
            var text = string.Join(' ', Enumerable.Repeat("bardzoDlugiWyraz", 300));

            var result = await repository.IngestAsync(
                new ChplDocumentRecord { ProductName = "Test", SourceFile = "long.pdf", DocumentType = "ChPL", Language = "pl", ReviewStatus = "needs_review", ImportedAt = now },
                [new ChplSectionRecord { SectionNumber = "4.4", Text = text, TextHash = "long", ReviewStatus = "needs_review", CreatedAt = now }]);

            Assert.IsTrue(result.ChunksSaved > 1);
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(LENGTH(chunk_text)) FROM knowledge_chunks;";
            Assert.IsTrue(Convert.ToInt64(await command.ExecuteScalarAsync()) <= 1200);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static SqliteConnectionFactory CreateConnectionFactory(string databasePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}" })
            .Build();
        return new SqliteConnectionFactory(configuration);
    }

    private static async Task CreateKnowledgeBaseDatabaseAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE chpl_documents (id INTEGER PRIMARY KEY AUTOINCREMENT, rpl_product_id INTEGER, product_name TEXT, chpl_url TEXT, source_file TEXT, local_file_path TEXT, file_hash TEXT, document_type TEXT NOT NULL, language TEXT NOT NULL, parser_version TEXT, review_status TEXT NOT NULL, imported_at TEXT NOT NULL, parsed_at TEXT, reviewed_at TEXT, reviewed_by TEXT, review_note TEXT);
            CREATE TABLE chpl_sections (id INTEGER PRIMARY KEY AUTOINCREMENT, chpl_document_id INTEGER NOT NULL, section_number TEXT NOT NULL, section_title TEXT, section_type TEXT, text TEXT NOT NULL, text_hash TEXT, review_status TEXT NOT NULL, created_at TEXT NOT NULL, reviewed_at TEXT, reviewed_by TEXT, review_note TEXT);
            CREATE TABLE knowledge_chunks (id INTEGER PRIMARY KEY AUTOINCREMENT, source_type TEXT NOT NULL, source_id INTEGER, parent_section_id INTEGER, chunk_index INTEGER NOT NULL DEFAULT 0, source_title TEXT NOT NULL, product_name TEXT, active_substance TEXT, section_number TEXT, section_title TEXT, chunk_text TEXT NOT NULL, chunk_hash TEXT NOT NULL UNIQUE, review_status TEXT NOT NULL, clinical_category TEXT, clinical_priority INTEGER NOT NULL DEFAULT 0, evidence_kind TEXT NOT NULL DEFAULT 'unknown', source_url TEXT, created_at TEXT NOT NULL, reviewed_at TEXT, reviewed_by TEXT, review_note TEXT);
            CREATE TABLE audit_logs (id INTEGER PRIMARY KEY AUTOINCREMENT, event_type TEXT NOT NULL, details TEXT, created_at TEXT NOT NULL);
            CREATE VIRTUAL TABLE knowledge_chunks_fts USING fts5(source_title, product_name, section_number, chunk_text);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
