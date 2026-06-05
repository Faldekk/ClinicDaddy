namespace DrugCompare.Models;

public sealed class ActiveSubstanceSynonymItem
{
    public long Id { get; set; }

    public long ActiveSubstanceId { get; set; }

    public string Synonym { get; set; } = string.Empty;

    public string NormalizedSynonym { get; set; } = string.Empty;

    public string Source { get; set; } = "manual";

    public DateTime CreatedAt { get; set; }
}