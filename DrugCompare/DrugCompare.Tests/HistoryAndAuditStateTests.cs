using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class HistoryAndAuditStateTests
{
    [TestMethod]
    public async Task Refreshes_HaveIndependentBusyState_AndRejectRepeatedExecution()
    {
        var history = new TaskCompletionSource<List<InteractionHistoryItem>>();
        var audit = new TaskCompletionSource<List<AuditLogItem>>();
        var service = new Services { History = history.Task, Audit = audit.Task };
        var vm = Create(service);
        var first = vm.LoadHistoryCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsLoadingHistory);
        Assert.IsFalse(vm.LoadHistoryCommand.CanExecute(null));
        Assert.IsTrue(vm.LoadAuditLogsCommand.CanExecute(null));
        await vm.LoadHistoryCommand.ExecuteAsync(null);
        Assert.AreEqual(1, service.HistoryCalls);
        var second = vm.LoadAuditLogsCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsLoadingAuditLogs);
        await vm.LoadAuditLogsCommand.ExecuteAsync(null);
        Assert.AreEqual(1, service.AuditCalls);
        history.SetResult([]);
        await first;
        Assert.IsFalse(vm.IsLoadingHistory);
        Assert.IsTrue(vm.IsLoadingAuditLogs);
        Assert.IsFalse(vm.IsBusy);
        audit.SetResult([]);
        await second;
        StringAssert.Contains(vm.HistoryStatus, "Brak zapisanych");
        StringAssert.Contains(vm.AuditLogStatus, "Brak zapisanych");
    }

    [TestMethod]
    public async Task FailedRefresh_PreservesListsAndSelection_AndAllowsRetry()
    {
        var service = new Services
        {
            History = Task.FromException<List<InteractionHistoryItem>>(new InvalidOperationException("test")),
            Audit = Task.FromException<List<AuditLogItem>>(new InvalidOperationException("test"))
        };
        var vm = Create(service);
        vm.InteractionHistory.Add(new InteractionHistoryItem { Id = 1 });
        var selected = new AuditLogItem { Id = 2, DetailsJson = "{\"test\":true}" };
        vm.AuditLogs.Add(selected);
        vm.SelectedAuditLog = selected;
        await vm.LoadHistoryCommand.ExecuteAsync(null);
        await vm.LoadAuditLogsCommand.ExecuteAsync(null);
        Assert.AreEqual(1, vm.InteractionHistory.Count);
        Assert.AreSame(selected, vm.SelectedAuditLog);
        Assert.AreEqual(1, vm.AuditLogs.Count);
        StringAssert.Contains(vm.HistoryStatus, "Spróbuj ponownie");
        StringAssert.Contains(vm.AuditLogStatus, "Spróbuj ponownie");
        Assert.IsTrue(vm.LoadHistoryCommand.CanExecute(null));
        Assert.IsTrue(vm.LoadAuditLogsCommand.CanExecute(null));
        service.History = Task.FromResult(new List<InteractionHistoryItem>());
        service.Audit = Task.FromResult(new List<AuditLogItem>());
        await vm.LoadHistoryCommand.ExecuteAsync(null);
        await vm.LoadAuditLogsCommand.ExecuteAsync(null);
        Assert.AreEqual(0, vm.InteractionHistory.Count);
        Assert.AreEqual(0, vm.AuditLogs.Count);
        Assert.IsNull(vm.SelectedAuditLog);
        Assert.AreEqual("Brak szczegółów.", vm.SelectedAuditLogDetails);
    }

    [TestMethod]
    public async Task AuditRefresh_PreservesSelectionById_AndHandlesNonJsonDetails()
    {
        var previous = new AuditLogItem { Id = 2 };
        var updated = new AuditLogItem { Id = 2, EventType = "Test", DetailsJson = "legacy text" };
        var service = new Services
        {
            Audit = Task.FromResult(new List<AuditLogItem> { new() { Id = 3 }, updated })
        };
        var vm = Create(service);
        vm.AuditLogs.Add(previous);
        vm.SelectedAuditLog = previous;
        await vm.LoadAuditLogsCommand.ExecuteAsync(null);
        Assert.AreSame(updated, vm.SelectedAuditLog);
        Assert.AreEqual("legacy text", vm.SelectedAuditLogDetails);
    }

    private static MainViewModel Create(Services s) => new(
        null!, null!, null!, null!, null!, null!, s, s, null!, null!, null!, null!, null!);

    private sealed class Services : IInteractionHistoryService, IAuditLogService
    {
        public Task<List<InteractionHistoryItem>> History { get; set; } = Task.FromResult(new List<InteractionHistoryItem>());
        public Task<List<AuditLogItem>> Audit { get; set; } = Task.FromResult(new List<AuditLogItem>());
        public int HistoryCalls { get; private set; }
        public int AuditCalls { get; private set; }
        public Task<List<InteractionHistoryItem>> GetRecentHistoryAsync(int limit = 20) { HistoryCalls++; return History; }
        public Task SaveInteractionCheckAsync(IReadOnlyCollection<ActiveSubstanceItem> substances, IReadOnlyCollection<InteractionResult> results) => Task.CompletedTask;
        public Task AddAsync(string eventType, string? details = null) => Task.CompletedTask;
        public Task WriteAsync(string eventType, string? details = null) => Task.CompletedTask;
        public Task WriteAsync(string eventType, object? details) => Task.CompletedTask;
        public Task<List<AuditLogItem>> GetRecentLogsAsync(int limit = 50) { AuditCalls++; return Audit; }
        public Task<List<AuditLogItem>> GetRecentAsync(int limit = 50) => GetRecentLogsAsync(limit);
    }
}
