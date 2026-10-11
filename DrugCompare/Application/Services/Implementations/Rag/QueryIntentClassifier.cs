using DrugCompare.Application.Models.Rag;

namespace DrugCompare.Application.Services.Implementations.Rag;

public static class QueryIntentClassifier
{
    public static QueryIntent Classify(string query)
    {
        var text = query.ToLowerInvariant();
        if (Contains(text, "interakc", "łączyć", "poląc", "polącz", "razem z")) return QueryIntent.Interaction;
        if (Contains(text, "przeciwwskaz", "nie stosować", "zakaz")) return QueryIntent.Contraindication;
        if (Contains(text, "ciąża", "ciaża", "karmien", "laktac", "płodno", "plodno")) return QueryIntent.PregnancyLactation;
        if (Contains(text, "działani", "dzialani", "skutki uboczne", "niepożądan", "niepozadan")) return QueryIntent.AdverseReaction;
        if (Contains(text, "dawk", "dawkowan", "ile", "stosować")) return QueryIntent.Posology;
        if (Contains(text, "nerk", "renal")) return QueryIntent.RenalImpairment;
        if (Contains(text, "wątrob", "watrob", "hepatic")) return QueryIntent.HepaticImpairment;
        if (Contains(text, "farmakokin", "metabolizm", "eliminac")) return QueryIntent.Pharmacokinetics;
        if (Contains(text, "przedawk", "overdose")) return QueryIntent.Overdose;
        if (Contains(text, "substancj", "laktoz", "barwnik", "alerg", "składnik", "skladnik")) return QueryIntent.Excipient;
        if (Contains(text, "ostrzeż", "ostrzez", "uwaga")) return QueryIntent.Warning;
        return QueryIntent.Unknown;
    }

    public static IReadOnlyList<string> PreferredSections(QueryIntent intent) => intent switch
    {
        QueryIntent.Interaction => ["4.5", "4.4", "4.3", "5.2", "4.8"],
        QueryIntent.Contraindication => ["4.3", "4.4", "4.5", "4.8"],
        QueryIntent.Warning => ["4.4", "4.3", "4.5", "4.8"],
        QueryIntent.PregnancyLactation => ["4.6", "4.3", "4.4"],
        QueryIntent.AdverseReaction => ["4.8", "4.4", "4.9"],
        QueryIntent.Posology or QueryIntent.RenalImpairment or QueryIntent.HepaticImpairment => ["4.2", "4.4", "5.2"],
        QueryIntent.Excipient => ["6.1", "4.3", "4.4"],
        QueryIntent.Overdose => ["4.9", "4.4", "5.2"],
        QueryIntent.Pharmacokinetics => ["5.2", "4.2", "4.4"],
        _ => []
    };

    private static bool Contains(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.Ordinal));
}
