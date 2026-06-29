using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using DrugCompare.Features.ChPLNavigator.Models;

namespace DrugCompare.Features.ChPLNavigator.Services;

public sealed class ChplJsonExporter
{
    public async Task ExportAsync(ChplDocument document, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var json = JsonSerializer.Serialize(document, options);

        await File.WriteAllTextAsync(filePath, json);
    }
}