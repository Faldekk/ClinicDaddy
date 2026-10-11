using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;

namespace DrugCompare.ViewModels;

public sealed partial class MainViewModel
{
    private CancellationTokenSource? _chplImportCancellation;
    public IReadOnlyList<int> ChplBatchSizes { get; } = [10, 50, 100, 250, 500];
    public ObservableCollection<ChplImportFailure> ChplImportFailures { get; } = new();

    [ObservableProperty] private int chplBatchSize = 50;
    [ObservableProperty] private int chplImportCompleted;
    [ObservableProperty] private int chplImportTotal = 1;
    [ObservableProperty] private string chplImportStatus = "Pobierz brakujące dokumenty ChPL z linków zapisanych w rejestrze.";
    [ObservableProperty] private string chplImportBackupPath = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportChplBatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryChplImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelChplImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDatabaseBackupCommand))]
    private bool isChplImportRunning;

    private bool CanImportChpl() => !IsChplImportRunning && !IsBusy;
    private bool CanRetryChpl() => CanImportChpl() && ChplImportFailures.Count > 0;
    private bool CanCancelChpl() => IsChplImportRunning;

    [RelayCommand(CanExecute = nameof(CanImportChpl))]
    private Task ImportChplBatchAsync() => RunChplImportAsync(false);

    [RelayCommand(CanExecute = nameof(CanRetryChpl))]
    private Task RetryChplImportAsync() => RunChplImportAsync(true);

    [RelayCommand(CanExecute = nameof(CanCancelChpl))]
    private void CancelChplImport()
    {
        _chplImportCancellation?.Cancel();
        ChplImportStatus = "Anulowanie importu — już zapisane dokumenty pozostają w bazie.";
    }

    private async Task RunChplImportAsync(bool retry)
    {
        if (!CanImportChpl()) return;
        var retryIds = retry ? ChplImportFailures.Select(x => x.ProductId).Take(500).ToArray() : null;
        IsChplImportRunning = true;
        ChplImportCompleted = 0;
        ChplImportTotal = 1;
        ChplImportBackupPath = "";
        ChplImportStatus = "Przygotowanie importu i kopii bazy...";
        using var cancellation = new CancellationTokenSource();
        _chplImportCancellation = cancellation;
        var progress = new Progress<ChplImportProgress>(p =>
        {
            ChplImportCompleted = p.Completed;
            ChplImportTotal = Math.Max(1, p.Total);
            ChplImportStatus = $"{p.Completed}/{p.Total} • {p.ProductName} • zapisano: {p.Imported}, pominięto: {p.Skipped}, błędy: {p.Failed}";
        });
        try
        {
            // PDF extraction and SQLite backup can be synchronous; keep them off the UI thread.
            var result = await Task.Run(() => _chplBatchImportService.ImportAsync(
                retryIds?.Length ?? ChplBatchSize, progress, cancellation.Token, retryIds));
            if (retryIds is not null && !result.Cancelled)
            {
                var stillFailed = result.Failures.Select(x => x.ProductId).ToHashSet();
                foreach (var oldFailure in ChplImportFailures.Where(x => retryIds.Contains(x.ProductId) && !stillFailed.Contains(x.ProductId)).ToArray())
                    ChplImportFailures.Remove(oldFailure);
            }
            foreach (var failure in result.Failures)
            {
                var previous = ChplImportFailures.FirstOrDefault(x => x.ProductId == failure.ProductId);
                if (previous is not null) ChplImportFailures.Remove(previous);
                ChplImportFailures.Add(failure);
            }
            ChplImportBackupPath = result.BackupPath ?? "";
            ChplImportStatus = result.Cancelled ? "Import anulowany. " : "Import zakończony. ";
            ChplImportStatus += $"Zapisano: {result.Imported}, pominięto: {result.Skipped}, błędy: {result.Failures.Count}.";
            if (!result.Cancelled && result.Imported == 0 && result.Failures.Count == 0 && result.BackupPath is null)
                ChplImportStatus = "Brak nowych dokumentów ChPL do pobrania.";
            await SafeAuditAsync("ChplBatchImported", new
            {
                result.Imported, result.Skipped, result.Cancelled,
                result.Failures, result.BackupPath
            });
            try { await LoadKnowledgeBaseStatsAsync(); }
            catch (Exception ex) { ChplImportStatus += $" Nie udało się odświeżyć liczników: {ex.Message}"; }
        }
        catch (OperationCanceledException) { ChplImportStatus = "Import anulowany."; }
        catch (Exception ex) { ChplImportStatus = $"Import nie został ukończony: {ex.Message}"; }
        finally
        {
            _chplImportCancellation = null;
            IsChplImportRunning = false;
            RetryChplImportCommand.NotifyCanExecuteChanged();
        }
    }
}
