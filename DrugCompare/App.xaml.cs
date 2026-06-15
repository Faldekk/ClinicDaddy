using DrugCompare.Database;
using DrugCompare.Repositories;
using DrugCompare.Repositories.Contracts;
using DrugCompare.Services;
using DrugCompare.Services.Application;
using DrugCompare.Services.Contracts;
using DrugCompare.ViewModels;
using DrugCompare.ViewModels.DrugExplorer;
using DrugCompare.ViewModels.ICD;
using DrugCompare.ViewModels.Interaction;
using DrugCompare.ViewModels.PolishRegistry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace DrugCompare;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(
                "appsettings.json",
                optional: false,
                reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        RegisterDatabase(configuration, services);
        RegisterApplicationServices(services);
        RegisterViewModels(services);
        RegisterViews(services);

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void RegisterDatabase(
        IConfiguration configuration,
        IServiceCollection services)
    {
        var provider = configuration["Database:Provider"] ?? "SQLite";

        if (string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            RegisterSqliteServices(services);
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported database provider: {provider}. Current portable version supports SQLite.");
    }

    private static void RegisterSqliteServices(IServiceCollection services)
    {
        services.AddSingleton<SqliteConnectionFactory>();

        services.AddSingleton<IDrugRepository, SqliteDrugRepository>();
        services.AddSingleton<ISubstanceRepository, SqliteSubstanceRepository>();
        services.AddSingleton<IInteractionRepository, SqliteInteractionRepository>();
        services.AddSingleton<IDrugExplorerRepository, SqliteDrugExplorerRepository>();

        services.AddSingleton<IPolishDrugRegistryRepository, SqlitePolishDrugRegistryRepository>();
        services.AddSingleton<IIcdCodeRepository, SqliteIcdCodeRepository>();
        services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();

        services.AddSingleton<IDatabaseStatusService, SqliteDatabaseStatusService>();
        services.AddSingleton<IDataManagementService, DisabledDataManagementService>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<PostgresDrugDataService>();

        services.AddSingleton<IDrugLookupService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<ISubstanceLookupService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<ISubstanceSynonymService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<IInteractionCheckerService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<IInteractionHistoryService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<IDrugExplorerService>(sp =>
            sp.GetRequiredService<PostgresDrugDataService>());

        services.AddSingleton<IPolishDrugRegistryService, PolishDrugRegistryService>();
        services.AddSingleton<IIcdCodeService, IcdCodeService>();
        services.AddSingleton<IAuditLogService, AuditLogService>();

        services.AddSingleton<InteractionAnalysisService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<InteractionCheckerViewModel>();
        services.AddSingleton<ICDLookerViewModel>();
        services.AddSingleton<DrugExplorerViewModel>();
        services.AddSingleton<PolishDrugRegistryViewModel>();

        services.AddSingleton<MainViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
    }
}