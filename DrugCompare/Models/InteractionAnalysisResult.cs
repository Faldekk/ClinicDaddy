namespace DrugCompare.Models;

public sealed class InteractionAnalysisResult
{
    public List<InteractionResult> Interactions { get; set; } = new();

    public string? HighestSeverity { get; set; }

    public string SummaryMessage { get; set; } = string.Empty;
}