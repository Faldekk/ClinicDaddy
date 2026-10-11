using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Features.IcdLooker;
using DrugCompare.Features.InteractionChecker;
using DrugCompare.Features.ChPLNavigator;
using DrugCompare.Features.PolishRegistry;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using System.Collections.ObjectModel;
using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.ViewModels.Interaction;
using DrugCompare.Features.EvidenceAssistant;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;

namespace DrugCompare.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDatabaseStatusService _databaseStatusService;
    private readonly IDataManagementService _dataManagementService;
    private readonly IInteractionHistoryService _interactionHistoryService;
    private readonly IAuditLogService _auditLogService;
    private readonly IKnowledgeBaseStatsService _knowledgeBaseStatsService;
    private readonly IKnowledgeBaseSearchService _knowledgeBaseSearchService;
    private readonly ILocalDatabaseBackupService _databaseBackupService;
    private readonly IChplBatchImportService _chplBatchImportService;

    private string _databaseStatusText = "Database status not loaded.";
    private string _emaImportSummary = "EMA import status not loaded.";
    private string _ddinterImportSummary = "DDInter import status not loaded.";
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    private AuditLogItem? _selectedAuditLog;
    private string _selectedAuditLogDetails = "Wybierz zdarzenie, aby zobaczyć szczegóły.";

    public MainViewModel(
    InteractionCheckerViewModel interactionChecker,
    IcdLookerViewModel icdLooker,
    PolishDrugRegistryViewModel polishDrugRegistry,
    ChPLNavigatorViewModel chPLNavigator,
    IDatabaseStatusService databaseStatusService,
    IDataManagementService dataManagementService,
    IInteractionHistoryService interactionHistoryService,
    IAuditLogService auditLogService, EvidenceAssistantViewModel evidenceAssistant,
    IKnowledgeBaseStatsService knowledgeBaseStatsService,
    IKnowledgeBaseSearchService knowledgeBaseSearchService,
    ILocalDatabaseBackupService databaseBackupService,
    IChplBatchImportService chplBatchImportService)
    {
        InteractionChecker = interactionChecker;
        IcdLooker = icdLooker;
        PolishDrugRegistry = polishDrugRegistry;
        ChPLNavigator = chPLNavigator;
        EvidenceAssistant = evidenceAssistant;

        _databaseStatusService = databaseStatusService;
        _dataManagementService = dataManagementService;
        _interactionHistoryService = interactionHistoryService;
        _auditLogService = auditLogService;
        _knowledgeBaseStatsService = knowledgeBaseStatsService;
        _knowledgeBaseSearchService = knowledgeBaseSearchService;
        _databaseBackupService = databaseBackupService;
        _chplBatchImportService = chplBatchImportService;


        LoadDatabaseStatusCommand = new AsyncRelayCommand(LoadDatabaseStatusAsync);
        LoadDataManagementCommand = new AsyncRelayCommand(LoadDataManagementAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync, () => !IsLoadingHistory);
        LoadAuditLogsCommand = new AsyncRelayCommand(LoadAuditLogsAsync, () => !IsLoadingAuditLogs);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadHistoryCommand))]
    private bool isLoadingHistory;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadAuditLogsCommand))]
    private bool isLoadingAuditLogs;

    [ObservableProperty]
    private string historyStatus = "Historia nie została jeszcze wczytana.";

    [ObservableProperty]
    private string auditLogStatus = "Dziennik nie został jeszcze wczytany.";

    [ObservableProperty]
    private int chplDocumentCount;

    [ObservableProperty]
    private int chplSectionCount;

    [ObservableProperty]
    private int knowledgeChunkCount;

    [ObservableProperty]
    private int needsReviewChunkCount;

    [ObservableProperty]
    private int reviewedChunkCount;

    [ObservableProperty]
    private string knowledgeSearchQuery = string.Empty;

    [ObservableProperty]
    private string knowledgeSearchStatus = "Gotowe.";

    public ObservableCollection<KnowledgeSearchResult> KnowledgeSearchResults { get; } = new();
    [ObservableProperty]
    private int verifiedChunkCount;
    public InteractionCheckerViewModel InteractionChecker { get; }

    public IcdLookerViewModel IcdLooker { get; }

    public ChPLNavigatorViewModel ChPLNavigator { get; }

    public PolishDrugRegistryViewModel PolishDrugRegistry { get; }

    public EvidenceAssistantViewModel EvidenceAssistant { get; }

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
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ImportChplBatchCommand.NotifyCanExecuteChanged();
                RetryChplImportCommand.NotifyCanExecuteChanged();
                RestoreDatabaseBackupCommand.NotifyCanExecuteChanged();
            }
        }
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

    [RelayCommand]
    private async Task CreateDatabaseBackupAsync()
    {
        var dialog = new SaveFileDialog { Title = "Zapisz kopię bazy SQLite", Filter = "SQLite database (*.db)|*.db", FileName = $"medcompare-backup-{DateTime.Now:yyyyMMdd-HHmm}.db" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            IsBusy = true;
            await _databaseBackupService.CreateBackupAsync(dialog.FileName);
            StatusMessage = $"Utworzono kopię bazy: {dialog.FileName}";
            await SafeAuditAsync("DatabaseBackupCreated", new { Path = dialog.FileName, Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex) { StatusMessage = $"Tworzenie kopii nie powiodło się: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanImportChpl))]
    private async Task RestoreDatabaseBackupAsync()
    {
        var dialog = new OpenFileDialog { Title = "Wybierz kopię SQLite do przywrócenia", Filter = "SQLite database (*.db)|*.db" };
        if (dialog.ShowDialog() != true) return;
        var confirmation = MessageBox.Show("Aktualna baza zostanie zastąpiona. Przed podmianą aplikacja utworzy automatyczną kopię bezpieczeństwa. Kontynuować?", "Przywracanie bazy", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;
        try
        {
            IsBusy = true;
            var safetyCopy = await _databaseBackupService.RestoreAsync(dialog.FileName);
            StatusMessage = $"Przywrócono bazę. Kopia sprzed przywrócenia: {safetyCopy}. Uruchom aplikację ponownie.";
        }
        catch (Exception ex) { StatusMessage = $"Przywracanie nie powiodło się: {ex.Message}"; }
        finally { IsBusy = false; }
    }
    [RelayCommand]
    private async Task SearchKnowledgeBaseAsync()
    {
        KnowledgeSearchResults.Clear();

        if (string.IsNullOrWhiteSpace(KnowledgeSearchQuery))
        {
            KnowledgeSearchStatus = "Wpisz zapytanie.";
            return;
        }

        var results = await _knowledgeBaseSearchService.SearchAsync(
            KnowledgeSearchQuery,
            limit: 20);

        foreach (var result in results)
        {
            KnowledgeSearchResults.Add(result);
        }

        KnowledgeSearchStatus = results.Count == 0
            ? "Nie znaleziono wyników w Knowledge Base."
            : $"Znaleziono wyników: {results.Count}.";
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
        if (IsLoadingHistory) return;
        IsLoadingHistory = true;
        HistoryStatus = "Wczytywanie historii interakcji…";

        try
        {
            var items = await _interactionHistoryService.GetRecentHistoryAsync(20);

            InteractionHistory.Clear();

            foreach (var item in items)
            {
                InteractionHistory.Add(item);
            }

            HistoryStatus = InteractionHistory.Count == 0
                ? "Brak zapisanych sprawdzeń interakcji."
                : $"Wczytane sprawdzenia: {InteractionHistory.Count}.";
        }
        catch (Exception ex)
        {
            HistoryStatus = $"Nie udało się odświeżyć historii: {ex.Message}. Poprzednia lista pozostaje bez zmian. Spróbuj ponownie.";
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    private async Task LoadAuditLogsAsync()
    {
        if (IsLoadingAuditLogs) return;
        IsLoadingAuditLogs = true;
        AuditLogStatus = "Wczytywanie dziennika zdarzeń…";

        try
        {
            var previouslySelectedId = SelectedAuditLog?.Id;

            var logs = await _auditLogService.GetRecentAsync(100);

            AuditLogs.Clear();

            foreach (var log in logs)
            {
                AuditLogs.Add(log);
            }

            SelectedAuditLog =
                AuditLogs.FirstOrDefault(x => x.Id == previouslySelectedId)
                ?? AuditLogs.FirstOrDefault();

            AuditLogStatus = AuditLogs.Count == 0
                ? "Brak zapisanych zdarzeń."
                : $"Wczytane zdarzenia: {AuditLogs.Count}.";
        }
        catch (Exception ex)
        {
            AuditLogStatus = $"Nie udało się odświeżyć dziennika: {ex.Message}. Poprzednia lista pozostaje bez zmian. Spróbuj ponownie.";
        }
        finally
        {
            IsLoadingAuditLogs = false;
        }
    }

    private static string FormatAuditLogDetails(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return "Brak szczegółów.";
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
    [RelayCommand]
    private async Task LoadKnowledgeBaseStatsAsync()
    {
        var stats = await _knowledgeBaseStatsService.GetStatsAsync();

        ChplDocumentCount = stats.ChplDocumentCount;
        ChplSectionCount = stats.ChplSectionCount;
        KnowledgeChunkCount = stats.KnowledgeChunkCount;
        NeedsReviewChunkCount = stats.NeedsReviewChunkCount;
        ReviewedChunkCount = stats.ReviewedChunkCount;
        VerifiedChunkCount = stats.VerifiedChunkCount;
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
