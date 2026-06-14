using DrugCompare.Services;
using DrugCompare.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DrugCompare.Services.Contracts;
using System.Windows;
using DrugCompare.Services.Application;
using DrugCompare.Repositories;
using DrugCompare.Repositories.Contracts;
using DrugCompare.Database;


namespace DrugCompare;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<SqliteConnectionFactory>();

        var databaseProvider = configuration["Database:Provider"];
        var useSqlite = string.Equals(databaseProvider, "SQLite", StringComparison.OrdinalIgnoreCase);

        if (useSqlite)
        {
            services.AddSingleton<SqliteConnectionFactory>();

            services.AddSingleton<IIcdCodeRepository, SqliteIcdCodeRepository>();
            services.AddSingleton<IPolishDrugRegistryRepository, SqlitePolishDrugRegistryRepository>();
            services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();
            services.AddSingleton<IInteractionRepository, SqliteInteractionRepository>();

            // Na razie wyłączam dla portable
            // services.AddSingleton<IDatabaseStatusRepository, SqliteDatabaseStatusRepository>();
            // services.AddSingleton<IDataManagementRepository, SqliteDataManagementRepository>();
        }
        else
        {
            services.AddSingleton<IIcdCodeRepository, PostgresIcdCodeRepository>();
            services.AddSingleton<IPolishDrugRegistryRepository, PostgresPolishDrugRegistryRepository>();
            services.AddSingleton<IAuditLogRepository, PostgresAuditLogRepository>();
            services.AddSingleton<IInteractionRepository, PostgresInteractionRepository>();

            services.AddSingleton<IDatabaseStatusRepository, PostgresDatabaseStatusRepository>();
            services.AddSingleton<IDataManagementRepository, PostgresDataManagementRepository>();
        }

        services.AddSingleton<IDrugRepository, PostgresDrugRepository>();
        services.AddSingleton<ISubstanceRepository, PostgresSubstanceRepository>();
        services.AddSingleton<IInteractionRepository, PostgresInteractionRepository>();
        services.AddSingleton<IInteractionHistoryRepository, PostgresInteractionHistoryRepository>();
        services.AddSingleton<IDatabaseStatusRepository, PostgresDatabaseStatusRepository>();
        services.AddSingleton<IDataManagementRepository, PostgresDataManagementRepository>();
        services.AddSingleton<IAuditLogRepository, PostgresAuditLogRepository>();
        services.AddSingleton<IDrugExplorerRepository, PostgresDrugExplorerRepository>();
        services.AddSingleton<IPolishDrugRegistryService, PolishDrugRegistryService>();
        services.AddSingleton<IPolishDrugRegistryRepository, PostgresPolishDrugRegistryRepository>();
        services.AddSingleton<IIcdCodeRepository, PostgresIcdCodeRepository>();
        services.AddSingleton<IIcdCodeService, IcdCodeService>();
        services.AddSingleton<IDatabaseStatusRepository, PostgresDatabaseStatusRepository>();
        services.AddSingleton<IDataManagementRepository, PostgresDataManagementRepository>();

        services.AddSingleton<PostgresDrugDataService>();

        services.AddSingleton<IAuditLogService, AuditLogService>();

        services.AddSingleton<IDrugExplorerService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());
        
        services.AddSingleton<IDrugLookupService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<IDrugExplorerService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<ISubstanceLookupService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<ISubstanceSynonymService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<IInteractionCheckerService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<IInteractionHistoryService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());
        if (useSqlite)
        {
            services.AddSingleton<SqliteConnectionFactory>();

            services.AddSingleton<IDrugRepository, SqliteDrugRepository>();
            services.AddSingleton<ISubstanceRepository, SqliteSubstanceRepository>();
            services.AddSingleton<IInteractionRepository, SqliteInteractionRepository>();
            services.AddSingleton<IDrugExplorerRepository, SqliteDrugExplorerRepository>();

            services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();
            services.AddSingleton<IIcdCodeRepository, SqliteIcdCodeRepository>();
            services.AddSingleton<IPolishDrugRegistryRepository, SqlitePolishDrugRegistryRepository>();

            services.AddSingleton<IDatabaseStatusService, SqliteDatabaseStatusService>();
            services.AddSingleton<IDataManagementService, DisabledDataManagementService>();
        }
        else
        {
            services.AddSingleton<IDrugRepository, PostgresDrugRepository>();
            services.AddSingleton<ISubstanceRepository, PostgresSubstanceRepository>();
            services.AddSingleton<IInteractionRepository, PostgresInteractionRepository>();
            services.AddSingleton<IDrugExplorerRepository, PostgresDrugExplorerRepository>();

            services.AddSingleton<IAuditLogRepository, PostgresAuditLogRepository>();
            services.AddSingleton<IIcdCodeRepository, PostgresIcdCodeRepository>();
            services.AddSingleton<IPolishDrugRegistryRepository, PostgresPolishDrugRegistryRepository>();

            services.AddSingleton<IDatabaseStatusRepository, PostgresDatabaseStatusRepository>();
            services.AddSingleton<IDataManagementRepository, PostgresDataManagementRepository>();
        }

        services.AddTransient<InteractionAnalysisService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

       // await ShowStartupDatabaseStatsAsync(mainWindow);
    }

    private async Task ShowStartupDatabaseStatsAsync(Window owner)
    {
        if (_serviceProvider is null)
            return;

        try
        {
            var databaseStatusService = _serviceProvider.GetRequiredService<IDatabaseStatusService>();
            var status = await databaseStatusService.GetDatabaseStatusAsync();

            var viewModel = new DatabaseStatsViewModel(status);

            var statsWindow = new DatabaseStatsWindow(viewModel)
            {
                Owner = owner
            };

            statsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not load database statistics.\n\n{ex.Message}",
                "Database statistics unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}