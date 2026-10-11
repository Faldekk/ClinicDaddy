using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.Rag;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Presentation;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;

namespace DrugCompare.Features.EvidenceAssistant;

public sealed partial class EvidenceAssistantViewModel : ObservableObject
{
    private readonly IRagRetriever _ragRetriever;
    private readonly IKnowledgeBaseReviewService _reviewService;
    private readonly IRagAnswerService _ragAnswerService;
    private readonly IOllamaStatusService _ollamaStatusService;
    private readonly WebDocumentLauncher _documentLauncher;

    public EvidenceAssistantViewModel(IRagRetriever ragRetriever, IKnowledgeBaseReviewService reviewService,
        IRagAnswerService ragAnswerService, IOllamaStatusService ollamaStatusService,
        WebDocumentLauncher? documentLauncher = null)
    {
        _ragRetriever = ragRetriever;
        _reviewService = reviewService;
        _ragAnswerService = ragAnswerService;
        _ollamaStatusService = ollamaStatusService;
        _documentLauncher = documentLauncher ?? new WebDocumentLauncher();
    }

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private string sectionNumber = string.Empty;

    [ObservableProperty]
    private bool includeNeedsReview;

    [ObservableProperty]
    private string statusMessage = "Gotowe.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeReviewStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateRagAnswerCommand))]
    [NotifyCanExecuteChangedFor(nameof(CheckOllamaStatusCommand))]
    private bool isBusy;

