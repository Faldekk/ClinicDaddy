using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Models;
using DrugCompare.Services;
using DrugCompare.Services.Contracts;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Text.Json;
using DrugCompare.Services.Application;
using System.IO;
using System.Diagnostics;

namespace DrugCompare.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDrugLookupService _drugLookupService;
    private readonly ISubstanceLookupService _substanceLookupService;
    private readonly IInteractionCheckerService _interactionCheckerService;
    private readonly IDatabaseStatusService _databaseStatusService;
    private readonly IInteractionHistoryService _interactionHistoryService;
    private readonly IDrugExplorerService _drugExplorerService;
    private readonly IDataManagementService _dataManagementService;
    private readonly IPolishDrugRegistryService _polishDrugRegistryService;
    private readonly IIcdCodeService _icdCodeService;
    private readonly InteractionAnalysisService _interactionAnalysisService;
    private string _drugNameInput = string.Empty;
    private string _manualSubstanceInput = string.Empty;
    private ActiveSubstanceItem? _selectedDetectedSubstance;
    private ActiveSubstanceItem? _selectedAcceptedSubstance;
    private InteractionResult? _selectedInteraction;
    private readonly IAuditLogService _auditLogService;
    private AuditLogItem? _selectedAuditLog;
    private string _polishDrugRegistryQuery = "";
    private PolishDrugRegistryItem? _selectedPolishDrugRegistryItem;
    private string _selectedIcdCategory = "All";
    private string _selectedAuditLogDetails = "Select audit log entry to inspect details.";
    private string _resultSummaryMessage = "No interaction check performed yet.";
    private string _statusMessage = "Ready.";
    private string _emaImportSummary = "EMA import status not loaded.";
    private string _ddinterImportSummary = "DDInter import status not loaded.";
    private bool _isBusy;
    private string _databaseStatusText = "Database status not loaded.";
    private string _drugExplorerQuery = "";
    private string _icdSearchQuery = "";
    private IcdCodeItem? _selectedIcdCode;
    private DrugExplorerResult? _selectedDrugExplorerResult;
    public ObservableCollection<InteractionHistoryItem> InteractionHistory { get; } = new();
    public AsyncRelayCommand ExportCurrentReportCommand { get; }
    public ObservableCollection<AuditLogItem> AuditLogs { get; } = new();

    public MainViewModel(
    IDrugLookupService drugLookupService,
    ISubstanceLookupService substanceLookupService,
    IInteractionCheckerService interactionCheckerService,
    IDatabaseStatusService databaseStatusService,
    IInteractionHistoryService interactionHistoryService,
    IDataManagementService dataManagementService,
    InteractionAnalysisService interactionAnalysisService,
    IAuditLogService auditLogService, 
    IDrugExplorerService drugExplorerService,
    IPolishDrugRegistryService polishDrugRegistryService,
    IIcdCodeService icdCodeService)
    {
        _icdCodeService = icdCodeService;
        _drugLookupService = drugLookupService;
        _substanceLookupService = substanceLookupService;
        _interactionCheckerService = interactionCheckerService;
        _databaseStatusService = databaseStatusService;
        _interactionHistoryService = interactionHistoryService;
        _dataManagementService = dataManagementService;
        _interactionAnalysisService = interactionAnalysisService;
        _drugExplorerService = drugExplorerService;
        _polishDrugRegistryService = polishDrugRegistryService;

        FindDrugCommand = new AsyncRelayCommand(FindDrugAsync);
        AcceptDetectedSubstanceCommand = new RelayCommand(AcceptDetectedSubstance);
        AcceptAllDetectedSubstancesCommand = new RelayCommand(AcceptAllDetectedSubstances);
        AddManualSubstanceCommand = new AsyncRelayCommand(AddManualSubstanceAsync);
        RemoveAcceptedSubstanceCommand = new RelayCommand(RemoveAcceptedSubstance);
        ClearCaseCommand = new RelayCommand(ClearCase);
        CheckInteractionsCommand = new AsyncRelayCommand(CheckInteractionsAsync);
        LoadDatabaseStatusCommand = new AsyncRelayCommand(LoadDatabaseStatusAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
        LoadDataManagementCommand = new AsyncRelayCommand(LoadDataManagementAsync);
        ExportCurrentReportCommand = new AsyncRelayCommand(ExportCurrentReportAsync);
        _auditLogService = auditLogService;
        LoadAuditLogsCommand = new AsyncRelayCommand(LoadAuditLogsAsync);
        SearchDrugExplorerCommand = new AsyncRelayCommand(SearchDrugExplorerAsync);
        SearchPolishDrugRegistryCommand = new AsyncRelayCommand(SearchPolishDrugRegistryAsync);
        OpenSelectedChplCommand = new RelayCommand(OpenSelectedChpl, CanOpenSelectedChpl);
        OpenSelectedLeafletCommand = new RelayCommand(OpenSelectedLeaflet, CanOpenSelectedLeaflet);
        SearchIcdCodesCommand = new AsyncRelayCommand(SearchIcdCodesAsync);
        LoadIcdCategoriesCommand = new AsyncRelayCommand(LoadIcdCategoriesAsync);

    }
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
    public string IcdSearchQuery
    {
        get => _icdSearchQuery;
        set => SetProperty(ref _icdSearchQuery, value);
    }


    public IcdCodeItem? SelectedIcdCode
    {
        get => _selectedIcdCode;
        set => SetProperty(ref _selectedIcdCode, value);
    }
    public string DrugNameInput
    {
        get => _drugNameInput;
        set => SetProperty(ref _drugNameInput, value);
    }
    public string ResultSummaryMessage
    {
        get => _resultSummaryMessage;
        set => SetProperty(ref _resultSummaryMessage, value);
    }
    public string ManualSubstanceInput
    {
        get => _manualSubstanceInput;
        set => SetProperty(ref _manualSubstanceInput, value);
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
    public ActiveSubstanceItem? SelectedDetectedSubstance
    {
        get => _selectedDetectedSubstance;
        set => SetProperty(ref _selectedDetectedSubstance, value);
    }

    public ActiveSubstanceItem? SelectedAcceptedSubstance
    {
        get => _selectedAcceptedSubstance;
        set => SetProperty(ref _selectedAcceptedSubstance, value);
    }

    public InteractionResult? SelectedInteraction
    {
        get => _selectedInteraction;
        set => SetProperty(ref _selectedInteraction, value);
    }
 

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    public string DrugExplorerQuery
    {
        get => _drugExplorerQuery;
        set => SetProperty(ref _drugExplorerQuery, value);
    }

    public DrugExplorerResult? SelectedDrugExplorerResult
    {
        get => _selectedDrugExplorerResult;
        set => SetProperty(ref _selectedDrugExplorerResult, value);
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
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<ActiveSubstanceItem> DetectedSubstances { get; } = new();

    public ObservableCollection<ActiveSubstanceItem> AcceptedSubstances { get; } = new();
    public IAsyncRelayCommand SearchDrugExplorerCommand { get; }
    public ObservableCollection<DrugExplorerResult> DrugExplorerResults { get; } = new();
    public ObservableCollection<InteractionResult> InteractionResults { get; } = new();
    public ObservableCollection<DataSourceVersionItem> RecentDataImports { get; } = new();
    public ObservableCollection<PolishDrugRegistryItem> PolishDrugRegistryResults { get; } = new();
    public string SelectedIcdCategory
    {
        get => _selectedIcdCategory;
        set => SetProperty(ref _selectedIcdCategory, value);
    }


    public IAsyncRelayCommand SearchPolishDrugRegistryCommand { get; }
    public IRelayCommand OpenSelectedChplCommand { get; }
    public IRelayCommand OpenSelectedLeafletCommand { get; }
    public IAsyncRelayCommand FindDrugCommand { get; }

    public IAsyncRelayCommand LoadHistoryCommand { get; }
    public IAsyncRelayCommand LoadAuditLogsCommand { get; }
    public IRelayCommand AcceptDetectedSubstanceCommand { get; }
    public ObservableCollection<IcdCodeItem> IcdSearchResults { get; } = new();

    public IAsyncRelayCommand SearchIcdCodesCommand { get; }
    public IRelayCommand AcceptAllDetectedSubstancesCommand { get; }

    public IAsyncRelayCommand AddManualSubstanceCommand { get; }

    public IRelayCommand RemoveAcceptedSubstanceCommand { get; }

    public IRelayCommand ClearCaseCommand { get; }

    public IAsyncRelayCommand CheckInteractionsCommand { get; }
    public IAsyncRelayCommand LoadIcdCategoriesCommand { get; }
    public IAsyncRelayCommand LoadDatabaseStatusCommand { get; }
    public IAsyncRelayCommand LoadDataManagementCommand { get; }
    public ObservableCollection<string> IcdCategories { get; } = new()
{
    "All",
    "Choroby zakaźne i pasożytnicze",
    "Nowotwory",
    "Choroby krwi i narządów krwiotwórczych",
    "Choroby układu odpornościowego",
    "Choroby endokrynologiczne, żywieniowe lub metaboliczne",
    "Zaburzenia psychiczne, behawioralne lub neurorozwojowe",
    "Zaburzenia snu i czuwania",
    "Choroby układu nerwowego",
    "Choroby narządu wzroku",
    "Choroby ucha lub wyrostka sutkowatego",
    "Choroby układu krążenia",
    "Choroby układu oddechowego",
    "Choroby układu pokarmowego",
    "Choroby skóry",
    "Choroby układu mięśniowo-szkieletowego lub tkanki łącznej",
    "Choroby układu moczowo-płciowego",
    "Ciąża, poród lub połóg",
    "Stany rozpoczynające się w okresie okołoporodowym",
    "Wady rozwojowe",
    "Objawy, oznaki lub wyniki kliniczne niesklasyfikowane gdzie indziej",
    "Urazy, zatrucia lub inne następstwa przyczyn zewnętrznych",
    "Zewnętrzne przyczyny zachorowania lub śmierci",
    "Czynniki wpływające na stan zdrowia lub kontakt ze służbą zdrowia"
};
    public async Task<DatabaseStatusResult> GetDatabaseStatusForStartupAsync()
    {
        return await _databaseStatusService.GetDatabaseStatusAsync();
    }
    private static string BuildImportSummary(string sourceName, DataSourceVersionItem? item)
    {
        if (item is null)
            return $"{sourceName}: no import record found.";

        return
            $"{sourceName}: {item.ImportStatus} | " +
            $"File: {item.FileName} | " +
            $"Records: {item.RecordsImported:N0} | " +
            $"Imported: {item.ImportedAt:yyyy-MM-dd HH:mm}";
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
    }
    private async Task SearchPolishDrugRegistryAsync()
    {
        PolishDrugRegistryResults.Clear();
        SelectedPolishDrugRegistryItem = null;

        if (string.IsNullOrWhiteSpace(PolishDrugRegistryQuery))
        {
            StatusMessage = "Enter Polish drug name, active substance, or authorization number.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Searching Polish Drug Registry...";

        try
        {
            var results = await _polishDrugRegistryService.SearchAsync(PolishDrugRegistryQuery, 100);

            foreach (var item in results)
            {
                PolishDrugRegistryResults.Add(item);
            }

            SelectedPolishDrugRegistryItem = PolishDrugRegistryResults.FirstOrDefault();

            StatusMessage = $"Found {PolishDrugRegistryResults.Count} Polish registry record(s).";

            await _auditLogService.WriteAsync("PolishDrugRegistrySearched", new
            {
                Query = PolishDrugRegistryQuery.Trim(),
                ResultCount = PolishDrugRegistryResults.Count,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Polish registry search failed: {(ex)}";
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
    private async Task SearchDrugExplorerAsync()
    {
        DrugExplorerResults.Clear();
        SelectedDrugExplorerResult = null;

        if (string.IsNullOrWhiteSpace(DrugExplorerQuery))
        {
            StatusMessage = "Enter drug name to search.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Searching drug database...";

        try
        {
            var results = await _drugExplorerService.SearchAsync(DrugExplorerQuery, 50);

            foreach (var result in results)
            {
                DrugExplorerResults.Add(result);
            }

            SelectedDrugExplorerResult = DrugExplorerResults.FirstOrDefault();

            StatusMessage = $"Found {DrugExplorerResults.Count} drug record(s).";

            await _auditLogService.WriteAsync("DrugExplorerSearched", new
            {
                Query = DrugExplorerQuery.Trim(),
                ResultCount = DrugExplorerResults.Count,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Drug explorer search failed: {(ex)}";
        }
        finally
        {
            IsBusy = false;
        }
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
            await _auditLogService.WriteAsync("DatabaseStatsViewed", new
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
    private async Task FindDrugAsync()
    {
        DetectedSubstances.Clear();

        if (string.IsNullOrWhiteSpace(DrugNameInput))
        {
            StatusMessage = "Enter drug name.";
            return;
        }

        var searchedDrugName = DrugNameInput.Trim();

        IsBusy = true;
        StatusMessage = "Searching local drug dictionary...";

        try
        {
            var result = await _drugLookupService.FindDrugAsync(searchedDrugName);

            if (result is null)
            {
                StatusMessage = "Drug not found in local dictionary. Add active substance manually.";

                //await _auditLogService.WriteAsync("DrugSearched", new
                //{
                //    DrugName = searchedDrugName,
                //    Found = false,
                //    DetectedSubstanceCount = 0,
                //    Timestamp = DateTime.Now
                //});

                return;
            }

            foreach (var substance in result.ActiveSubstances)
            {
                DetectedSubstances.Add(substance);
            }
            //Debug part for SQLite
            StatusMessage =
            $"Found {result.ActiveSubstances.Count} active substance(s) for {result.DrugName}. " +
            $"IDs: {string.Join(", ", result.ActiveSubstances.Select(x => $"{x.Name}:{x.DatabaseId?.ToString() ?? "NULL"}"))}";
            //StatusMessage = $"Found {result.ActiveSubstances.Count} active substance(s) for {result.DrugName}.";

            //await _auditLogService.WriteAsync("DrugSearched", new
            //{
            //    DrugName = searchedDrugName,
            //    Found = true,
            //    ResultDrugName = result.DrugName,
            //    DetectedSubstanceCount = result.ActiveSubstances.Count,
            //    DetectedSubstances = result.ActiveSubstances.Select(x => new
            //    {
            //        x.Name,
            //        x.DatabaseId,
            //        x.DDInterId,
            //        x.Source
            //    }).ToList(),
            //    Timestamp = DateTime.Now
            //});
        }
        catch (Exception ex)
        {
            StatusMessage = $"Drug lookup failed: {ex.Message}";

            await _auditLogService.WriteAsync("DrugSearchFailed", new
            {
                DrugName = searchedDrugName,
                Error = ex.Message,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
   
    private void AcceptDetectedSubstance()
       {
        if (SelectedDetectedSubstance is null)
        {
            StatusMessage = "Select detected active substance first.";
            return;
        }

        var substance = SelectedDetectedSubstance;

        AddAcceptedSubstance(substance);

        DetectedSubstances.Remove(substance);
        SelectedDetectedSubstance = null;
    }

    private void AcceptAllDetectedSubstances()
    {
        if (DetectedSubstances.Count == 0)
        {
            StatusMessage = "No detected substances to accept.";
            return;
        }

        var addedCount = 0;

        foreach (var substance in DetectedSubstances)
        {
            var alreadyExists = AcceptedSubstances.Any(x =>
                x.DatabaseId == substance.DatabaseId &&
                string.Equals(x.NormalizedName, substance.NormalizedName, StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                continue;
            }

            AcceptedSubstances.Add(new ActiveSubstanceItem
            {
                DatabaseId = substance.DatabaseId,
                Name = substance.Name,
                NormalizedName = substance.NormalizedName,
                DDInterId = substance.DDInterId,
                Source = substance.Source
            });

            addedCount++;
        }

        DetectedSubstances.Clear();

        StatusMessage = $"Accepted {addedCount} substance(s).";
    }
    private void AcceptSelectedDetectedSubstance()
{
    if (SelectedDetectedSubstance is null)
    {
        StatusMessage = "Select a detected substance first.";
        return;
    }

    var substance = SelectedDetectedSubstance;

    var alreadyExists = AcceptedSubstances.Any(x =>
        x.DatabaseId == substance.DatabaseId &&
        string.Equals(x.NormalizedName, substance.NormalizedName, StringComparison.OrdinalIgnoreCase));

    if (alreadyExists)
    {
        StatusMessage = "This substance is already accepted.";
        return;
    }

    AcceptedSubstances.Add(new ActiveSubstanceItem
    {
        DatabaseId = substance.DatabaseId,
        Name = substance.Name,
        NormalizedName = substance.NormalizedName,
        DDInterId = substance.DDInterId,
        Source = substance.Source
    });

    DetectedSubstances.Remove(substance);

    StatusMessage = $"Accepted active substance: {substance.DisplayName}.";
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
    private string BuildCurrentReport()
    {
        var highestSeverity = InteractionResults.Count == 0
            ? "None"
            : InteractionResults
                .OrderByDescending(x => GetSeverityScore(x.Severity))
                .First()
                .Severity;

        var report = new StringWriter();

        report.WriteLine("Drug Compare Interaction Report");
        report.WriteLine("================================");
        report.WriteLine();
        report.WriteLine($"Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.WriteLine($"Highest severity: {highestSeverity}");
        report.WriteLine();

        report.WriteLine("Accepted active substances:");
        report.WriteLine("---------------------------");

        foreach (var substance in AcceptedSubstances)
        {
            report.WriteLine($"- {substance.Name}");
            report.WriteLine($"  Database ID: {substance.DatabaseId}");
            report.WriteLine($"  DDInter ID: {substance.DDInterId}");
            report.WriteLine($"  Source: {substance.Source}");
        }

        report.WriteLine();

        report.WriteLine("Detected interactions:");
        report.WriteLine("----------------------");

        if (InteractionResults.Count == 0)
        {
            report.WriteLine("No known interaction was found in the local DDInter-based database.");
            report.WriteLine("This does not mean that the combination is safe.");
        }
        else
        {
            foreach (var interaction in InteractionResults)
            {
                report.WriteLine($"- {interaction.SubstanceA} + {interaction.SubstanceB}");
                report.WriteLine($"  Severity: {interaction.Severity}");
                report.WriteLine($"  Message: {interaction.Message}");
                report.WriteLine($"  Source: {interaction.Source}");
                report.WriteLine();
            }
        }

        report.WriteLine();
        report.WriteLine("Medical disclaimer:");
        report.WriteLine("-------------------");
        report.WriteLine("This application is an educational clinical decision-support prototype.");
        report.WriteLine("It does not replace physician or pharmacist judgment.");
        report.WriteLine("Missing interaction data does not mean that a combination is safe.");
        report.WriteLine("Every result must be clinically verified by qualified medical personnel.");

        return report.ToString();
    }
    private async Task ExportCurrentReportAsync()
    {
        if (AcceptedSubstances.Count == 0)
        {
            StatusMessage = "No accepted substances to export.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export interaction report",
            Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"drug-compare-report-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var report = BuildCurrentReport();
            File.WriteAllText(dialog.FileName, report);

            StatusMessage = $"Report exported: {dialog.FileName}";

            await _auditLogService.WriteAsync("ReportExported", new
            {
                FilePath = dialog.FileName,
                SubstanceCount = AcceptedSubstances.Count,
                InteractionCount = InteractionResults.Count,
                HighestSeverity = InteractionResults.Count == 0
                    ? "None"
                    : InteractionResults
                        .OrderByDescending(x => GetSeverityScore(x.Severity))
                        .First()
                        .Severity,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Report export failed: {ex.Message}";

            await _auditLogService.WriteAsync("ReportExportFailed", new
            {
                FilePath = dialog.FileName,
                Error = ex.Message,
                Timestamp = DateTime.Now
            });
        }
    }
    private static int GetSeverityScore(string severity)
    {
        return severity switch
        {
            "Contraindicated" => 4,
            "Major" => 3,
            "Moderate" => 2,
            "Minor" => 1,
            _ => 0
        };
    }
    private async Task AddManualSubstanceAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualSubstanceInput))
        {
            StatusMessage = "Enter active substance name.";
            return;
        }

        IsBusy = true;

        try
        {
            var substance = await _substanceLookupService.FindActiveSubstanceAsync(ManualSubstanceInput);

            if (substance is null)
            {
                StatusMessage = "Could not add active substance.";
                return;
            }

            AddAcceptedSubstance(substance);
            ManualSubstanceInput = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Adding substance failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RemoveAcceptedSubstance()
    {
        if (SelectedAcceptedSubstance is null)
        {
            StatusMessage = "Select active substance to remove.";
            return;
        }

        AcceptedSubstances.Remove(SelectedAcceptedSubstance);
        SelectedAcceptedSubstance = null;

        StatusMessage = "Active substance removed.";
    }

    private void ClearCase()
    {

        DrugNameInput = string.Empty;
        ManualSubstanceInput = string.Empty;

        DetectedSubstances.Clear();
        AcceptedSubstances.Clear();
        InteractionResults.Clear();

        SelectedDetectedSubstance = null;
        SelectedAcceptedSubstance = null;
        SelectedInteraction = null;

        ResultSummaryMessage = "No interaction check performed yet.";

        StatusMessage = "Case cleared.";
    }
    private async Task LoadHistoryAsync()
    {
        try
        {
            InteractionHistory.Clear();

            var items = await _interactionHistoryService.GetRecentHistoryAsync(20);
            foreach (var item in items)
            {
                InteractionHistory.Add(item);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Loading history failed: {ex.Message}";
        }
    }

    private async Task CheckInteractionsAsync()
    {
        InteractionResults.Clear();
        SelectedInteraction = null;

        if (AcceptedSubstances.Count < 2)
        {
            ResultSummaryMessage = "At least two active substances are required to check interactions.";
            StatusMessage = "At least two active substances are required.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Checking substance interactions...";

        try
        {
            var accepted = AcceptedSubstances.ToList();

            System.Diagnostics.Debug.WriteLine(
                "UI CHECK INPUT: " +
                string.Join(", ", accepted.Select(x =>
                    $"{x.Name}:{x.DatabaseId?.ToString() ?? "NULL"}:{x.DDInterId ?? "NO_DDINTER"}")));

            var interactions = await _interactionCheckerService.CheckInteractionsAsync(accepted);

            System.Diagnostics.Debug.WriteLine($"UI DIRECT INTERACTIONS COUNT: {interactions.Count}");

            foreach (var interaction in interactions)
            {
                InteractionResults.Add(interaction);
            }

            SelectedInteraction = InteractionResults.FirstOrDefault();

            if (InteractionResults.Count == 0)
            {
                ResultSummaryMessage =
                    "No known interaction was found in the local database. Missing interaction data does not mean that the combination is safe.";
            }
            else
            {
                var highestSeverity = InteractionResults
                    .OrderByDescending(x => GetSeverityScore(x.Severity))
                    .First()
                    .Severity;

                ResultSummaryMessage =
                    $"Found {InteractionResults.Count} interaction(s). Highest severity: {highestSeverity}.";
            }

            StatusMessage =
                $"Direct checker count: {interactions.Count} | UI count: {InteractionResults.Count}";

            await _auditLogService.WriteAsync("InteractionChecked", new
            {
                AcceptedSubstances = AcceptedSubstances.Select(x => new
                {
                    x.Name,
                    x.DatabaseId,
                    x.DDInterId
                }).ToList(),
                InteractionCount = InteractionResults.Count,
                HighestSeverity = InteractionResults.Count == 0
                    ? "None"
                    : InteractionResults
                        .OrderByDescending(x => GetSeverityScore(x.Severity))
                        .First()
                        .Severity,
                Timestamp = DateTime.Now
            });

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            ResultSummaryMessage = "Interaction check failed.";
            StatusMessage = $"Interaction check failed: {ex.Message}";

            System.Diagnostics.Debug.WriteLine($"INTERACTION CHECK ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddAcceptedSubstance(ActiveSubstanceItem substance)
    {
        var alreadyExists = AcceptedSubstances.Any(x => x.NormalizedName == substance.NormalizedName);

        if (alreadyExists)
        {
            StatusMessage = $"Active substance already accepted: {substance.Name}.";

            _ = _auditLogService.WriteAsync("SubstanceAcceptSkipped", new
            {
                substance.Name,
                substance.DatabaseId,
                substance.DDInterId,
                substance.Source,
                Reason = "Duplicate",
                Timestamp = DateTime.Now
            });

            return;
        }

        AcceptedSubstances.Add(substance);

        _ = _auditLogService.WriteAsync("SubstanceAccepted", new
        {
            substance.Name,
            substance.DatabaseId,
            substance.DDInterId,
            substance.Source,
            Timestamp = DateTime.Now
        });

        StatusMessage = $"Accepted: {substance.Name}, DatabaseId: {substance.DatabaseId}, Source: {substance.Source}";
    }
    private async Task SearchIcdCodesAsync()
    {
        IcdSearchResults.Clear();
        SelectedIcdCode = null;

        if (string.IsNullOrWhiteSpace(IcdSearchQuery))
        {
            StatusMessage = "Enter ICD code, disease name, or description.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Searching ICD codes...";

        try
        {
            var categoryFilter = SelectedIcdCategory == "All"
                ? null
                : SelectedIcdCategory;

            var results = await _icdCodeService.SearchAsync(
                IcdSearchQuery,
                categoryFilter,
                100);

            foreach (var item in results)
            {
                IcdSearchResults.Add(item);
            }

            SelectedIcdCode = IcdSearchResults.FirstOrDefault();

            StatusMessage = $"Found {IcdSearchResults.Count} ICD code(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ICD search failed: {(ex)}";
        }
        finally
        {
            IsBusy = false;
        }
    }
    private async Task LoadIcdCategoriesAsync()
    {
        try
        {
            var current = SelectedIcdCategory;

            IcdCategories.Clear();
            IcdCategories.Add("All");

            var categories = await _icdCodeService.GetCategoriesAsync();

            foreach (var category in categories)
            {
                IcdCategories.Add(category);
            }

            SelectedIcdCategory = IcdCategories.Contains(current)
                ? current
                : "All";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Loading ICD categories failed: {(ex)}";
        }
    }
}