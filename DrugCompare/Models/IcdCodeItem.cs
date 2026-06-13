namespace DrugCompare.Models;

public class IcdCodeItem
{
    public long Id { get; set; }

    public string Code { get; set; } = "";
    public string NormalizedCode { get; set; } = "";

    public string Title { get; set; } = "";
    public string NormalizedTitle { get; set; } = "";

    public string? Description { get; set; }
    public string? Chapter { get; set; }
    public string? ParentCode { get; set; }

    public string Source { get; set; } = "ICD";
    public string? Version { get; set; }

    public DateTime ImportedAt { get; set; }
}