using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Features.IcdLooker;
using DrugCompare.Features.InteractionChecker;
using DrugCompare.Features.ChPLNavigator;
using DrugCompare.Features.PolishRegistry;
using DrugCompare.ViewModels.Interaction;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace DrugCompare.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDatabaseStatusService _databaseStatusService;
    private readonly IDataManagementService _dataManagementService;
    private readonly IInteractionHistoryService _interactionHistoryService;
    private readonly IAuditLogService _auditLogService;

    private string _databaseStatusText = "Database status not loaded.";
    private string _emaImportSummary = "EMA import status not loaded.";
    private string _ddinterImportSummary = "DDInter import status not loaded.";
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    private AuditLogItem? _selectedAuditLog;
    private string _selectedAuditLogDetails = "Select audit log entry to inspect details.";

    public MainViewModel(
    InteractionCheckerViewModel interactionChecker,
    IcdLookerViewModel icdLooker,
    PolishDrugRegistryViewModel polishDrugRegistry,
    ChPLNavigatorViewModel chPLNavigator,
    IDatabaseStatusService databaseStatusService,
    IDataManagementService dataManagementService,
    IInteractionHistoryService interactionHistoryService,
    IAuditLogService auditLogService)
    {
        InteractionChecker = interactionChecker;
        IcdLooker = icdLooker;
        PolishDrugRegistry = polishDrugRegistry;
        ChPLNavigator = chPLNavigator;

        _databaseStatusService = databaseStatusService;
        _dataManagementService = dataManagementService;
        _interactionHistoryService = interactionHistoryService;
        _auditLogService = auditLogService;

        LoadDatabaseStatusCommand = new AsyncRelayCommand(LoadDatabaseStatusAsync);
        LoadDataManagementCommand = new AsyncRelayCommand(LoadDataManagementAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
        LoadAuditLogsCommand = new AsyncRelayCommand(LoadAuditLogsAsync);
    }

    public InteractionCheckerViewModel InteractionChecker { get; }

    public IcdLookerViewModel IcdLooker { get; }

    public ChPLNavigatorViewModel ChPLNavigator { get; }

    public PolishDrugRegistryViewModel PolishDrugRegistry { get; }

    public string DatabaseStatusText
    {
        get => _databaseStatusText;
        set => SetProperty(ref _databaseStatusText, value);
    }

    public string EmaImportSummary
    {
        get => _emaImportSummary;
        set => SetProperty(ref _emaImportSummary, value);
    }

    public string DdinterImportSummary
    {
        get => _ddinterImportSummary;
        set => SetProperty(ref _ddinterImportSummary, value);
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

    public AuditLogItem? SelectedAuditLog
    {
        get => _selectedAuditLog;
        set
        {
            if (SetProperty(ref _selectedAuditLog, value))
            {
                SelectedAuditLogDetails = FormatAuditLogDetails(value?.DetailsJson);
            }
        }
    }

    public string SelectedAuditLogDetails
    {
        get => _selectedAuditLogDetails;
        set => SetProperty(ref _selectedAuditLogDetails, value);
    }

    public ObservableCollection<DataSourceVersionItem> RecentDataImports { get; } = new();

    public ObservableCollection<InteractionHistoryItem> InteractionHistory { get; } = new();

    public ObservableCollection<AuditLogItem> AuditLogs { get; } = new();

    public IAsyncRelayCommand LoadDatabaseStatusCommand { get; }

    public IAsyncRelayCommand LoadDataManagementCommand { get; }

    public IAsyncRelayCommand LoadHistoryCommand { get; }

    public IAsyncRelayCommand LoadAuditLogsCommand { get; }

    public async Task<DatabaseStatusResult> GetDatabaseStatusForStartupAsync()
    {
        return await _databaseStatusService.GetDatabaseStatusAsync();
    }

    private async Task LoadDatabaseStatusAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading database status...";

        try
        {
            var status = await _databaseStatusService.GetDatabaseStatusAsync();

            DatabaseStatusText =
                $"Drugs: {status.DrugsCount:N0} | " +
                $"Active substances: {status.ActiveSubstancesCount:N0} | " +
                $"Relations: {status.DrugActiveSubstancesCount:N0} | " +
                $"Interactions: {status.SubstanceInteractionsCount:N0}";

            StatusMessage = "Database status loaded.";

            await SafeAuditAsync("DatabaseStatusViewed", new
            {
                status.DrugsCount,
                status.ActiveSubstancesCount,
                status.DrugActiveSubstancesCount,
                status.SubstanceInteractionsCount,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            DatabaseStatusText = "Database status unavailable.";
            StatusMessage = $"Database status failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDataManagementAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading data management status...";

        try
        {
            var result = await _dataManagementService.GetDataManagementStatusAsync();

            EmaImportSummary = BuildImportSummary("EMA", result.LatestEmaImport);
            DdinterImportSummary = BuildImportSummary("DDInter", result.LatestDdinterImport);

            RecentDataImports.Clear();

            foreach (var item in result.RecentImports)
            {
                RecentDataImports.Add(item);
            }

            StatusMessage = "Data management status loaded.";
        }
        catch (Exception ex)
        {
            EmaImportSummary = "EMA import status unavailable.";
            DdinterImportSummary = "DDInter import status unavailable.";
            StatusMessage = $"Data management loading failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildImportSummary(string sourceName, DataSourceVersionItem? item)
    {
        if (item is null)
        {
            return $"{sourceName}: no import record found.";
        }

        return
            $"{sourceName}: {item.ImportStatus} | " +
            $"File: {item.FileName} | " +
            $"Records: {item.RecordsImported:N0} | " +
            $"Imported: {item.ImportedAt:yyyy-MM-dd HH:mm}";
    }

    private async Task LoadHistoryAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading interaction history...";

        try
        {
            InteractionHistory.Clear();

            var items = await _interactionHistoryService.GetRecentHistoryAsync(20);

            foreach (var item in items)
            {
                InteractionHistory.Add(item);
            }

            StatusMessage = $"Loaded {InteractionHistory.Count} history item(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Loading history failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAuditLogsAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading audit logs...";

        try
        {
            var previouslySelectedId = SelectedAuditLog?.Id;

            AuditLogs.Clear();

            var logs = await _auditLogService.GetRecentAsync(100);

            foreach (var log in logs)
            {
                AuditLogs.Add(log);
            }

            SelectedAuditLog =
                AuditLogs.FirstOrDefault(x => x.Id == previouslySelectedId)
                ?? AuditLogs.FirstOrDefault();

            StatusMessage = $"Loaded {AuditLogs.Count} audit log entries.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Loading audit logs failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatAuditLogDetails(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return "No details.";
        }

        try
        {
            using var document = JsonDocument.Parse(detailsJson);

            return JsonSerializer.Serialize(
                document.RootElement,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }
        catch
        {
            return detailsJson;
        }
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