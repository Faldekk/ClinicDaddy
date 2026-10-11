using System.IO;
using System.Security.Cryptography;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Features.ChPLNavigator.Models;
using DrugCompare.Features.ChPLNavigator.Services;

namespace DrugCompare.Application.Services.Implementations.KnowledgeBase;

public sealed class ChplProductImporter(ChplPdfDownloader downloader, IKnowledgeBaseIngestionService ingestion)
    : IChplProductImporter
{
    public async Task<KnowledgeBaseIngestionResult> ImportAsync(PolishDrugRegistryItem product, CancellationToken cancellationToken)
    {
        var pdf = await downloader.DownloadAsync(product.ChplUrl!, product.Id, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var sections = new ChplSectionParser().ParseSections(new PdfTextExtractor().ExtractText(pdf.LocalFilePath));
        cancellationToken.ThrowIfCancellationRequested();
        if (sections.Count == 0)
            throw new InvalidOperationException("Nie wykryto sekcji ChPL. Dokument może wymagać OCR lub ręcznego importu.");
        new ChplRuleTagger().TagSections(sections);

        await using var file = File.OpenRead(pdf.LocalFilePath);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken)).ToLowerInvariant();
        return await ingestion.IngestChplDocumentAsync(new ChplDocument
        {
            RplProductId = product.Id,
            ProductName = product.ProductName,
            ActiveSubstanceText = product.ActiveSubstanceText,
            ChplUrl = product.ChplUrl,
            SourceFile = Path.GetFileName(pdf.LocalFilePath),
            ParsedAt = DateTime.UtcNow,
            Sections = sections
        }, pdf.LocalFilePath, hash, cancellationToken);
    }
}
