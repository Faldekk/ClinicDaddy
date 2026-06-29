namespace DrugCompare.Features.ChPLNavigator.Models;

public sealed class ChplDocument
{
    public string SourceFile { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "ChPL";
    public string Language { get; set; } = "pl";
    public string? ProductName { get; set; }
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    public List<ChplSection> Sections { get; set; } = new();
}