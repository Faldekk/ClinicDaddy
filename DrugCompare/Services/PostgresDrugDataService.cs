using DrugCompare.Repositories.Contracts;
using DrugCompare.Services.Contracts;
using DrugCompare.Models;
using DrugCompare.Views;
using DrugCompare.ViewModels;
using DrugCompare.Services;
namespace DrugCompare.Services;

public sealed class PostgresDrugDataService :
    IDrugLookupService,
    ISubstanceLookupService,
    ISubstanceSynonymService,
    IInteractionCheckerService,
    IInteractionHistoryService,
    IDatabaseStatusService,
    IDataManagementService
{
    private readonly IDrugRepository _drugRepository;
    private readonly ISubstanceRepository _substanceRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly IInteractionHistoryRepository _interactionHistoryRepository;
    private readonly IDatabaseStatusRepository _databaseStatusRepository;
    private readonly IDataManagementRepository _dataManagementRepository;


    public PostgresDrugDataService(
    IDrugRepository drugRepository,
    ISubstanceRepository substanceRepository,
    IInteractionRepository interactionRepository,
    IInteractionHistoryRepository interactionHistoryRepository,
    IDatabaseStatusRepository databaseStatusRepository,
    IDataManagementRepository dataManagementRepository)
    {
        _drugRepository = drugRepository;
        _substanceRepository = substanceRepository;
        _interactionRepository = interactionRepository;
        _interactionHistoryRepository = interactionHistoryRepository;
        _databaseStatusRepository = databaseStatusRepository;
        _dataManagementRepository = dataManagementRepository;
    }
    
    public Task<DataManagementStatusResult> GetDataManagementStatusAsync()
    {
        return _dataManagementRepository.GetDataManagementStatusAsync();
    }

    public Task<DrugLookupResult?> FindDrugAsync(string drugName)
    {
        return _drugRepository.FindDrugAsync(drugName);
    }

    public Task<ActiveSubstanceItem?> FindActiveSubstanceAsync(string substanceName)
    {
        return _substanceRepository.FindActiveSubstanceAsync(substanceName);
    }

    public Task AddSynonymAsync(
        long activeSubstanceId,
        string synonym,
        string source = "manual")
    {
        return _substanceRepository.AddSynonymAsync(activeSubstanceId, synonym, source);
    }

    public Task<List<ActiveSubstanceSynonymItem>> GetSynonymsAsync(long activeSubstanceId)
    {
        return _substanceRepository.GetSynonymsAsync(activeSubstanceId);
    }

    public Task<List<InteractionResult>> CheckInteractionsAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances)
    {
        return _interactionRepository.CheckInteractionsAsync(substances);
    }

    public Task SaveInteractionCheckAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances,
        IReadOnlyCollection<InteractionResult> results)
    {
        return _interactionHistoryRepository.SaveInteractionCheckAsync(substances, results);
    }

    public Task<List<InteractionHistoryItem>> GetRecentHistoryAsync(int limit = 20)
    {
        return _interactionHistoryRepository.GetRecentHistoryAsync(limit);
    }

    public Task<DatabaseStatusResult> GetDatabaseStatusAsync()
    {
        return _databaseStatusRepository.GetDatabaseStatusAsync();
    }
}