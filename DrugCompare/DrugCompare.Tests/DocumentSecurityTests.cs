using System.Diagnostics;
using DrugCompare.Application.Models;
using DrugCompare.Application.Models.Rag;
using DrugCompare.Features.EvidenceAssistant;
using DrugCompare.Features.PolishRegistry;
using DrugCompare.Features.ChPLNavigator.Models;
using DrugCompare.Features.ChPLNavigator.Services;
using DrugCompare.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DrugCompare.Tests;

[TestClass]
public sealed class DocumentSecurityTests
{
    [DataTestMethod]
    [DataRow("C:\\Windows\\System32\\notepad.exe")]
    [DataRow("\\\\server\\share\\run.exe")]
    [DataRow("file:///C:/Windows/System32/notepad.exe")]
    [DataRow("ms-settings:privacy")]
    [DataRow("javascript:alert(1)")]
    [DataRow("ftp://example.org/test.pdf")]
    [DataRow("test.pdf")]
    [DataRow("//example.org/test.pdf")]
    [DataRow("httpx://example.org/test.pdf")]
    [DataRow("https://user:password@example.org/test.pdf")]
    [DataRow("https://example.org/test.pdf\r\nfile:///test")]
    public void AllDocumentCommands_RejectNonWebOrAmbiguousTargets(string value)
    {
        var launched = new List<ProcessStartInfo>();
        var launcher = new WebDocumentLauncher(launched.Add);
        Assert.ThrowsException<ArgumentException>(() => launcher.Open(value));
        var registry = new PolishDrugRegistryViewModel(null!, null!, launcher)
        { SelectedResult = new PolishDrugRegistryItem { ChplUrl = value, LeafletUrl = value } };
        registry.OpenChplCommand.Execute(null);
        StringAssert.Contains(registry.StatusMessage, "Nie udało się");
        registry.OpenLeafletCommand.Execute(null);
        var evidence = new EvidenceAssistantViewModel(null!, null!, null!, null!, launcher)
        { SelectedResult = new KnowledgeChunkResult { SourceUrl = value } };
        evidence.OpenSelectedSourceCommand.Execute(null);
        StringAssert.Contains(evidence.StatusMessage, "Nie udało się");
        Assert.AreEqual(0, launched.Count);
    }

    [DataTestMethod]
    [DataRow("https://example.org/document.pdf?x=1&y=2#page=3")]
    [DataRow("HTTP://example.org/document.pdf")]
    [DataRow("https://example.org/żółć.pdf")]
    public void AllDocumentCommands_LaunchCanonicalWebUrl(string value)
    {
        var launched = new List<ProcessStartInfo>();
        var launcher = new WebDocumentLauncher(launched.Add);
        var registry = new PolishDrugRegistryViewModel(null!, null!, launcher)
        { SelectedResult = new PolishDrugRegistryItem { ChplUrl = value, LeafletUrl = value } };
        registry.OpenChplCommand.Execute(null);
        registry.OpenLeafletCommand.Execute(null);
        var evidence = new EvidenceAssistantViewModel(null!, null!, null!, null!, launcher)
        { SelectedResult = new KnowledgeChunkResult { SourceUrl = value } };
        evidence.OpenSelectedSourceCommand.Execute(null);
        Assert.AreEqual(3, launched.Count);
        foreach (var info in launched)
        {
            Assert.AreEqual(new Uri(value).AbsoluteUri, info.FileName);
            Assert.IsTrue(info.UseShellExecute);
            Assert.AreEqual(string.Empty, info.Arguments);
        }
    }

    [DataTestMethod]
    [DataRow("=1+1")]
    [DataRow("+1+1")]
    [DataRow("-1+1")]
    [DataRow("@SUM(1)")]
    [DataRow(" \t\r\n=1+1")]
    [DataRow("\u0001+1+1")]
    [DataRow("\u00a0-1+1")]
    [DataRow("\ufeff@SUM(1)")]
    [DataRow("\u200b=1+1")]
    public async Task Csv_EncodesEveryStringColumnAsLiteral_WithoutMutatingDocument(string value)
    {
        var section = new ChplSection
        { SectionNumber = value, Title = value, Text = value, Tags = [value], ReviewStatus = value };
        var document = new ChplDocument
        { SourceFile = value, DocumentType = value, Language = value, ProductName = value, Sections = [section] };
        var file = Path.Combine(Path.GetTempPath(), $"clinicdaddy-security-{Guid.NewGuid():N}.csv");
        try
        {
            await new ChplCsvExporter().ExportAsync(document, file);
            var csv = await File.ReadAllTextAsync(file);
            var row = string.Join(",", Enumerable.Repeat("\"'" + value.Replace("\"", "\"\"") + "\"", 9));
            StringAssert.EndsWith(csv, row + Environment.NewLine);
            Assert.AreEqual(value, document.SourceFile);
            Assert.AreEqual(value, document.DocumentType);
            Assert.AreEqual(value, document.Language);
            Assert.AreEqual(value, document.ProductName);
            Assert.AreEqual(value, section.SectionNumber);
            Assert.AreEqual(value, section.Title);
            Assert.AreEqual(value, section.Text);
            Assert.AreEqual(value, section.Tags[0]);
            Assert.AreEqual(value, section.ReviewStatus);
        }
        finally { File.Delete(file); }
    }

    [TestMethod]
    public async Task Csv_PreservesOrdinaryTextQuotesNewlinesAndEmptyValues()
    {
        var document = new ChplDocument { SourceFile = "źródło.pdf", ProductName = null,
            Sections = [new ChplSection { SectionNumber = "4.1", Title = "Wskazania",
                Text = "Zwykły tekst, \"cytat\"\nDrugi wiersz = nie formuła." }] };
        var file = Path.Combine(Path.GetTempPath(), $"clinicdaddy-security-{Guid.NewGuid():N}.csv");
        try
        {
            await new ChplCsvExporter().ExportAsync(document, file);
            var csv = await File.ReadAllTextAsync(file);
            StringAssert.Contains(csv, "\"źródło.pdf\",\"ChPL\",\"pl\",\"\",\"4.1\",\"Wskazania\",\"\",\"needs_review\"");
            StringAssert.Contains(csv, "\"Zwykły tekst, \"\"cytat\"\"\nDrugi wiersz = nie formuła.\"");
        }
        finally { File.Delete(file); }
    }
}
