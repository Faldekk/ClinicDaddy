using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

namespace DrugCompare.ViewModels.Interaction;

public sealed class InteractionCheckerViewModel : ObservableObject
{
    private readonly IDrugLookupService _drugLookupService;
    private readonly ISubstanceLookupService _substanceLookupService;
    private readonly IInteractionCheckerService _interactionCheckerService;
    private readonly IAuditLogService _auditLogService;

    private string _drugNameInput = string.Empty;
    private string _manualSubstanceInput = string.Empty;
    private string _resultSummaryMessage = "Nie wykonano jeszcze sprawdzenia interakcji.";
    private string _statusMessage = "Gotowe.";
    private bool _isBusy;

    private ActiveSubstanceItem? _selectedDetectedSubstance;
    private ActiveSubstanceItem? _selectedAcceptedSubstance;
    private InteractionResult? _selectedInteraction;

    public InteractionCheckerViewModel(
        IDrugLookupService drugLookupService,
        ISubstanceLookupService substanceLookupService,
        IInteractionCheckerService interactionCheckerService,
        IAuditLogService auditLogService)
    {
        _drugLookupService = drugLookupService;
        _substanceLookupService = substanceLookupService;
        _interactionCheckerService = interactionCheckerService;
        _auditLogService = auditLogService;

        FindDrugCommand = new AsyncRelayCommand(FindDrugAsync, () => CanWork);
        AcceptDetectedSubstanceCommand = new RelayCommand(AcceptDetectedSubstance, () => CanWork);
        AcceptAllDetectedSubstancesCommand = new RelayCommand(AcceptAllDetectedSubstances, () => CanWork);
        AddManualSubstanceCommand = new AsyncRelayCommand(AddManualSubstanceAsync, () => CanWork);
        RemoveAcceptedSubstanceCommand = new RelayCommand(RemoveAcceptedSubstance, () => CanWork);
        ClearCaseCommand = new RelayCommand(ClearCase, () => CanWork);
        CheckInteractionsCommand = new AsyncRelayCommand(CheckInteractionsAsync, () => CanWork);
        ExportCurrentReportCommand = new AsyncRelayCommand(ExportCurrentReportAsync, () => CanWork);
        AcceptedSubstances.CollectionChanged += (_, _) => InvalidateResults();
    }

    public string DrugNameInput
    {
        get => _drugNameInput;
        set => SetProperty(ref _drugNameInput, value);
    }

    public string ManualSubstanceInput
    {
        get => _manualSubstanceInput;
        set => SetProperty(ref _manualSubstanceInput, value);
    }

