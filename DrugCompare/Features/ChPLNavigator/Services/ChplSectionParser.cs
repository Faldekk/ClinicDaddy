using System.Globalization;
using System.Text.RegularExpressions;
using DrugCompare.Features.ChPLNavigator.Models;

namespace DrugCompare.Features.ChPLNavigator.Services;

public sealed class ChplSectionParser
{
    private static readonly Regex SectionNumberRegex = new(
        @"(?<!\d)(?<number>[1-9]\.\d)(?!\d)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ExportedSections = new()
    {
        "4.1",
        "4.2",
        "4.3",
        "4.4",
        "4.5",
        "4.6",
        "4.7",
        "4.8",
        "4.9",
        "5.1",
        "5.2",
        "5.3",
        "6.1"
    };

    private static readonly HashSet<string> BoundarySections = new()
    {
        "6.2"
    };

    private static readonly Dictionary<string, string[]> KnownSectionTitleHints = new()
    {
        ["4.1"] =
        [
            "wskazania do stosowania",
            "wskazania"
        ],

        ["4.2"] =
        [
            "dawkowanie i sposób podawania",
            "dawkowanie",
            "sposób podawania"
        ],

        ["4.3"] =
        [
            "przeciwwskazania"
        ],

        ["4.4"] =
        [
            "specjalne ostrzeżenia",
            "środki ostrożności",
            "specjalne ostrzeżenia i środki ostrożności",
            "ostrzeżenia i środki ostrożności"
        ],

        ["4.5"] =
        [
            "interakcje z innymi produktami leczniczymi",
            "inne rodzaje interakcji",
            "interakcje"
        ],

        ["4.6"] =
        [
            "wpływ na płodność",
            "ciąża i laktacja",
            "płodność",
            "ciąża",
            "laktacja"
        ],

        ["4.7"] =
        [
            "wpływ na zdolność prowadzenia pojazdów",
            "obsługiwania maszyn",
            "prowadzenia pojazdów"
        ],

        ["4.8"] =
        [
            "działania niepożądane"
        ],

        ["4.9"] =
        [
            "przedawkowanie"
        ],

        ["5.1"] =
        [
            "właściwości farmakodynamiczne",
            "farmakodynamiczne"
        ],

        ["5.2"] =
        [
            "właściwości farmakokinetyczne",
            "farmakokinetyczne"
        ],

        ["5.3"] =
        [
            "przedkliniczne dane o bezpieczeństwie",
            "dane o bezpieczeństwie"
        ],

        ["6.1"] =
        [
            "wykaz substancji pomocniczych",
            "substancje pomocnicze"
        ],
        ["6.2"] =
        [
            "niezgodności farmaceutyczne",
            "niezgodności"
        ]
    };

    public List<ChplSection> ParseSections(string text)
    {
        var sections = new List<ChplSection>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return sections;
        }

        var normalizedText = NormalizeText(text);

        var matches = SectionNumberRegex
            .Matches(normalizedText)
            .Cast<Match>()
            .Where(m => IsKnownSection(GetSectionNumber(m)))
            .Where(m => !IsReferenceToSection(normalizedText, m))
            .Where(m => LooksLikeRealSectionHeader(normalizedText, m))
            .OrderBy(GetNumberStartIndex)
            .ToList();

        if (matches.Count == 0)
        {
            return sections;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var current = matches[i];
            var next = i + 1 < matches.Count ? matches[i + 1] : null;

            var number = GetSectionNumber(current);

            // 6.1 jest tylko granicą końca 5.3, nie eksportujemy jej.
            if (!ExportedSections.Contains(number))
            {
                continue;
            }

            var sectionStart = GetNumberEndIndex(current);
            var sectionEnd = next is null
                ? normalizedText.Length
                : GetNumberStartIndex(next);

            if (sectionEnd <= sectionStart)
            {
                continue;
            }

            var sectionContent = normalizedText[sectionStart..sectionEnd].Trim();

            if (string.IsNullOrWhiteSpace(sectionContent))
            {
                continue;
            }

            var title = ExtractTitle(number, sectionContent);
            var body = ExtractBody(sectionContent, title);

            sections.Add(new ChplSection
            {
                SectionNumber = number,
                Title = title,
                Text = body,
                ReviewStatus = "needs_review"
            });
        }

        return sections
            .GroupBy(s => s.SectionNumber)
            .Select(g => g.First())
            .OrderBy(s => ParseSectionNumber(s.SectionNumber))
            .ToList();
    }

