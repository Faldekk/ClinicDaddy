using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.Rag;
using DrugCompare.Features.EvidenceAssistant;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class EvidenceAssistantTests
{
    [TestMethod]
    public async Task Generate_RefreshesSourcesForCurrentQuestion()
    {
        var retriever = new Retriever();
        var generator = new Generator();
        var vm = new EvidenceAssistantViewModel(retriever, new Review(), generator, new Status(OllamaState.Ready))
        { Query = "Nowe pytanie" };
        vm.Results.Add(new KnowledgeChunkResult { Id = 99, ReviewStatus = "reviewed" });
        await vm.GenerateRagAnswerCommand.ExecuteAsync(null);
        Assert.AreEqual("Nowe pytanie", retriever.Query);
        CollectionAssert.AreEqual(new long[] { 1 }, generator.SourceIds);
        Assert.AreEqual("Prawdziwa odpowiedź.", vm.GeneratedAnswer);
        Assert.IsFalse(vm.IsBusy);
    }

    [TestMethod]
    public async Task MissingModel_PreservesSources_AndDoesNotInsertAutomaticAnswer()
    {
        var generator = new Generator();
        var vm = new EvidenceAssistantViewModel(new Retriever(), new Review(), generator, new Status(OllamaState.ModelMissing))
        { Query = "Pytanie" };
        await vm.GenerateRagAnswerCommand.ExecuteAsync(null);
        Assert.AreEqual(0, generator.SourceIds.Length);
        Assert.AreEqual(1, vm.Results.Count);
        Assert.AreEqual("", vm.GeneratedAnswer);
        StringAssert.Contains(vm.StatusMessage, "Brak modelu");
    }

    [TestMethod]
    public async Task Cancellation_ReleasesBusyState_AndKeepsRetrievedSources()
    {
        var generator = new Generator { WaitForCancellation = true };
        var vm = new EvidenceAssistantViewModel(new Retriever(), new Review(), generator, new Status(OllamaState.Ready))
        { Query = "Pytanie" };
        var operation = vm.GenerateRagAnswerCommand.ExecuteAsync(null);
        await generator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.GenerateRagAnswerCancelCommand.Execute(null);
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(vm.IsBusy);
        Assert.IsFalse(vm.IsGenerating);
        Assert.AreEqual(1, vm.Results.Count);
        StringAssert.Contains(vm.StatusMessage, "anulowane");
    }

    private sealed class Retriever : IRagRetriever
    {
        public string? Query { get; private set; }
        public Task<IReadOnlyList<KnowledgeChunkResult>> RetrieveAsync(string query, RagRetrievalOptions options, CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult<IReadOnlyList<KnowledgeChunkResult>>([new KnowledgeChunkResult
            { Id = 1, ReviewStatus = "reviewed", ChunkText = "Źródło" }]);
        }
    }
    private sealed class Generator : IRagAnswerService
    {
        public long[] SourceIds { get; private set; } = [];
        public bool WaitForCancellation { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RagAnswer> GenerateAsync(string question, IReadOnlyList<KnowledgeChunkResult> sources, CancellationToken cancellationToken = default)
        {
            SourceIds = sources.Select(x => x.Id).ToArray();
            Started.TrySetResult();
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new RagAnswer { Summary = "Prawdziwa odpowiedź." };
        }
    }
    private sealed class Status(OllamaState state) : IOllamaStatusService
    {
        public Task<OllamaStatus> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OllamaStatus(state, "qwen3:8b", state == OllamaState.Ready ? "Gotowe" : "Brak modelu"));
    }
    private sealed class Review : IKnowledgeBaseReviewService
    {
        public Task ReviewDocumentForChunkAsync(long chunkId, string newStatus, string reviewedBy, string? reviewNote = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
