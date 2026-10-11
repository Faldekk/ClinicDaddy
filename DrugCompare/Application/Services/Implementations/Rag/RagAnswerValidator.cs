using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.Rag;
using System.Text.RegularExpressions;

namespace DrugCompare.Application.Services.Implementations.Rag;

public sealed class RagAnswerValidator : IRagAnswerValidator
{
    private static readonly HashSet<string> AllowedStatuses = ["reviewed", "verified"];

    public RagAnswerValidationResult Validate(RagAnswer answer, IReadOnlyList<KnowledgeChunkResult> suppliedSources)
    {
        if (answer.Findings is null) return Invalid("Model returned invalid findings.");
        if (answer.InsufficientEvidence)
            return answer.Findings.Count == 0 ? new RagAnswerValidationResult { IsValid = true } : Invalid("An insufficient-evidence response cannot contain findings.");
        if (answer.Findings.Count == 0) return Invalid("Model returned no source-backed findings.");
        if (answer.Findings.Count > 4) return Invalid("Return at most four findings, without padding the answer.");

        var allowedIds = suppliedSources.Where(source => AllowedStatuses.Contains(source.ReviewStatus)).Select(source => source.Id).ToHashSet();
        var usedQuotes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in answer.Findings)
        {
            if (finding is null || string.IsNullOrWhiteSpace(finding.Claim) || finding.SourceIds is null || finding.SourceIds.Count == 0)
                return Invalid("Every finding requires a claim and at least one source_id.");
            if (finding.SourceIds.Any(id => !allowedIds.Contains(id)))
                return Invalid("Model cited a source that was not supplied as reviewed or verified evidence.");
            var quote = Normalize(finding.EvidenceQuote ?? string.Empty);
            if (quote.Count(character => character == '•') > 1)
                return Invalid("Do not merge separate bullet points into one finding. Use a quote covering only one bullet point and preserve its conditions.");
            if (!usedQuotes.Add(quote))
                return Invalid("Duplicate evidenceQuote: combine the supported conditions into one finding. Do not add filler or repeated findings.");
            if (quote.Length < 20 || !suppliedSources.Any(source => finding.SourceIds.Contains(source.Id)
                && Normalize(source.ChunkText).Contains(quote, StringComparison.Ordinal)))
                return Invalid("Every finding requires a verbatim evidenceQuote from a cited, supplied fragment (at least 20 characters).");
        }

        return new RagAnswerValidationResult { IsValid = true };
    }

    private static RagAnswerValidationResult Invalid(string error) => new() { IsValid = false, Error = error };
    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