    private static bool IsKnownSection(string sectionNumber)
    {
        return ExportedSections.Contains(sectionNumber) ||
               BoundarySections.Contains(sectionNumber);
    }

    private static string NormalizeText(string text)
    {
        var result = text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace('\u00A0', ' ');

        result = Regex.Replace(
            result,
            @"(?<!\d)([1-9])\s+\.\s+(\d)(?!\d)",
            "$1.$2");

        foreach (var section in KnownSectionTitleHints)
        {
            foreach (var hint in section.Value)
            {
                var escapedNumber = Regex.Escape(section.Key);
                var escapedHint = Regex.Escape(hint);

                result = Regex.Replace(
                    result,
                    @$"\s+(?={escapedNumber}\s+{escapedHint})",
                    "\n",
                    RegexOptions.IgnoreCase);
            }
        }

        result = Regex.Replace(result, @"\n{3,}", "\n\n");

        return result.Trim();
    }

    private static string GetSectionNumber(Match match)
    {
        return match.Groups["number"].Value.Trim();
    }

    private static int GetNumberStartIndex(Match match)
    {
        return match.Groups["number"].Index;
    }

    private static int GetNumberEndIndex(Match match)
    {
        var group = match.Groups["number"];
        return group.Index + group.Length;
    }

    private static bool LooksLikeRealSectionHeader(string text, Match match)
    {
        var number = GetSectionNumber(match);

        var afterStart = GetNumberEndIndex(match);
        var afterLength = Math.Min(260, text.Length - afterStart);

        if (afterLength <= 0)
        {
            return false;
        }

        var after = text
            .Substring(afterStart, afterLength)
            .TrimStart()
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(after))
        {
            return false;
        }

        if (!KnownSectionTitleHints.TryGetValue(number, out var hints))
        {
            return false;
        }

