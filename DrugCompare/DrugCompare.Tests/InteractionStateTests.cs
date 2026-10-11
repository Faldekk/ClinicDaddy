using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.ViewModels.Interaction;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class InteractionStateTests
{
    [TestMethod]
    public async Task Lookup_BlocksAllMutationsAndConcurrentOperations()
    {
        var pending = new TaskCompletionSource<DrugLookupResult?>();
        var services = new Services { DrugResult = pending.Task };
        var vm = Create(services);
        vm.DrugNameInput = "test";
        var operation = vm.FindDrugCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsWorking);
        Assert.IsFalse(vm.CanWork);
        Assert.IsFalse(vm.ClearCaseCommand.CanExecute(null));
        Assert.IsFalse(vm.AcceptDetectedSubstanceCommand.CanExecute(null));
        Assert.IsFalse(vm.AcceptAllDetectedSubstancesCommand.CanExecute(null));
        Assert.IsFalse(vm.RemoveAcceptedSubstanceCommand.CanExecute(null));
        Assert.IsFalse(vm.AddManualSubstanceCommand.CanExecute(null));
        Assert.IsFalse(vm.CheckInteractionsCommand.CanExecute(null));
        Assert.IsFalse(vm.ExportCurrentReportCommand.CanExecute(null));
        // Explicit execution must also be guarded, not only button CanExecute.
        vm.ClearCaseCommand.Execute(null);
        await vm.CheckInteractionsCommand.ExecuteAsync(null);
        Assert.AreEqual("test", vm.DrugNameInput);
        Assert.AreEqual(0, services.CheckCalls);
        pending.SetResult(null);
        await operation;
        Assert.IsTrue(vm.CanWork);
        StringAssert.Contains(vm.StatusMessage, "Nie znaleziono");
    }

    [TestMethod]
    public async Task LookupAndManualFailure_ReleaseControlsAndAllowRetry()
    {
        var services = new Services
        {
            DrugResult = Task.FromException<DrugLookupResult?>(new InvalidOperationException("test")),
            SubstanceResult = Task.FromException<ActiveSubstanceItem?>(new InvalidOperationException("test"))
        };
        var vm = Create(services);
        vm.DrugNameInput = "test";
        await vm.FindDrugCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.CanWork);
        StringAssert.Contains(vm.StatusMessage, "Błąd wyszukiwania");
        vm.ManualSubstanceInput = "test";
        await vm.AddManualSubstanceCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.CanWork);
        StringAssert.Contains(vm.StatusMessage, "Błąd dodawania");
        services.SubstanceResult = Task.FromResult<ActiveSubstanceItem?>(Substance("A"));
        await vm.AddManualSubstanceCommand.ExecuteAsync(null);
        Assert.AreEqual(1, vm.AcceptedSubstances.Count);
        Assert.AreEqual(string.Empty, vm.ManualSubstanceInput);
    }

    [TestMethod]
    public async Task CheckFailure_ClearsOldResultsAndAllowsRetry()
    {
        var pending = new TaskCompletionSource<List<InteractionResult>>();
        var services = new Services { CheckResult = pending.Task };
        var vm = Create(services);
        vm.AcceptedSubstances.Add(Substance("A"));
        vm.AcceptedSubstances.Add(Substance("B"));
        vm.InteractionResults.Add(new InteractionResult());
        var operation = vm.CheckInteractionsCommand.ExecuteAsync(null);
        Assert.AreEqual(0, vm.InteractionResults.Count);
        Assert.IsFalse(vm.ClearCaseCommand.CanExecute(null));
        StringAssert.Contains(vm.ResultSummaryMessage, "Sprawdzanie");
        pending.SetException(new InvalidOperationException("test"));
        await operation;
        Assert.IsTrue(vm.CanWork);
        StringAssert.Contains(vm.ResultSummaryMessage, "nie są dostępne");
        services.CheckResult = Task.FromResult(new List<InteractionResult>());
        await vm.CheckInteractionsCommand.ExecuteAsync(null);
        Assert.AreEqual(2, services.CheckCalls);
        StringAssert.Contains(vm.ResultSummaryMessage, "Brak danych nie oznacza");
    }

    [TestMethod]
    public async Task ChangingSubstances_InvalidatesPreviouslyComputedResults()
    {
        var services = new Services
        {
            CheckResult = Task.FromResult(new List<InteractionResult>
            {
                new() { SubstanceA = "A", SubstanceB = "B", Severity = "major" }
            })
        };
        var vm = Create(services);
        vm.AcceptedSubstances.Add(Substance("A"));
        vm.AcceptedSubstances.Add(Substance("B"));
        await vm.CheckInteractionsCommand.ExecuteAsync(null);
        Assert.AreEqual(1, vm.InteractionResults.Count);
        Assert.IsNotNull(vm.SelectedInteraction);
        vm.SelectedAcceptedSubstance = vm.AcceptedSubstances[0];
        vm.RemoveAcceptedSubstanceCommand.Execute(null);
        Assert.AreEqual(0, vm.InteractionResults.Count);
        Assert.IsNull(vm.SelectedInteraction);
        StringAssert.Contains(vm.ResultSummaryMessage, "Sprawdź interakcje ponownie");
        vm.ClearCaseCommand.Execute(null);
        StringAssert.Contains(vm.ResultSummaryMessage, "Nie wykonano");
    }

    [TestMethod]
    public async Task Report_DoesNotClaimNoInteractionsBeforeCheckOrAfterFailure()
    {
        var services = new Services
        {
            CheckResult = Task.FromException<List<InteractionResult>>(new InvalidOperationException("test"))
        };
        var vm = Create(services);
        vm.AcceptedSubstances.Add(Substance("A"));
        vm.AcceptedSubstances.Add(Substance("B"));
        var build = typeof(InteractionCheckerViewModel).GetMethod("BuildCurrentReport",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var before = (string)build.Invoke(vm, null)!;
        StringAssert.Contains(before, "Sprawdź interakcje ponownie");
        Assert.IsFalse(before.Contains("No known interaction was found"));
        await vm.CheckInteractionsCommand.ExecuteAsync(null);
        var failed = (string)build.Invoke(vm, null)!;
        StringAssert.Contains(failed, "Wyniki nie są dostępne");
        Assert.IsFalse(failed.Contains("No known interaction was found"));
    }

    private static ActiveSubstanceItem Substance(string name) => new() { Name = name, NormalizedName = name };
    private static InteractionCheckerViewModel Create(Services s) => new(s, s, s, s);

    private sealed class Services : IDrugLookupService, ISubstanceLookupService, IInteractionCheckerService, IAuditLogService
    {
        public Task<DrugLookupResult?> DrugResult { get; set; } = Task.FromResult<DrugLookupResult?>(null);
        public Task<ActiveSubstanceItem?> SubstanceResult { get; set; } = Task.FromResult<ActiveSubstanceItem?>(null);
        public Task<List<InteractionResult>> CheckResult { get; set; } = Task.FromResult(new List<InteractionResult>());
        public int CheckCalls { get; private set; }
        public Task<DrugLookupResult?> FindDrugAsync(string drugName) => DrugResult;
        public Task<ActiveSubstanceItem?> FindActiveSubstanceAsync(string substanceName) => SubstanceResult;
        public Task<List<InteractionResult>> CheckInteractionsAsync(IReadOnlyCollection<ActiveSubstanceItem> substances)
        { CheckCalls++; return CheckResult; }
        public Task AddAsync(string eventType, string? details = null) => Task.CompletedTask;
        public Task WriteAsync(string eventType, string? details = null) => Task.CompletedTask;
        public Task WriteAsync(string eventType, object? details) => Task.CompletedTask;
        public Task<List<AuditLogItem>> GetRecentLogsAsync(int limit = 50) => Task.FromResult(new List<AuditLogItem>());
        public Task<List<AuditLogItem>> GetRecentAsync(int limit = 50) => GetRecentLogsAsync(limit);
    }
}
