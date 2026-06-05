using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Models;
using DrugCompare.Services;
using DrugCompare.Services.Contracts;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using DrugCompare.Services.Application;
using System.IO;

namespace DrugCompare.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDrugLookupService _drugLookupService;
    private readonly ISubstanceLookupService _substanceLookupService;
    private readonly IInteractionCheckerService _interactionCheckerService;
    private readonly IDatabaseStatusService _databaseStatusService;
    private readonly IInteractionHistoryService _interactionHistoryService;
    private readonly InteractionAnalysisService _interactionAnalysisService;
    private string _drugNameInput = string.Empty;
    private string _manualSubstanceInput = string.Empty;
    private ActiveSubstanceItem? _selectedDetectedSubstance;
    private ActiveSubstanceItem? _selectedAcceptedSubstance;
    private InteractionResult? _selectedInteraction;
    private string _resultSummaryMessage = "No interaction check performed yet.";
    private string _statusMessage = "Ready.";
    private bool _isBusy;
    private string _databaseStatusText = "Database status not loaded.";
    public ObservableCollection<InteractionHistoryItem> InteractionHistory { get; } = new();
    public IRelayCommand ExportCurrentReportCommand { get; }
    public MainViewModel(
    IDrugLookupService drugLookupService,
    ISubstanceLookupService substanceLookupService,
    IInteractionCheckerService interactionCheckerService,
    IDatabaseStatusService databaseStatusService,
    IInteractionHistoryService interactionHistoryService,
    InteractionAnalysisService interactionAnalysisService)
    {
        _drugLookupService = drugLookupService;
        _substanceLookupService = substanceLookupService;
        _interactionCheckerService = interactionCheckerService;
        _databaseStatusService = databaseStatusService;
        _interactionHistoryService = interactionHistoryService;
        _interactionAnalysisService = interactionAnalysisService;

        FindDrugCommand = new AsyncRelayCommand(FindDrugAsync);
        AcceptDetectedSubstanceCommand = new RelayCommand(AcceptDetectedSubstance);
        AcceptAllDetectedSubstancesCommand = new RelayCommand(AcceptAllDetectedSubstances);
        AddManualSubstanceCommand = new AsyncRelayCommand(AddManualSubstanceAsync);
        RemoveAcceptedSubstanceCommand = new RelayCommand(RemoveAcceptedSubstance);
        ClearCaseCommand = new RelayCommand(ClearCase);

        CheckInteractionsCommand = new AsyncRelayCommand(CheckInteractionsAsync);
        LoadDatabaseStatusCommand = new AsyncRelayCommand(LoadDatabaseStatusAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
        ExportCurrentReportCommand = new RelayCommand(ExportCurrentReport);
    }
    public string DatabaseStatusText
    {
        get => _databaseStatusText;
        set => SetProperty(ref _databaseStatusText, value);
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

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<ActiveSubstanceItem> DetectedSubstances { get; } = new();

    public ObservableCollection<ActiveSubstanceItem> AcceptedSubstances { get; } = new();

    public ObservableCollection<InteractionResult> InteractionResults { get; } = new();

    public IAsyncRelayCommand FindDrugCommand { get; }
    public IAsyncRelayCommand LoadHistoryCommand { get; }

    public IRelayCommand AcceptDetectedSubstanceCommand { get; }

    public IRelayCommand AcceptAllDetectedSubstancesCommand { get; }

    public IAsyncRelayCommand AddManualSubstanceCommand { get; }

    public IRelayCommand RemoveAcceptedSubstanceCommand { get; }

    public IRelayCommand ClearCaseCommand { get; }

    public IAsyncRelayCommand CheckInteractionsCommand { get; }
    public IAsyncRelayCommand LoadDatabaseStatusCommand { get; }
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

        IsBusy = true;
        StatusMessage = "Searching local drug dictionary...";

        try
        {
            var result = await _drugLookupService.FindDrugAsync(DrugNameInput);

            if (result is null)
            {
                StatusMessage = "Drug not found in local dictionary. Add active substance manually.";
                return;
            }

            foreach (var substance in result.ActiveSubstances)
            {
                DetectedSubstances.Add(substance);
            }

            StatusMessage = $"Found {result.ActiveSubstances.Count} active substance(s) for {result.DrugName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Drug lookup failed: {ex.Message}";
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

        AddAcceptedSubstance(SelectedDetectedSubstance);
    }

    private void AcceptAllDetectedSubstances()
    {
        if (DetectedSubstances.Count == 0)
        {
            StatusMessage = "No detected substances to accept.";
            return;
        }

        foreach (var substance in DetectedSubstances)
        {
            AddAcceptedSubstance(substance);
        }

        StatusMessage = "Detected active substances accepted.";
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
    private void ExportCurrentReport()
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
            return;

        try
        {
            var report = BuildCurrentReport();

            File.WriteAllText(dialog.FileName, report);

            StatusMessage = $"Report exported: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Report export failed: {ex.Message}";
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
            var analysis = await _interactionAnalysisService.AnalyzeAsync(
                AcceptedSubstances.ToList());

            foreach (var interaction in analysis.Interactions)
            {
                InteractionResults.Add(interaction);
            }

            SelectedInteraction = InteractionResults.FirstOrDefault();

            ResultSummaryMessage = analysis.SummaryMessage;
            StatusMessage = analysis.SummaryMessage;

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Interaction check failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddAcceptedSubstance(ActiveSubstanceItem substance)
    {
        var alreadyExists = AcceptedSubstances.Any(x =>
            x.NormalizedName == substance.NormalizedName);

        if (alreadyExists)
        {
            StatusMessage = $"Accepted: {substance.Name}, DatabaseId: {substance.DatabaseId}, Source: {substance.Source}";
            return;
        }

        AcceptedSubstances.Add(substance);

        StatusMessage = $"Accepted: {substance.Name}, DatabaseId: {substance.DatabaseId}, Source: {substance.Source}";
    }
}