        foreach (var hint in hints)
        {
            if (after.StartsWith(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var compactAfter = Regex.Replace(after, @"\s+", " ");

        foreach (var hint in hints)
        {
            if (compactAfter.StartsWith(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReferenceToSection(string text, Match match)
    {
        var sectionNumber = GetSectionNumber(match);

        var beforeStart = Math.Max(0, GetNumberStartIndex(match) - 90);
        var beforeLength = GetNumberStartIndex(match) - beforeStart;

        var beforeFull = text
            .Substring(beforeStart, beforeLength)
            .ToLowerInvariant();

        var afterStart = GetNumberEndIndex(match);
        var afterLength = Math.Min(80, text.Length - afterStart);

        var after = afterLength > 0
            ? text.Substring(afterStart, afterLength).ToLowerInvariant()
            : string.Empty;

        var before = GetCurrentSentencePrefix(beforeFull);

        var referenceBeforePatterns = new[]
        {
            "patrz punkt",
            "patrz pkt",
            "patrz sekcja",
            "patrz rozdział",
            "patrz także punkt",
            "patrz również punkt",
            "zob. punkt",
            "zob. pkt",
            "zobacz punkt",
            "zobacz pkt",
            "zgodnie z punktem",
            "zgodnie z pkt",
            "opisano w punkcie",
            "opisane w punkcie",
            "w punkcie",
            "w pkt"
        };

        if (referenceBeforePatterns.Any(pattern =>
                before.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var localStart = Math.Max(0, GetNumberStartIndex(match) - 35);
        var localEnd = Math.Min(text.Length, GetNumberEndIndex(match) + 35);
        var local = text[localStart..localEnd].ToLowerInvariant();

        var indexOfNumberInLocal = local.IndexOf(sectionNumber, StringComparison.OrdinalIgnoreCase);

        if (indexOfNumberInLocal >= 0)
        {
            var beforeNumberLocal = local[..indexOfNumberInLocal];

            var lastOpenParen = beforeNumberLocal.LastIndexOf('(');
            var lastCloseParen = beforeNumberLocal.LastIndexOf(')');

            var isInsideOpenParen = lastOpenParen > lastCloseParen;

            if (isInsideOpenParen &&
                (beforeNumberLocal.Contains("patrz", StringComparison.OrdinalIgnoreCase) ||
                 beforeNumberLocal.Contains("zob.", StringComparison.OrdinalIgnoreCase) ||
                 beforeNumberLocal.Contains("zobacz", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        var trimmedAfter = after.TrimStart();

        if (trimmedAfter.StartsWith(")") ||
            trimmedAfter.StartsWith(",") ||
            trimmedAfter.StartsWith(";"))
        {
            return true;
        }

        return false;
    }

    private static string GetCurrentSentencePrefix(string before)
    {
        if (string.IsNullOrWhiteSpace(before))
        {
            return string.Empty;
        }

        var cutPositions = new[]
        {
            before.LastIndexOf('\n'),
            before.LastIndexOf(". "),
            before.LastIndexOf(")."),
            before.LastIndexOf("; "),
            before.LastIndexOf(": ")
        };

        var lastCut = cutPositions.Max();

        if (lastCut < 0)
        {
            return before;
        }

        return before[(lastCut + 1)..].Trim();
    }

    private static string ExtractTitle(string sectionNumber, string sectionContent)
    {
        var firstLine = sectionContent
            .Split('\n')
            .FirstOrDefault()
            ?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return GetFallbackTitle(sectionNumber);
        }

        var title = firstLine;

        var doubleSpace = Regex.Match(title, @"\s{2,}");

        if (doubleSpace.Success && doubleSpace.Index > 5)
        {
            title = title[..doubleSpace.Index].Trim();
        }

        title = CutAtStopMarkers(title);
        title = PreferKnownTitle(sectionNumber, title);

        if (title.Length > 180)
        {
            title = title[..180].Trim();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return GetFallbackTitle(sectionNumber);
        }

        return title.Trim();
    }

    private static string ExtractBody(string sectionContent, string title)
    {
        var content = sectionContent.Trim();

        if (!string.IsNullOrWhiteSpace(title) &&
            content.StartsWith(title, StringComparison.OrdinalIgnoreCase))
        {
            content = content[title.Length..].Trim();
        }

        content = Regex.Replace(content, @"^\s{2,}", string.Empty);

        return content;
    }

    private static string PreferKnownTitle(string sectionNumber, string title)
    {
        if (!KnownSectionTitleHints.TryGetValue(sectionNumber, out var hints))
        {
            return title;
        }

        var lowerTitle = title.ToLowerInvariant();

        foreach (var hint in hints.OrderByDescending(h => h.Length))
        {
            var index = lowerTitle.IndexOf(hint, StringComparison.OrdinalIgnoreCase);

            if (index == 0)
            {
                return title[..Math.Min(title.Length, hint.Length)].Trim();
            }

            if (index > 0 && index <= 15)
            {
                return title[index..Math.Min(title.Length, index + hint.Length)].Trim();
            }
        }

        return title;
    }

    private static string CutAtStopMarkers(string value)
    {
        var markers = new[]
        {
            " Produkt leczniczy ",
            " Produkt ",
            " Ten produkt ",
            " Lek ",
            " Należy ",
            " Nie należy ",
            " W przypadku ",
            " Zaleca się ",
            " Zaleca ",
            " Dawka ",
            " Dorośli ",
            " Dzieci ",
            " Pacjenci ",
            " U pacjentów ",
            " Podanie ",
            " Stosowanie ",
            " Leczenie ",
            " Podczas ",
            " Jednoczesne ",
            " Jednocześnie ",
            " Przed ",
            " Po ",
            " Nadwrażliwość ",
            " Reakcje "
        };

        var result = value;

        foreach (var marker in markers)
        {
            var index = result.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (index > 15)
            {
                result = result[..index].Trim();
                break;
            }
        }

        return result;
    }

    private static string GetFallbackTitle(string sectionNumber)
    {
        return sectionNumber switch
        {
            "4.1" => "Wskazania do stosowania",
            "4.2" => "Dawkowanie i sposób podawania",
            "4.3" => "Przeciwwskazania",
            "4.4" => "Specjalne ostrzeżenia i środki ostrożności dotyczące stosowania",
            "4.5" => "Interakcje z innymi produktami leczniczymi i inne rodzaje interakcji",
            "4.6" => "Wpływ na płodność, ciążę i laktację",
            "4.7" => "Wpływ na zdolność prowadzenia pojazdów i obsługiwania maszyn",
            "4.8" => "Działania niepożądane",
            "4.9" => "Przedawkowanie",
            "5.1" => "Właściwości farmakodynamiczne",
            "5.2" => "Właściwości farmakokinetyczne",
            "5.3" => "Przedkliniczne dane o bezpieczeństwie",
            "6.1" => "Wykaz substancji pomocniczych",
            "6.2" => "Niezgodności farmaceutyczne",
            _ => "Sekcja ChPL"
        };
    }

    private static double ParseSectionNumber(string sectionNumber)
    {
        return double.TryParse(
            sectionNumber,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
    }
}