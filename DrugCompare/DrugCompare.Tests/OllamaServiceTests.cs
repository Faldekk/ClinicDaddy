using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.Rag;
using DrugCompare.Application.Services.Implementations.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class OllamaServiceTests
{
    [TestMethod]
    public async Task Status_DistinguishesInstalledAndMissingModel()
    {
        using var readyClient = Client(_ => Json("""{"models":[{"name":"qwen3:8b"}]}"""));
        var ready = await new OllamaStatusService(readyClient, Config()).CheckAsync();
        Assert.IsTrue(ready.IsReady);
        using var missingClient = Client(_ => Json("""{"models":[]}"""));
        var missing = await new OllamaStatusService(missingClient, Config()).CheckAsync();
        Assert.AreEqual(OllamaState.ModelMissing, missing.State);
        StringAssert.Contains(missing.Message, "ollama pull qwen3:8b");
    }

    [TestMethod]
    public async Task Status_ReportsConnectionFailureAndInvalidResponse()
    {
        using var offline = Client(_ => throw new HttpRequestException("Connection refused"));
        Assert.AreEqual(OllamaState.Unavailable, (await new OllamaStatusService(offline, Config()).CheckAsync()).State);
        using var malformed = Client(_ => Json("not-json"));
        Assert.AreEqual(OllamaState.Error, (await new OllamaStatusService(malformed, Config()).CheckAsync()).State);
    }

    [TestMethod]
    public async Task Generate_ReportsMissingModelTimeoutAndInvalidJsonSeparately()
    {
        foreach (var (handler, expected) in new (Func<HttpRequestMessage, HttpResponseMessage>, RagFailureKind)[]
        {
            (_ => Json("""{"error":"model not found"}""", HttpStatusCode.NotFound), RagFailureKind.ModelMissing),
            (_ => throw new TaskCanceledException(), RagFailureKind.Timeout),
            (_ => Json("not-json"), RagFailureKind.InvalidAnswer)
        })
        {
            using var client = Client(handler);
            var service = new OllamaRagAnswerService(client, Config(), new RagAnswerValidator());
            var error = await Assert.ThrowsExceptionAsync<RagServiceException>(() => service.GenerateAsync("Pytanie", Sources(1)));
            Assert.AreEqual(expected, error.Kind);
        }
    }

    [TestMethod]
    public async Task InvalidEndpoint_IsReportedWithoutCrashingServiceConstruction()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Rag:Ollama:Endpoint"] = "not-an-address" }).Build();
        using var client = Client(_ => throw new AssertFailedException("Must not send a request."));
        var status = await new OllamaStatusService(client, config).CheckAsync();
        Assert.AreEqual(OllamaState.Error, status.State);
        var service = new OllamaRagAnswerService(client, config, new RagAnswerValidator());
        var error = await Assert.ThrowsExceptionAsync<RagServiceException>(() => service.GenerateAsync("Pytanie", Sources(1)));
        Assert.AreEqual(RagFailureKind.Connection, error.Kind);
    }

    [TestMethod]
    public async Task Generate_ReturnsActualModelAnswer_WithValidCitation()
    {
        using var client = Client(_ => Answer(1));
        var answer = await new OllamaRagAnswerService(client, Config(), new RagAnswerValidator())
            .GenerateAsync("Pytanie", Sources(1));
        Assert.AreEqual("Wniosek", answer.Summary, "Do not expose the model's separately generated unchecked summary.");
        CollectionAssert.AreEqual(new long[] { 1 }, answer.Findings.Single().SourceIds);
    }

    [TestMethod]
    public async Task Generate_RejectsCitationToRetrievedSourceThatWasNotSentToModel()
    {
        var calls = 0;
        using var client = Client(_ => { calls++; return Answer(9); });
        var error = await Assert.ThrowsExceptionAsync<RagServiceException>(() =>
            new OllamaRagAnswerService(client, Config(), new RagAnswerValidator()).GenerateAsync("Pytanie", Sources(9)));
        Assert.AreEqual(RagFailureKind.InvalidAnswer, error.Kind);
        Assert.AreEqual(2, calls, "Only one repair attempt should be made.");
    }

    [TestMethod]
    public async Task Generate_PreservesUserCancellation_InsteadOfReportingTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = Client(_ => Answer(1));
        try
        {
            await new OllamaRagAnswerService(client, Config(), new RagAnswerValidator())
                .GenerateAsync("Pytanie", Sources(1), cancellation.Token);
            Assert.Fail("Expected cancellation.");
        }
        catch (OperationCanceledException) { }
    }

    [TestMethod]
    public void Validator_RejectsMergingIndependentBulletConditions()
    {
        var text = "• Pierwszy niezależny warunek. • Drugi niezależny warunek.";
        var sources = Sources(1);
        sources[0].ChunkText = text;
        var answer = new RagAnswer { Findings = [new RagFinding
        { Claim = "Pierwszy oraz drugi warunek", SourceIds = [1], EvidenceQuote = text }] };
        Assert.IsFalse(new RagAnswerValidator().Validate(answer, sources).IsValid);
    }

    [TestMethod]
    public async Task Generate_NeverCallsModelForUnreviewedProductionSources()
    {
        var sources = Sources(1);
        sources[0].ReviewStatus = "needs_review";
        using var client = Client(_ => throw new AssertFailedException("Unreviewed production evidence must not be sent to Ollama."));
        var answer = await new OllamaRagAnswerService(client, Config(), new RagAnswerValidator())
            .GenerateAsync("Pytanie", sources);
        Assert.IsTrue(answer.InsufficientEvidence);
        Assert.AreEqual(0, answer.Findings.Count);
    }

    [TestMethod]
    public async Task Generate_DoesNotExposeClaimsInsideInsufficientEvidenceSummary()
    {
        using var client = Client(_ => Json(JsonSerializer.Serialize(new
        { message = new { content = """{"summary":"Niepoparta porada medyczna","findings":[],"insufficientEvidence":true}""" } })));
        var answer = await new OllamaRagAnswerService(client, Config(), new RagAnswerValidator())
            .GenerateAsync("Pytanie", Sources(1));
        Assert.IsTrue(answer.InsufficientEvidence);
        Assert.AreEqual(string.Empty, answer.Summary);
    }

    [TestMethod]
    public void Validator_AcceptsGroundedFindingsWithoutSeparateSummary()
    {
        Assert.IsTrue(new RagAnswerValidator().Validate(new RagAnswer { Findings =
        [new RagFinding { Claim = "Wniosek", SourceIds = [1], EvidenceQuote = "pełny tekst źródła do testowania cytatu." }] }, Sources(1)).IsValid);
    }

    [TestMethod]
    public void Validator_RejectsPaddingWithRepeatedEvidenceQuotes()
    {
        var quote = "pełny tekst źródła do testowania cytatu.";
        var answer = new RagAnswer { Summary = "Model", Findings =
        [new RagFinding { Claim = "Warunek", SourceIds = [1], EvidenceQuote = quote },
         new RagFinding { Claim = "Niepoparty metakomentarz", SourceIds = [1], EvidenceQuote = quote }] };
        Assert.IsFalse(new RagAnswerValidator().Validate(answer, Sources(1)).IsValid);
    }

    [TestMethod]
    public void Validator_RejectsInventedQuote_AndAcceptsWhitespaceOnlyDifferences()
    {
        var answer = new RagAnswer { Summary = "Model", Findings = [new RagFinding
        { Claim = "Wniosek", SourceIds = [1], EvidenceQuote = "Cytat którego nie ma w źródle." }] };
        var validator = new RagAnswerValidator();
        Assert.IsFalse(validator.Validate(answer, Sources(1)).IsValid);
        answer.Findings[0].EvidenceQuote = "pełny tekst\nźródła do testowania cytatu.";
        Assert.IsTrue(validator.Validate(answer, Sources(1)).IsValid);
    }

    [TestMethod]
    public async Task Generate_RejectsQuoteOutsideFragmentActuallySent()
    {
        var sources = Sources(1);
        sources[0].ChunkText = new string('x', 950) + " pełny tekst źródła do testowania cytatu.";
        using var client = Client(_ => Answer(1));
        var error = await Assert.ThrowsExceptionAsync<RagServiceException>(() =>
            new OllamaRagAnswerService(client, Config(), new RagAnswerValidator()).GenerateAsync("Pytanie", sources));
        Assert.AreEqual(RagFailureKind.InvalidAnswer, error.Kind);
    }

    [TestMethod]
    public void Validator_RejectsNullCollectionsWithoutCrashing()
    {
        var validator = new RagAnswerValidator();
        Assert.IsFalse(validator.Validate(new RagAnswer { Summary = "Treść", Findings = null! }, Sources(1)).IsValid);
        Assert.IsFalse(validator.Validate(new RagAnswer
        {
            Summary = "Treść", Findings = [new RagFinding { Claim = "Wniosek", SourceIds = null! }]
        }, Sources(1)).IsValid);
    }

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Rag:Ollama:Enabled"] = "true", ["Rag:Ollama:Model"] = "qwen3:8b",
        ["Rag:Ollama:Endpoint"] = "http://localhost:11434/"
    }).Build();
    private static KnowledgeChunkResult[] Sources(int count) => Enumerable.Range(1, count)
        .Select(id => new KnowledgeChunkResult { Id = id, ReviewStatus = "reviewed", ChunkText = "To jest pełny tekst źródła do testowania cytatu." }).ToArray();
    private static HttpResponseMessage Answer(long id) => Json(JsonSerializer.Serialize(new
    {
        message = new { content = JsonSerializer.Serialize(new
        {
            summary = "Odpowiedź modelu.", findings = new[] { new { claim = "Wniosek", sourceIds = new[] { id }, evidenceQuote = "pełny tekst źródła do testowania cytatu." } },
            insufficientEvidence = false
        }) }
    }));
    private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new Handler(response));
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }
}
