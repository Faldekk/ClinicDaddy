namespace DrugCompare.Models;

public sealed class DataManagementStatusResult
{
    public DataSourceVersionItem? LatestEmaImport { get; set; }

    public DataSourceVersionItem? LatestDdinterImport { get; set; }

    public List<DataSourceVersionItem> RecentImports { get; set; } = new();
}