namespace DrugCompare.Features.ChPLNavigator.Models;

public sealed class ChplSection
{
    public string SectionNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    // Tymczasowo zostawione dla zgodności ze starym importerem.
    public List<string> Tags { get; set; } = new();

    public List<string> SearchKeywords { get; set; } = new();
    public List<string> CandidateFlags { get; set; } = new();

    public string ReviewStatus { get; set; } = "needs_review";

    public string TagsText => string.Join("; ", Tags);
    public string SearchKeywordsText => string.Join("; ", SearchKeywords);
    public string CandidateFlagsText => string.Join("; ", CandidateFlags);
}