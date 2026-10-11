using DrugCompare.Application.Models;

namespace DrugCompare.Application.Services.Contracts.KnowledgeBase;

public sealed record ChplImportFailure(long ProductId, string ProductName, string Message);
public sealed record ChplImportProgress(int Completed, int Total, int Imported, int Skipped, int Failed, string ProductName);
public sealed record ChplBatchImportResult(int Imported, int Skipped, bool Cancelled, string? BackupPath, IReadOnlyList<ChplImportFailure> Failures);

public interface IChplBatchImportService
{
    Task<ChplBatchImportResult> ImportAsync(int limit, IProgress<ChplImportProgress>? progress,
        CancellationToken cancellationToken, IReadOnlyCollection<long>? retryProductIds = null);
}

public interface IChplProductImporter
{
    Task<KnowledgeBaseIngestionResult> ImportAsync(PolishDrugRegistryItem product, CancellationToken cancellationToken);
}
