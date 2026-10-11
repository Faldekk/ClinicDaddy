using DrugCompare.Application.Repositories.Contracts;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.Rag;
using DrugCompare.Application.Services.Implementations;
using DrugCompare.Application.Services.Implementations.KnowledgeBase;
using DrugCompare.Application.Services.Implementations.Rag;
using DrugCompare.Features.ChPLNavigator;
using DrugCompare.Features.ChPLNavigator.Services;
using DrugCompare.Features.EvidenceAssistant;
using DrugCompare.Features.IcdLooker;
using DrugCompare.Features.InteractionChecker;
using DrugCompare.Features.PolishRegistry;
using DrugCompare.Infrastructure.SQLite;
using DrugCompare.Infrastructure.SQLite.KnowledgeBase;
using DrugCompare.Infrastructure.SQLite.Rag;
using DrugCompare.ViewModels;
using DrugCompare.ViewModels.Interaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Net.Http;
using System.Windows.Threading;

namespace DrugCompare;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            RegisterSqliteRepositories(services);
            RegisterApplicationServices(services);
            RegisterViewModels(services);
            RegisterViews(services);

            _serviceProvider = services.BuildServiceProvider();
            WriteLifecycle("Startup: service provider created.");
            await _serviceProvider.GetRequiredService<SqliteDatabaseInitializer>()
                .InitializeAsync();
            WriteLifecycle("Startup: SQLite migrations completed.");

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            WriteLifecycle("Startup: main window shown.");
        }
        catch (Exception ex)
        {
            WriteStartupError(ex);
            System.Windows.MessageBox.Show(
                $"Nie można uruchomić aplikacji. Szczegóły zapisano w startup-error.log.\n\n{ex.Message}",
                "Błąd uruchamiania",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLifecycle($"Dispatcher exception: {e.Exception}");
        WriteStartupError(e.Exception);
        System.Windows.MessageBox.Show(
            $"Nieobsłużony błąd aplikacji. Szczegóły zapisano w startup-error.log.\n\n{e.Exception.Message}",
            "Błąd aplikacji",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void WriteStartupError(Exception exception)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "startup-error.log"),
                $"[{DateTime.Now:O}]\n{exception}\n\n");
        }
        catch
        {
            // The error dialog remains available even if the folder is read-only.
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        WriteLifecycle($"Application exit. Exit code: {e.ApplicationExitCode}.");
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void WriteLifecycle(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "app-lifecycle.log"),
                $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Lifecycle logging cannot interfere with shutdown.
        }
    }

    private static void RegisterSqliteRepositories(IServiceCollection services)
    {
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SqliteDatabaseInitializer>();
        services.AddSingleton<ILocalDatabaseBackupService, SqliteLocalDatabaseBackupService>();

        services.AddSingleton<IDrugRepository, SqliteDrugRepository>();
        services.AddSingleton<ISubstanceRepository, SqliteSubstanceRepository>();
        services.AddSingleton<IInteractionRepository, SqliteInteractionRepository>();
        services.AddSingleton<IInteractionHistoryRepository, SqliteInteractionHistoryRepository>();
        services.AddSingleton<IPolishDrugRegistryRepository, SqlitePolishDrugRegistryRepository>();
        services.AddSingleton<IIcdCodeRepository, SqliteIcdCodeRepository>();
        services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();
        services.AddSingleton<IChplDocumentRepository, SqliteChplDocumentRepository>();
        services.AddSingleton<IChplSectionRepository, SqliteChplSectionRepository>();
        services.AddSingleton<IKnowledgeChunkRepository, SqliteKnowledgeChunkRepository>();
        services.AddSingleton<IAtomicKnowledgeBaseIngestionRepository, SqliteAtomicKnowledgeBaseIngestionRepository>();
        services.AddSingleton<IKnowledgeBaseReviewRepository, SqliteKnowledgeBaseReviewRepository>();
        services.AddSingleton<IDatabaseStatusRepository, SqliteDatabaseStatusRepository>();
        services.AddSingleton<IDataManagementRepository, DisabledDataManagementRepository>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<DrugDataService>();

        services.AddSingleton<IDrugLookupService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<ISubstanceLookupService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<ISubstanceSynonymService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<IInteractionCheckerService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<IInteractionHistoryService>(sp =>
            sp.GetRequiredService<DrugDataService>());

        services.AddSingleton<IPolishDrugRegistryService, PolishDrugRegistryService>();
        services.AddSingleton<IIcdCodeService, IcdCodeService>();
        services.AddSingleton<IAuditLogService, AuditLogService>();
        services.AddSingleton<IRagRetriever, SqliteFtsRagRetriever>();
        services.AddSingleton<IRagAnswerValidator, RagAnswerValidator>();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IRagAnswerService, OllamaRagAnswerService>();
        services.AddSingleton<IOllamaStatusService, OllamaStatusService>();
        services.AddSingleton<ChplPdfDownloader>();
        services.AddSingleton<IKnowledgeBaseIngestionService, KnowledgeBaseIngestionService>();
        services.AddSingleton<IChplProductImporter, ChplProductImporter>();
        services.AddSingleton<IChplBatchImportService, SqliteChplBatchImportService>();
        services.AddSingleton<IKnowledgeBaseReviewService, KnowledgeBaseReviewService>();
        services.AddSingleton<IKnowledgeBaseStatsService, KnowledgeBaseStatsService>();
        services.AddSingleton<IDatabaseStatusService, DatabaseStatusService>();
        services.AddSingleton<IKnowledgeBaseSearchService, KnowledgeBaseSearchService>();
        services.AddSingleton<IDataManagementService, DisabledDataManagementService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<InteractionCheckerViewModel>();
        services.AddSingleton<PolishDrugRegistryViewModel>();
        services.AddSingleton<IcdLookerViewModel>();
        services.AddSingleton<ChPLNavigatorViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<EvidenceAssistantViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
    }
}
