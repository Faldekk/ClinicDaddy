using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;

namespace DrugCompare.Features.IcdLooker;

public sealed partial class IcdLookerViewModel : ObservableObject
{
    private readonly IIcdCodeService _icdCodeService;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string? selectedCategory;

    [ObservableProperty]
    private IcdCodeItem? selectedResult;

    [ObservableProperty]
    private string statusMessage = "Gotowe.";

    [ObservableProperty]
    private bool isBusy;
    [ObservableProperty]
    private bool isLoadingCategories;
    public bool CanWork => !IsWorking;
    public bool IsWorking => IsBusy || IsLoadingCategories;
    partial void OnIsBusyChanged(bool value) => UpdateCommands();
    partial void OnIsLoadingCategoriesChanged(bool value) => UpdateCommands();
    private void UpdateCommands()
    {
        OnPropertyChanged(nameof(CanWork));
        OnPropertyChanged(nameof(IsWorking));
        SearchCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<IcdCodeItem> Results { get; } = new();

    public ObservableCollection<string> Categories { get; } = new();

    public IcdLookerViewModel(IIcdCodeService icdCodeService)
    {
        _icdCodeService = icdCodeService;
        _ = LoadCategoriesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task SearchAsync()
    {
        if (!CanWork) return;
        try
        {
            IsBusy = true;
            StatusMessage = "Wyszukiwanie kodów ICD...";

            Results.Clear();
            SelectedResult = null;

            var chapterFilter =
                string.IsNullOrWhiteSpace(SelectedCategory) ||
                SelectedCategory.Equals("All", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : SelectedCategory;

            var items = await _icdCodeService.SearchCodesAsync(
                SearchText,
                chapterFilter,
                limit: 100);

            foreach (var item in items)
            {
                Results.Add(item);
            }

            SelectedResult = Results.FirstOrDefault();

            StatusMessage = Results.Count == 0 ? "Nie znaleziono kodów. Zmień zapytanie lub dział." : $"Znaleziono {Results.Count} kodów ICD.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się wyszukać kodów ICD. Spróbuj ponownie. Szczegóły: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadCategoriesAsync()
    {
        try
        {
            IsLoadingCategories = true;
            StatusMessage = "Ładowanie działów ICD...";
            Categories.Clear();
            Categories.Add("All");

            var categories = await _icdCodeService.GetCategoriesAsync();

            foreach (var category in categories)
            {
                if (!string.IsNullOrWhiteSpace(category))
                {
                    Categories.Add(category);
                }
            }

            SelectedCategory = "All";
            StatusMessage = "Gotowe.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się załadować działów ICD. Możesz wyszukać bez filtra. Szczegóły: {ex.Message}";
        }
        finally { IsLoadingCategories = false; }
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private void Clear()
    {
        if (!CanWork) return;
        SearchText = string.Empty;
        SelectedCategory = "All";
        Results.Clear();
        SelectedResult = null;
        StatusMessage = "Wyczyszczono.";
    }
}
