namespace DrugCompare.Models;

public class PolishDrugRegistryItem
{
    public long Id { get; set; }

    public string? RplId { get; set; }

    public string ProductName { get; set; } = "";
    public string NormalizedProductName { get; set; } = "";

    public string? ActiveSubstanceText { get; set; }
    public string? Strength { get; set; }
    public string? PharmaceuticalForm { get; set; }
    public string? MarketingAuthorizationHolder { get; set; }

    public string? AuthorizationNumber { get; set; }
    public string? AuthorizationValidity { get; set; }
    public string? ProductType { get; set; }
    public string? ProcedureType { get; set; }

    public string? ChplUrl { get; set; }
    public string? LeafletUrl { get; set; }

    public string Source { get; set; } = "RPL";
    public string? SourceVersion { get; set; }

    public DateTime ImportedAt { get; set; }
}