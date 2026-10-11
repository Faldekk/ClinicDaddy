using System.IO;
using System.Text;
using System.Globalization;
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

        // Quoting preserves CSV structure, but does not prevent spreadsheet formulas.
        // Inspect ignored prefixes without removing any original document content.
        var first = 0;
        while (first < value.Length && (char.IsWhiteSpace(value[first])
            || char.IsControl(value[first])
            || char.GetUnicodeCategory(value[first]) == UnicodeCategory.Format))
            first++;
        var literal = first < value.Length && value[first] is '=' or '+' or '-' or '@'
            ? "'" + value
            : value;
        var escaped = literal.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
