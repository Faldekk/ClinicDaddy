using DrugCompare.Models;
using DrugCompare.Services.Contracts;

namespace DrugCompare.Services.Application;

public sealed class InteractionAnalysisService
{
    private readonly IInteractionCheckerService _interactionCheckerService;
    private readonly IInteractionHistoryService _interactionHistoryService;

    public InteractionAnalysisService(
        IInteractionCheckerService interactionCheckerService,
        IInteractionHistoryService interactionHistoryService)
    {
        _interactionCheckerService = interactionCheckerService;
        _interactionHistoryService = interactionHistoryService;
    }

    public async Task<InteractionAnalysisResult> AnalyzeAsync(
        IReadOnlyCollection<ActiveSubstanceItem> substances)
    {
        var interactions = await _interactionCheckerService.CheckInteractionsAsync(substances);

        await _interactionHistoryService.SaveInteractionCheckAsync(
            substances,
            interactions);

        var highestSeverity = interactions.Count == 0
            ? null
            : interactions
                .OrderByDescending(x => GetSeverityScore(x.Severity))
                .First()
                .Severity;

        return new InteractionAnalysisResult
        {
            Interactions = interactions,
            HighestSeverity = highestSeverity,
            SummaryMessage = BuildSummaryMessage(interactions)
        };
    }

    private static string BuildSummaryMessage(IReadOnlyCollection<InteractionResult> interactions)
    {
        if (interactions.Count == 0)
        {
            return "No known interaction was found in the local DDInter-based database. Missing interaction data does not mean that the combination is safe.";
        }

        return $"Known interaction(s) found: {interactions.Count}. Clinical verification is required.";
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
}