using CommunityToolkit.Mvvm.ComponentModel;
using DrugCompare.Models;

namespace DrugCompare.ViewModels;

public sealed class DatabaseStatsViewModel : ObservableObject
{
    public DatabaseStatsViewModel(DatabaseStatusResult status)
    {
        DrugsCount = status.DrugsCount;
        ActiveSubstancesCount = status.ActiveSubstancesCount;
        DrugActiveSubstancesCount = status.DrugActiveSubstancesCount;
        SubstanceInteractionsCount = status.SubstanceInteractionsCount;
    }

    public long DrugsCount { get; }

    public long ActiveSubstancesCount { get; }

    public long DrugActiveSubstancesCount { get; }

    public long SubstanceInteractionsCount { get; }

    public string Summary =>
        $"Drugs: {DrugsCount:N0} | Active substances: {ActiveSubstancesCount:N0} | Relations: {DrugActiveSubstancesCount:N0} | Interactions: {SubstanceInteractionsCount:N0}";
}