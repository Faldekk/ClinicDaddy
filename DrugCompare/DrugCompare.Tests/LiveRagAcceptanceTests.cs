using System.Diagnostics;
using System.Text.Json;
using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Implementations.Rag;
using DrugCompare.Infrastructure.SQLite;
using DrugCompare.Infrastructure.SQLite.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class LiveRagAcceptanceTests
{
    [TestMethod]
    [TestCategory("DiagnosticLive")]
    public async Task UnreviewedMultiProductBenchmark_WritesResultsWithoutApprovingDatabase()
    {
        var database = Environment.GetEnvironmentVariable("CLINICDADDY_LIVE_DB");
        var report = Environment.GetEnvironmentVariable("CLINICDADDY_RAG_REPORT");
        if (string.IsNullOrEmpty(database) || string.IsNullOrEmpty(report)
            || Environment.GetEnvironmentVariable("CLINICDADDY_DIAGNOSTIC_UNREVIEWED") != "1")
            Assert.Inconclusive("Explicit opt-in required for diagnostic use of unreviewed evidence. Not a clinical acceptance test.");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={database};Mode=ReadOnly",
            ["Rag:Ollama:Model"] = "qwen3:8b", ["Rag:Ollama:Endpoint"] = "http://127.0.0.1:11434/"
        }).Build();
        var retriever = new SqliteFtsRagRetriever(new SqliteConnectionFactory(config));
        var cases = new[]
        {
            (Id: "apap-children-contra", Product: "APAP dla dzieci FORTE", Query: "APAP przeciwwskazania", Question: "Jakie przeciwwskazania ma APAP dla dzieci FORTE?", Section: "4.3"),
            (Id: "ibuprom-interactions", Product: "Ibuprom RR MAX", Query: "Ibuprom interakcje", Question: "Jakie interakcje ma Ibuprom RR MAX?", Section: "4.5"),
            (Id: "apap-migraine-contra", Product: "APAP migrena", Query: "APAP przeciwwskazania", Question: "Jakie przeciwwskazania ma APAP migrena?", Section: "4.3")
        };
        var selectedCase = Environment.GetEnvironmentVariable("CLINICDADDY_DIAGNOSTIC_CASE");
        if (!string.IsNullOrWhiteSpace(selectedCase)) cases = cases.Where(item => item.Id == selectedCase).ToArray();
        Assert.IsTrue(cases.Length > 0, "Unknown diagnostic case id.");
        var results = new List<object>();
        var technicalFailures = 0;
        foreach (var item in cases)
        {
            var elapsed = Stopwatch.StartNew();
            var sources = await retriever.RetrieveAsync(item.Query, new RagRetrievalOptions
            { ProductName = item.Product, SectionNumber = item.Section, IncludeNeedsReview = true, Limit = 4 });
            var productionSources = await retriever.RetrieveAsync(item.Query, new RagRetrievalOptions
            { ProductName = item.Product, SectionNumber = item.Section, Limit = 4 });
            RagAnswer? answer = null;
            string? error = null;
            try
            {
                if (sources.Count == 0) throw new InvalidOperationException("No matching diagnostic sources.");
                Assert.IsTrue(sources.All(source => source.ProductName == item.Product), "Do not mix formulations.");
                // Test-only copies allow exercising the unchanged production generator.
                // Original statuses remain in the report. No database review operation occurs.
                var testCopies = sources.Select(source => new KnowledgeChunkResult
                {
                    Id = source.Id, ProductName = source.ProductName, ActiveSubstance = source.ActiveSubstance,
                    SectionNumber = source.SectionNumber, SectionTitle = source.SectionTitle,
                    ChunkText = source.ChunkText, ReviewStatus = "reviewed"
                }).ToArray();
                using var client = new System.Net.Http.HttpClient(new CaptureHandler(report + "." + item.Id));
                answer = await new OllamaRagAnswerService(client, config, new RagAnswerValidator())
                    .GenerateAsync(item.Question, testCopies);
            }
            catch (Exception ex) { error = ex.Message; technicalFailures++; }
            results.Add(new
            {
                item.Id, item.Product, item.Question, retrievalQuery = item.Query, item.Section,
                seconds = elapsed.Elapsed.TotalSeconds, diagnosticSources = sources,
                productionSourceCount = productionSources.Count, answer, error,
                clinicalAssessment = "pending_manual_review", databaseStatusesChanged = false
            });
            await File.WriteAllTextAsync(report!, JsonSerializer.Serialize(new
            {
                diagnosticOnly = true, usesUnreviewedEvidence = true,
                warning = "Not clinical approval. Status adaptation exists only on test copies; production policy unchanged.",
                results
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{item.Id}: sources={sources.Count}, findings={answer?.Findings.Count ?? 0}, error={error ?? "none"}, elapsed={elapsed.Elapsed.TotalSeconds:F1}s");
        }
        Assert.AreEqual(cases.Length, results.Count, "Every diagnostic case must be reported, including failures.");
        Assert.AreEqual(0, technicalFailures, "See the persisted diagnostic report for failed cases. This assertion does not certify clinical meaning.");
    }

    [TestMethod]
    [TestCategory("Live")]
    public async Task RealDatabaseAndOllama_ReturnGroundedFindings()
    {
        var database = Environment.GetEnvironmentVariable("CLINICDADDY_LIVE_DB");
        var report = Environment.GetEnvironmentVariable("CLINICDADDY_RAG_REPORT");
        if (string.IsNullOrEmpty(database) || string.IsNullOrEmpty(report))
            Assert.Inconclusive("Opt-in: set CLINICDADDY_LIVE_DB and CLINICDADDY_RAG_REPORT. No production writes.");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={database};Mode=ReadOnly",
            ["Rag:Ollama:Model"] = "qwen3:8b", ["Rag:Ollama:Endpoint"] = "http://127.0.0.1:11434/"
        }).Build();
        var retriever = new SqliteFtsRagRetriever(new SqliteConnectionFactory(config));
        using var client = new System.Net.Http.HttpClient(new CaptureHandler(report!));
        var generator = new OllamaRagAnswerService(client, config, new RagAnswerValidator());
        var question = "Jakie ostrzeżenia dotyczą stosowania Xanax?";
        var sources = await retriever.RetrieveAsync(question, new RagRetrievalOptions { ProductName = "Xanax SR", Limit = 8 });
        Assert.IsTrue(sources.Count > 0, "A natural question must retrieve real evidence.");
        Assert.AreEqual("4.4", sources[0].SectionNumber);
        var elapsed = Stopwatch.StartNew();
        var answer = await generator.GenerateAsync(question, sources);
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new
        { question, seconds = elapsed.Elapsed.TotalSeconds, sources, answer }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.IsFalse(answer.InsufficientEvidence);
        Assert.IsTrue(answer.Findings.Count > 0);
        Console.WriteLine($"Live RAG: {elapsed.Elapsed.TotalSeconds:F1}s. Report: {report}");
    }

    private sealed class CaptureHandler(string report) : System.Net.Http.DelegatingHandler(new System.Net.Http.HttpClientHandler())
    {
        private int _request;
        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            await File.WriteAllTextAsync(report + $".response-{++_request}.json", await response.Content.ReadAsStringAsync(cancellationToken), cancellationToken);
            return response;
        }
    }
}
