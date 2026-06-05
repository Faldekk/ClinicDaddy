namespace DrugCompare.Models;

public sealed class InteractionHistoryItem
{
    public long Id { get; set; }

    public string AcceptedSubstancesText { get; set; } = string.Empty;

    public string ResultsText { get; set; } = string.Empty;

    public string? HighestSeverity { get; set; }

    public DateTime CreatedAt { get; set; }
}