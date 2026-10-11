using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Features.ChPLNavigator;
using System.Collections.ObjectModel;
using DrugCompare.Presentation;

namespace DrugCompare.Features.PolishRegistry;

public sealed partial class PolishDrugRegistryViewModel : ObservableObject
{
    private readonly IPolishDrugRegistryService _polishDrugRegistryService;
    private readonly ChPLNavigatorViewModel _chplNavigator;
    private readonly WebDocumentLauncher _documentLauncher;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private PolishDrugRegistryItem? selectedResult;

    [ObservableProperty]
    private string statusMessage = "Gotowe.";

    [ObservableProperty]
    private bool isBusy;
    public bool CanWork => !IsBusy;
    public bool IsWorking => IsBusy;
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanWork));
        OnPropertyChanged(nameof(IsWorking));
        SearchCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<PolishDrugRegistryItem> Results { get; } = new();
    
    private void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusMessage = "Brak dostępnego linku.";

            return;
        }

        try
        {
            _documentLauncher.Open(url);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się otworzyć linku: {ex.Message}";
        }
    }

    public PolishDrugRegistryViewModel(
        IPolishDrugRegistryService polishDrugRegistryService,
        ChPLNavigatorViewModel chplNavigator,
        WebDocumentLauncher? documentLauncher = null)
    {
        _polishDrugRegistryService = polishDrugRegistryService;
        _chplNavigator = chplNavigator;
        _documentLauncher = documentLauncher ?? new WebDocumentLauncher();
    }
    [RelayCommand]
    private void OpenChpl()
    {
        OpenUrl(SelectedResult?.ChplUrl);
    }

    [RelayCommand]
    private void OpenLeaflet()
    {
        OpenUrl(SelectedResult?.LeafletUrl);
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task SearchAsync()
    {
        await SearchAsync(SearchText);
    }

    [RelayCommand]
    private void SendToChplNavigator()
    {
        if (SelectedResult is null)
        {
            StatusMessage = "Najpierw wybierz produkt z wyników.";
            return;
        }

        _chplNavigator.SetProductContext(SelectedResult);
        StatusMessage = "Produkt przekazano do ChPL Navigator. Otwórz moduł ChPL Navigator i wybierz plik PDF.";
    }

    public async Task SearchAsync(string query)
    {
        if (!CanWork) return;
        try
        {
            IsBusy = true;
            SearchText = query;
            StatusMessage = "Wyszukiwanie w Polskim Rejestrze Produktów Leczniczych...";

            Results.Clear();
            SelectedResult = null;

            var items = await _polishDrugRegistryService.SearchAsync(query, limit: 100);

            foreach (var item in items)
            {
                Results.Add(item);
            }

            SelectedResult = Results.FirstOrDefault();

            StatusMessage = Results.Count == 0 ? "Nie znaleziono produktów. Zmień nazwę lub użyj jej fragmentu." : $"Znaleziono {Results.Count} produktów.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd wyszukiwania w rejestrze: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private void Clear()
    {
        if (!CanWork) return;
        SearchText = string.Empty;
        Results.Clear();
        SelectedResult = null;
        StatusMessage = "Wyczyszczono.";
    }
}