    public bool CanEdit => !IsBusy;
    private bool CanWork() => !IsBusy;
    private bool CanCheckOllama() => !IsBusy && !IsCheckingOllama;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckOllamaStatusCommand))]
    private bool isCheckingOllama;

    [ObservableProperty] private string ollamaStatusText = "Połączenie z Ollama nie zostało jeszcze sprawdzone.";
    [ObservableProperty] private bool isGenerating;
    [ObservableProperty] private string ragActivity = "";

    [RelayCommand(CanExecute = nameof(CanCheckOllama))]
    private async Task CheckOllamaStatusAsync()
    {
        try { await UpdateOllamaStatusAsync(CancellationToken.None); }
        catch (Exception ex) { OllamaStatusText = $"Nie udało się sprawdzić połączenia: {ex.Message}"; }
    }

    private async Task<OllamaStatus> UpdateOllamaStatusAsync(CancellationToken cancellationToken)
    {
        IsCheckingOllama = true;
        OllamaStatusText = "Sprawdzanie Ollama i dostępności modelu...";
        try
        {
            var status = await _ollamaStatusService.CheckAsync(cancellationToken);
            OllamaStatusText = status.Message;
            return status;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            OllamaStatusText = "Sprawdzanie połączenia anulowane.";
            throw;
        }
        finally { IsCheckingOllama = false; }
    }

    [ObservableProperty]
    private KnowledgeChunkResult? selectedResult;

    [ObservableProperty]
    private string reviewerName = string.Empty;

    [ObservableProperty]
    private string reviewNote = string.Empty;

    [ObservableProperty]
    private string generatedAnswer = string.Empty;

    public ObservableCollection<KnowledgeChunkResult> Results { get; } = new();

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task SearchAsync()
    {
        Results.Clear();
        SelectedResult = null;
        GeneratedAnswer = string.Empty;

        if (string.IsNullOrWhiteSpace(Query))
        {
            StatusMessage = "Wpisz pytanie albo frazę do wyszukania.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Wyszukiwanie źródeł w lokalnej bazie...";

            var options = new RagRetrievalOptions
            {
                Limit = 20,
                IncludeNeedsReview = IncludeNeedsReview,
                IncludeReviewed = true,
                IncludeVerified = true,
                ProductName = string.IsNullOrWhiteSpace(ProductName) ? null : ProductName,
                SectionNumber = string.IsNullOrWhiteSpace(SectionNumber) ? null : SectionNumber
            };

            var results = await _ragRetriever.RetrieveAsync(Query, options);

            foreach (var result in results)
            {
                Results.Add(result);
            }

            StatusMessage = results.Count == 0
                ? "Nie znaleziono źródeł. Brak danych nie oznacza bezpieczeństwa."
                : $"Znaleziono źródeł: {results.Count}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd wyszukiwania: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private void Clear()
    {
        Query = string.Empty;
        ProductName = string.Empty;
        SectionNumber = string.Empty;
        Results.Clear();
        SelectedResult = null;
        StatusMessage = "Wyczyszczono.";
        GeneratedAnswer = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private async Task ChangeReviewStatusAsync(string? status)
    {
        if (SelectedResult is null)
        {
            StatusMessage = "Wybierz źródło do weryfikacji.";
            return;
        }

        try
        {
            IsBusy = true;
            await _reviewService.ReviewDocumentForChunkAsync(SelectedResult.Id, status ?? string.Empty, ReviewerName, ReviewNote);
            StatusMessage = $"Dokument źródłowy oznaczono jako: {status}.";
            await SearchAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się zmienić statusu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSelectedSource()
    {
        if (string.IsNullOrWhiteSpace(SelectedResult?.SourceUrl))
        {
            StatusMessage = "Wybrane źródło nie ma adresu URL.";
            return;
        }

        try
        {
            _documentLauncher.Open(SelectedResult.SourceUrl);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się otworzyć źródła: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanWork), IncludeCancelCommand = true)]
    private async Task GenerateRagAnswerAsync(CancellationToken cancellationToken)
    {
        WriteRagUiLog($"Generate requested. Existing results={Results.Count}.");
        if (string.IsNullOrWhiteSpace(Query))
        {
            GeneratedAnswer = string.Empty;
            StatusMessage = "Wpisz pytanie przed uruchomieniem RAG.";
            return;
        }

        DispatcherTimer? timer = null;
        try
        {
            IsBusy = true;
            GeneratedAnswer = string.Empty;
            StatusMessage = "Przygotowywanie lokalnych źródeł dla RAG...";

            // Always retrieve for the current question and filters.
            Results.Clear();
            SelectedResult = null;
            var options = new RagRetrievalOptions
            {
                Limit = 20,
                IncludeNeedsReview = IncludeNeedsReview,
                IncludeReviewed = true,
                IncludeVerified = true,
                ProductName = string.IsNullOrWhiteSpace(ProductName) ? null : ProductName,
                SectionNumber = string.IsNullOrWhiteSpace(SectionNumber) ? null : SectionNumber
            };
            var retrieved = await _ragRetriever.RetrieveAsync(Query, options, cancellationToken);
            foreach (var source in retrieved) Results.Add(source);
            WriteRagUiLog($"Automatic retrieval completed. Results={Results.Count}.");

            var approvedSources = Results
                .Where(source => source.ReviewStatus is "reviewed" or "verified")
                .ToList();

            if (approvedSources.Count == 0)
            {
                WriteRagUiLog("No reviewed or verified sources. Ollama call skipped.");
                StatusMessage = "Brak zatwierdzonych źródeł. Zweryfikuj dokument ChPL jako Reviewed lub Verified. Model nie został wywołany.";
                return;
            }

            var connection = await UpdateOllamaStatusAsync(cancellationToken);
            if (!connection.IsReady)
            {
                StatusMessage = connection.Message + " Możesz nadal przeglądać wyszukane źródła.";
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            IsGenerating = true;
            var elapsed = Stopwatch.StartNew();
            RagActivity = "Oczekiwanie na odpowiedź modelu • 0 s";
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => RagActivity = $"Oczekiwanie na odpowiedź modelu • {elapsed.Elapsed.TotalSeconds:0} s";
            timer.Start();
            StatusMessage = "Redagowanie odpowiedzi przez Ollama na podstawie zatwierdzonych źródeł...";
            WriteRagUiLog($"Ollama call started. Approved sources={approvedSources.Count}.");
            var answer = await _ragAnswerService.GenerateAsync(Query, approvedSources, cancellationToken);
            WriteRagUiLog($"Ollama call completed. Findings={answer.Findings.Count}; insufficient={answer.InsufficientEvidence}.");
            GeneratedAnswer = answer.Summary;
            if (answer.Findings.Count > 0)
                GeneratedAnswer = "Wnioski modelu z wybranych fragmentów (nie pełny przegląd ChPL):" + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine + Environment.NewLine, answer.Findings.Select(finding => $"• {finding.Claim} [źródła: {string.Join(", ", finding.SourceIds)}]\nFragment źródła: {finding.EvidenceQuote}"));
            StatusMessage = answer.InsufficientEvidence ? "Model zgłosił brak wystarczających źródeł." : "Sprawdzono identyfikatory i dosłowność cytatów. Zgodność znaczenia wniosków wymaga oceny człowieka.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            GeneratedAnswer = string.Empty;
            StatusMessage = "Generowanie anulowane. Wyszukane źródła pozostają dostępne.";
        }
        catch (RagServiceException ex)
        {
            WriteRagUiLog("Generate failed.", ex);
            GeneratedAnswer = string.Empty;
            StatusMessage = ex.Message;
            if (ex.Kind is RagFailureKind.Connection or RagFailureKind.ModelMissing or RagFailureKind.Disabled)
                OllamaStatusText = ex.Message;
        }
        catch (Exception ex)
        {
            WriteRagUiLog("Generate failed.", ex);
            GeneratedAnswer = string.Empty;
            StatusMessage = $"Lokalny RAG nie jest dostępny: {ex.Message}";
        }
        finally
        {
            timer?.Stop();
            IsGenerating = false;
            RagActivity = "";
            IsBusy = false;
        }
    }

    private static void WriteRagUiLog(string message, Exception? exception = null)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:O}] {message}";
            if (exception is not null) line += Environment.NewLine + exception;
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "rag-ui.log"), line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics must not affect the UI command.
        }
    }
}
