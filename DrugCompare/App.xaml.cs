using DrugCompare.Application.Repositories.Contracts;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Application.Services.Implementations;
using DrugCompare.Features.DrugExplorer;
using DrugCompare.Features.IcdLooker;
using DrugCompare.Features.InteractionChecker;
using DrugCompare.Features.PolishRegistry;
using DrugCompare.Infrastructure.SQLite;
using DrugCompare.ViewModels;
using DrugCompare.ViewModels.Interaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DrugCompare;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
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

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void RegisterDatabase(IConfiguration configuration, IServiceCollection services)
    {
        var provider = configuration["Database:Provider"] ?? "SQLite";

        if (provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            RegisterSqliteServices(services);
            return;
        }

        throw new InvalidOperationException($"Unsupported database provider: {provider}");
    }

    private static void RegisterSqliteServices(IServiceCollection services)
    {
        services.AddSingleton<SqliteConnectionFactory>();

        services.AddSingleton<IDrugRepository, SqliteDrugRepository>();
        services.AddSingleton<ISubstanceRepository, SqliteSubstanceRepository>();
        services.AddSingleton<IInteractionRepository, SqliteInteractionRepository>();
        services.AddSingleton<IDrugExplorerRepository, SqliteDrugExplorerRepository>();
        services.AddSingleton<IInteractionHistoryRepository, SqliteInteractionHistoryRepository>();

        services.AddSingleton<IPolishDrugRegistryRepository, SqlitePolishDrugRegistryRepository>();
        services.AddSingleton<IIcdCodeRepository, SqliteIcdCodeRepository>();
        services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();

        services.AddSingleton<IDatabaseStatusRepository, SqliteDatabaseStatusRepository>();
        services.AddSingleton<IDataManagementRepository, DisabledDataManagementRepository>();

        services.AddSingleton<IDatabaseStatusService, DatabaseStatusService>();
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
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<InteractionCheckerViewModel>();
        services.AddSingleton<DrugExplorerViewModel>();
        services.AddSingleton<PolishDrugRegistryViewModel>();
        services.AddSingleton<IcdLookerViewModel>();

        services.AddSingleton<MainViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
    }
}