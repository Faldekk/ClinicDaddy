using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace DrugCompare.Features.PolishRegistry;

public sealed class PolishDrugRegistryViewModel : ObservableObject
{
    private readonly IPolishDrugRegistryService _polishDrugRegistryService;
    private readonly IAuditLogService _auditLogService;

    private string _polishDrugRegistryQuery = string.Empty;
    private PolishDrugRegistryItem? _selectedPolishDrugRegistryItem;
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public PolishDrugRegistryViewModel(
        IPolishDrugRegistryService polishDrugRegistryService,
        IAuditLogService auditLogService)
    {
        _polishDrugRegistryService = polishDrugRegistryService;
        _auditLogService = auditLogService;

        SearchPolishDrugRegistryCommand = new AsyncRelayCommand(SearchPolishDrugRegistryAsync);
        OpenSelectedChplCommand = new RelayCommand(OpenSelectedChpl, CanOpenSelectedChpl);
        OpenSelectedLeafletCommand = new RelayCommand(OpenSelectedLeaflet, CanOpenSelectedLeaflet);
    }

    public string PolishDrugRegistryQuery
    {
        get => _polishDrugRegistryQuery;
        set => SetProperty(ref _polishDrugRegistryQuery, value);
    }

    public PolishDrugRegistryItem? SelectedPolishDrugRegistryItem
    {
        get => _selectedPolishDrugRegistryItem;
        set
        {
            if (SetProperty(ref _selectedPolishDrugRegistryItem, value))
            {
                OpenSelectedChplCommand.NotifyCanExecuteChanged();
                OpenSelectedLeafletCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<PolishDrugRegistryItem> PolishDrugRegistryResults { get; } = new();

    public IAsyncRelayCommand SearchPolishDrugRegistryCommand { get; }
    public IRelayCommand OpenSelectedChplCommand { get; }
    public IRelayCommand OpenSelectedLeafletCommand { get; }

    private async Task SearchPolishDrugRegistryAsync()
    {
        PolishDrugRegistryResults.Clear();
        SelectedPolishDrugRegistryItem = null;

        if (string.IsNullOrWhiteSpace(PolishDrugRegistryQuery))
        {
            StatusMessage = "Enter Polish drug name, active substance, or authorization number.";
            return;
        }

        var query = PolishDrugRegistryQuery.Trim();

        IsBusy = true;
        StatusMessage = "Searching Polish Drug Registry...";

        try
        {
            var results = await _polishDrugRegistryService.SearchAsync(query, 100);

            foreach (var item in results)
            {
                PolishDrugRegistryResults.Add(item);
            }

            SelectedPolishDrugRegistryItem = PolishDrugRegistryResults.FirstOrDefault();

            StatusMessage = $"Found {PolishDrugRegistryResults.Count} Polish registry record(s).";

            await SafeAuditAsync("PolishDrugRegistrySearched", new
            {
                Query = query,
                ResultCount = PolishDrugRegistryResults.Count,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Polish registry search failed: {ex.Message}";

            await SafeAuditAsync("PolishDrugRegistrySearchFailed", new
            {
                Query = query,
                Error = ex.Message,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanOpenSelectedChpl()
    {
        return !string.IsNullOrWhiteSpace(SelectedPolishDrugRegistryItem?.ChplUrl);
    }

    private bool CanOpenSelectedLeaflet()
    {
        return !string.IsNullOrWhiteSpace(SelectedPolishDrugRegistryItem?.LeafletUrl);
    }

    private void OpenSelectedChpl()
    {
        OpenUrl(SelectedPolishDrugRegistryItem?.ChplUrl);
    }

    private void OpenSelectedLeaflet()
    {
        OpenUrl(SelectedPolishDrugRegistryItem?.LeafletUrl);
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private async Task SafeAuditAsync(string eventType, object details)
    {
        try
        {
            await _auditLogService.WriteAsync(eventType, details);
        }
        catch
        {
            // Audit log is non-critical in the current version.
        }
    }
}
