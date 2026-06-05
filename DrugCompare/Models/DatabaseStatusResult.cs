namespace DrugCompare.Models;

public sealed class DatabaseStatusResult
{
    public long DrugsCount { get; set; }

    public long ActiveSubstancesCount { get; set; }

    public long DrugActiveSubstancesCount { get; set; }

    public long SubstanceInteractionsCount { get; set; }
}