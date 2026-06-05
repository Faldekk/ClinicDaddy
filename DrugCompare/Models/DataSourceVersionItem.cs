namespace DrugCompare.Models;

public sealed class DataSourceVersionItem
{
    public long Id { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; }

    public int RecordsImported { get; set; }

    public string? Notes { get; set; }

    public string? SourceUrl { get; set; }

    public string? Checksum { get; set; }

    public string ImportStatus { get; set; } = "Unknown";

    public string? ErrorMessage { get; set; }
}