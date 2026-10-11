using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Features.IcdLooker;
using DrugCompare.Features.PolishRegistry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class SearchStateTests
{
    [TestMethod]
    public async Task Registry_RejectsRepeatedSearchAndClearWhileBusy()
    {
        var pending = new TaskCompletionSource<List<PolishDrugRegistryItem>>();
        var service = new RegistryService { Result = pending.Task };
        var vm = new PolishDrugRegistryViewModel(service, null!);
        var first = vm.SearchAsync("pierwsze");
        Assert.IsTrue(vm.IsWorking);
        Assert.IsFalse(vm.SearchCommand.CanExecute(null));
        Assert.IsFalse(vm.ClearCommand.CanExecute(null));
        await vm.SearchAsync("drugie");
        Assert.AreEqual(1, service.Calls);
        Assert.AreEqual("pierwsze", vm.SearchText);
        pending.SetResult([]);
        await first;
        Assert.IsTrue(vm.CanWork);
        StringAssert.Contains(vm.StatusMessage, "Nie znaleziono");
    }

    [TestMethod]
    public async Task Registry_FailureReleasesControls_AndAllowsRetry()
    {
        var service = new RegistryService { Result = Task.FromException<List<PolishDrugRegistryItem>>(new InvalidOperationException("test error")) };
        var vm = new PolishDrugRegistryViewModel(service, null!);
        await vm.SearchAsync("test");
        Assert.IsTrue(vm.CanWork);
        StringAssert.Contains(vm.StatusMessage, "Błąd wyszukiwania");
        service.Result = Task.FromResult(new List<PolishDrugRegistryItem>());
        await vm.SearchAsync("retry");
        Assert.AreEqual(2, service.Calls);
    }

    [TestMethod]
    public async Task Icd_LoadingCategoriesBlocksSearch_ThenReleasesControls()
    {
        var pending = new TaskCompletionSource<List<string>>();
        var service = new IcdService { Categories = pending.Task };
        var vm = new IcdLookerViewModel(service);
        Assert.IsTrue(vm.IsLoadingCategories);
        Assert.IsFalse(vm.SearchCommand.CanExecute(null));
        pending.SetResult(["Dział testowy"]);
        // The awaited task has an inline continuation in this fixture.
        Assert.IsTrue(vm.CanWork);
        Assert.AreEqual("All", vm.SelectedCategory);
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.AreEqual(1, service.Calls);
    }

    [TestMethod]
    public async Task Icd_SearchLocksClear_AndReleasesStateAfterFailure()
    {
        var pending = new TaskCompletionSource<List<IcdCodeItem>>();
        var service = new IcdService { Result = pending.Task };
        var vm = new IcdLookerViewModel(service);
        var operation = vm.SearchCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsBusy);
        Assert.IsFalse(vm.ClearCommand.CanExecute(null));
        Assert.IsFalse(vm.SearchCommand.CanExecute(null));
        pending.SetException(new InvalidOperationException("test failure"));
        await operation;
        Assert.IsTrue(vm.CanWork);
        StringAssert.Contains(vm.StatusMessage, "Spróbuj ponownie");
    }

    [TestMethod]
    public async Task Icd_CategoryFailureStillAllowsUnfilteredSearch()
    {
        var service = new IcdService { Categories = Task.FromException<List<string>>(new InvalidOperationException("test failure")) };
        var vm = new IcdLookerViewModel(service);
        Assert.IsTrue(vm.CanWork);
        StringAssert.Contains(vm.StatusMessage, "bez filtra");
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.IsNull(service.Filter);
        StringAssert.Contains(vm.StatusMessage, "Nie znaleziono");
    }

    private sealed class RegistryService : IPolishDrugRegistryService
    {
        public int Calls { get; private set; }
        public Task<List<PolishDrugRegistryItem>> Result { get; set; } = Task.FromResult(new List<PolishDrugRegistryItem>());
        public Task<List<PolishDrugRegistryItem>> SearchAsync(string query, int limit = 100) { Calls++; return Result; }
    }

    private sealed class IcdService : IIcdCodeService
    {
        public int Calls { get; private set; }
        public string? Filter { get; private set; }
        public Task<List<string>> Categories { get; set; } = Task.FromResult(new List<string>());
        public Task<List<IcdCodeItem>> Result { get; set; } = Task.FromResult(new List<IcdCodeItem>());
        public Task<List<string>> GetCategoriesAsync() => Categories;
        public Task<List<IcdCodeItem>> SearchAsync(string query, string? chapterFilter, int limit = 100) => SearchCodesAsync(query, chapterFilter, limit);
        public Task<List<IcdCodeItem>> SearchCodesAsync(string query, string? chapterFilter, int limit = 100)
        { Calls++; Filter = chapterFilter; return Result; }
    }
}
