namespace DrugCompare.Application.Models.Rag;

public sealed class RagAnswer
{
    public string Summary { get; set; } = string.Empty;
    public List<RagFinding> Findings { get; set; } = [];
    public bool InsufficientEvidence { get; set; }
}

public sealed class RagFinding
{
    public string Claim { get; set; } = string.Empty;
    public List<long> SourceIds { get; set; } = [];
    public string EvidenceQuote { get; set; } = string.Empty;
}

public sealed class RagAnswerValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
}
