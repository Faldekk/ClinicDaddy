using System.IO;
using System.Text;
using DrugCompare.Features.ChPLNavigator.Models;

namespace DrugCompare.Features.ChPLNavigator.Services;

public sealed class ChplCsvExporter
{
    public async Task ExportAsync(ChplDocument document, string filePath)
    {
        var builder = new StringBuilder();

        builder.AppendLine("source_file,document_type,language,product_name,section_number,section_title,tags,review_status,text");

        foreach (var section in document.Sections)
        {
            builder.AppendLine(string.Join(",",
                Escape(document.SourceFile),
                Escape(document.DocumentType),
                Escape(document.Language),
                Escape(document.ProductName),
                Escape(section.SectionNumber),
                Escape(section.Title),
                Escape(string.Join(";", section.Tags)),
                Escape(section.ReviewStatus),
                Escape(section.Text)));
        }

        await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