    public string ResultSummaryMessage
    {
        get => _resultSummaryMessage;
        set => SetProperty(ref _resultSummaryMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanWork));
            OnPropertyChanged(nameof(IsWorking));
            FindDrugCommand.NotifyCanExecuteChanged();
            AcceptDetectedSubstanceCommand.NotifyCanExecuteChanged();
            AcceptAllDetectedSubstancesCommand.NotifyCanExecuteChanged();
            AddManualSubstanceCommand.NotifyCanExecuteChanged();
            RemoveAcceptedSubstanceCommand.NotifyCanExecuteChanged();
            ClearCaseCommand.NotifyCanExecuteChanged();
            CheckInteractionsCommand.NotifyCanExecuteChanged();
            ExportCurrentReportCommand.NotifyCanExecuteChanged();
        }
    }

    public bool CanWork => !IsBusy;
    public bool IsWorking => IsBusy;

    private void InvalidateResults()
    {
        InteractionResults.Clear();
        SelectedInteraction = null;
        ResultSummaryMessage = "Zmieniono listę substancji. Sprawdź interakcje ponownie.";
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

    public ObservableCollection<ActiveSubstanceItem> DetectedSubstances { get; } = new();
    public ObservableCollection<ActiveSubstanceItem> AcceptedSubstances { get; } = new();
    public ObservableCollection<InteractionResult> InteractionResults { get; } = new();

    public IAsyncRelayCommand FindDrugCommand { get; }
    public IRelayCommand AcceptDetectedSubstanceCommand { get; }
    public IRelayCommand AcceptAllDetectedSubstancesCommand { get; }
    public IAsyncRelayCommand AddManualSubstanceCommand { get; }
    public IRelayCommand RemoveAcceptedSubstanceCommand { get; }
    public IRelayCommand ClearCaseCommand { get; }
    public IAsyncRelayCommand CheckInteractionsCommand { get; }
    public IAsyncRelayCommand ExportCurrentReportCommand { get; }

    private async Task FindDrugAsync()
    {
        if (!CanWork) return;
        DetectedSubstances.Clear();
        SelectedDetectedSubstance = null;

        if (string.IsNullOrWhiteSpace(DrugNameInput))
        {
            StatusMessage = "Wpisz nazwę leku.";
            return;
        }

        var searchedDrugName = DrugNameInput.Trim();

        IsBusy = true;
        StatusMessage = "Wyszukiwanie w lokalnym słowniku leków…";

        try
        {
            var result = await _drugLookupService.FindDrugAsync(searchedDrugName);

            if (result is null || result.ActiveSubstances.Count == 0)
            {
                StatusMessage = "Nie znaleziono leku w lokalnym słowniku. Dodaj substancję czynną ręcznie.";

                await SafeAuditAsync("DrugSearched", new
                {
                    DrugName = searchedDrugName,
                    Found = false,
                    DetectedSubstanceCount = 0,
                    Timestamp = DateTime.Now
                });

                return;
            }

            foreach (var substance in result.ActiveSubstances)
            {
                DetectedSubstances.Add(substance);
            }

            StatusMessage =
                $"Lek: {result.DrugName}. Znalezione substancje czynne: {result.ActiveSubstances.Count}.";

            await SafeAuditAsync("DrugSearched", new
            {
                DrugName = searchedDrugName,
                Found = true,
                ResultDrugName = result.DrugName,
                DetectedSubstanceCount = result.ActiveSubstances.Count,
                DetectedSubstances = result.ActiveSubstances.Select(x => new
                {
                    x.Name,
                    x.DatabaseId,
                    x.DDInterId,
                    x.Source
                }).ToList(),
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd wyszukiwania leku: {ex.Message}. Spróbuj ponownie.";

            await SafeAuditAsync("DrugSearchFailed", new
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

    private async Task AddManualSubstanceAsync()
    {
        if (!CanWork) return;
        if (string.IsNullOrWhiteSpace(ManualSubstanceInput))
        {
            StatusMessage = "Wpisz nazwę substancji czynnej.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Wyszukiwanie substancji czynnej…";

        try
        {
            var substance = await _substanceLookupService.FindActiveSubstanceAsync(ManualSubstanceInput);

            if (substance is null)
            {
                StatusMessage = "Nie znaleziono substancji czynnej. Sprawdź nazwę.";
                return;
            }

            AddAcceptedSubstance(substance);
            ManualSubstanceInput = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd dodawania substancji: {ex.Message}. Spróbuj ponownie.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AcceptDetectedSubstance()
    {
        if (!CanWork) return;
        if (SelectedDetectedSubstance is null)
        {
            StatusMessage = "Najpierw wybierz wykrytą substancję czynną.";
            return;
        }

        var substance = SelectedDetectedSubstance;

        AddAcceptedSubstance(substance);

        DetectedSubstances.Remove(substance);
        SelectedDetectedSubstance = null;
    }

    private void AcceptAllDetectedSubstances()
    {
        if (!CanWork) return;
        if (DetectedSubstances.Count == 0)
        {
            StatusMessage = "Brak wykrytych substancji do dodania.";
            return;
        }

        var items = DetectedSubstances.ToList();

        foreach (var substance in items)
        {
            AddAcceptedSubstance(substance);
        }

        DetectedSubstances.Clear();

        StatusMessage = $"Substancje do sprawdzenia: {AcceptedSubstances.Count}.";
    }

    private void AddAcceptedSubstance(ActiveSubstanceItem substance)
    {
        var alreadyExists = AcceptedSubstances.Any(x =>
            substance.DatabaseId.HasValue && x.DatabaseId == substance.DatabaseId
            || string.Equals(x.NormalizedName, substance.NormalizedName, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            StatusMessage = $"Substancja jest już na liście: {substance.Name}.";

            _ = SafeAuditAsync("SubstanceAcceptSkipped", new
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

        AcceptedSubstances.Add(new ActiveSubstanceItem
        {
            DatabaseId = substance.DatabaseId,
            Name = substance.Name,
            NormalizedName = substance.NormalizedName,
            DDInterId = substance.DDInterId,
            Source = substance.Source
        });

        _ = SafeAuditAsync("SubstanceAccepted", new
        {
            substance.Name,
            substance.DatabaseId,
            substance.DDInterId,
            substance.Source,
            Timestamp = DateTime.Now
        });

        StatusMessage = $"Dodano: {substance.Name}.";
    }

    private void RemoveAcceptedSubstance()
    {
        if (!CanWork) return;
        if (SelectedAcceptedSubstance is null)
        {
            StatusMessage = "Wybierz substancję do usunięcia.";
            return;
        }

        AcceptedSubstances.Remove(SelectedAcceptedSubstance);
        SelectedAcceptedSubstance = null;

        StatusMessage = "Usunięto substancję czynną.";
    }

    private async Task CheckInteractionsAsync()
    {
        if (!CanWork) return;
        InteractionResults.Clear();
        SelectedInteraction = null;

        if (AcceptedSubstances.Count < 2)
        {
            ResultSummaryMessage = "Do sprawdzenia interakcji wymagane są co najmniej dwie substancje czynne.";
            StatusMessage = ResultSummaryMessage;
            return;
        }

        IsBusy = true;
        StatusMessage = "Sprawdzanie interakcji substancji…";
        ResultSummaryMessage = "Sprawdzanie interakcji…";

        try
        {
            var accepted = AcceptedSubstances.ToList();

            var interactions = await _interactionCheckerService.CheckInteractionsAsync(accepted);

            foreach (var interaction in interactions)
            {
                InteractionResults.Add(interaction);
            }

            SelectedInteraction = InteractionResults.FirstOrDefault();

            if (InteractionResults.Count == 0)
            {
                ResultSummaryMessage =
                    "Nie znaleziono znanej interakcji w lokalnej bazie. Brak danych nie oznacza, że połączenie jest bezpieczne.";
            }
            else
            {
                var highestSeverity = InteractionResults
                    .OrderByDescending(x => GetSeverityScore(x.Severity))
                    .First()
                    .Severity;

                ResultSummaryMessage =
                    $"Znalezione interakcje: {InteractionResults.Count}. Najwyższe ryzyko: {highestSeverity}.";
            }

            StatusMessage = ResultSummaryMessage;

            await SafeAuditAsync("InteractionChecked", new
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
        }
        catch (Exception ex)
        {
            InteractionResults.Clear();
            SelectedInteraction = null;
            ResultSummaryMessage = "Nie udało się sprawdzić interakcji. Wyniki nie są dostępne.";
            StatusMessage = $"Błąd sprawdzania interakcji: {ex.Message}. Spróbuj ponownie.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearCase()
    {
        if (!CanWork) return;
        DrugNameInput = string.Empty;
        ManualSubstanceInput = string.Empty;

        DetectedSubstances.Clear();
        AcceptedSubstances.Clear();
        InteractionResults.Clear();

        SelectedDetectedSubstance = null;
        SelectedAcceptedSubstance = null;
        SelectedInteraction = null;

        ResultSummaryMessage = "Nie wykonano jeszcze sprawdzenia interakcji.";
        StatusMessage = "Przypadek wyczyszczony.";
    }

    private async Task ExportCurrentReportAsync()
    {
        if (!CanWork) return;
        if (AcceptedSubstances.Count == 0)
        {
            StatusMessage = "Brak substancji do wyeksportowania.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Eksport raportu interakcji",
            Filter = "Plik tekstowy (*.txt)|*.txt|Wszystkie pliki (*.*)|*.*",
            FileName = $"medcompare-report-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };

        IsBusy = true;
        try
        {
            if (dialog.ShowDialog() != true) return;
            var report = BuildCurrentReport();

            StatusMessage = "Zapisywanie raportu…";
            await File.WriteAllTextAsync(dialog.FileName, report);

            StatusMessage = $"Zapisano raport: {dialog.FileName}";

            await SafeAuditAsync("ReportExported", new
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
            StatusMessage = $"Błąd eksportu raportu: {ex.Message}";

            await SafeAuditAsync("ReportExportFailed", new
            {
                FilePath = dialog.FileName,
                Error = ex.Message,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsBusy = false;
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

        report.WriteLine("MedCompare Interaction Report");
        report.WriteLine("=============================");
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
            report.WriteLine(ResultSummaryMessage);
            report.WriteLine("Missing interaction data does not mean that the combination is safe.");
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

    private static int GetSeverityScore(string severity)
    {
        var value = severity.Trim().ToLowerInvariant();

        return value switch
        {
            "contraindicated" => 5,
            "major" => 4,
            "moderate" => 3,
            "minor" => 2,
            "unknown" => 1,
            "x" => 5,
            "d" => 4,
            "c" => 3,
            "b" => 2,
            "a" => 1,
            _ => 0
        };
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
