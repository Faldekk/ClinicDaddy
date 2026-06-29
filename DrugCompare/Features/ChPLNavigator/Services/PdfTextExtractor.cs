using System.IO;
using System.Text;
using UglyToad.PdfPig;

namespace DrugCompare.Features.ChPLNavigator.Services;

public sealed class PdfTextExtractor
{
    public string ExtractText(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("PDF path is empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("PDF file was not found.", filePath);
        }

        var builder = new StringBuilder();

        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}