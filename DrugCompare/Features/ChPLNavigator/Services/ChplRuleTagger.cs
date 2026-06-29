using DrugCompare.Features.ChPLNavigator.Models;

namespace DrugCompare.Features.ChPLNavigator.Services;

public sealed class ChplRuleTagger
{
    public void TagSections(IEnumerable<ChplSection> sections)
    {
        foreach (var section in sections)
        {
            section.Tags.Clear();

            AddSectionBasedTags(section);
            AddTextBasedTags(section);

            section.Tags = section.Tags
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }
    }

    private static void AddSectionBasedTags(ChplSection section)
    {
        switch (section.SectionNumber)
        {
            case "4.2":
                section.Tags.Add("dose");
                section.Tags.Add("administration");
                break;

            case "4.3":
                section.Tags.Add("contraindication");
                break;

            case "4.4":
                section.Tags.Add("warning");
                break;

            case "4.5":
                section.Tags.Add("interaction");
                break;

            case "4.6":
                section.Tags.Add("pregnancy");
                section.Tags.Add("lactation");
                break;

            case "4.8":
                section.Tags.Add("adverse_reaction");
                break;

            case "4.9":
                section.Tags.Add("overdose");
                break;

            case "5.1":
                section.Tags.Add("pharmacodynamics");
                break;

            case "5.2":
                section.Tags.Add("pharmacokinetics");
                break;

            case "6.1":
                section.Tags.Add("excipients");
                break;
        }
    }

    private static void AddTextBasedTags(ChplSection section)
    {
        var text = $"{section.Title} {section.Text}".ToLowerInvariant();

        AddIfContains(section, text, "monitor", "monitoring");
        AddIfContains(section, text, "nie stosować", "avoid_candidate");
        AddIfContains(section, text, "przeciwwskazan", "contraindication_candidate");

        AddIfContains(section, text, "interakc", "interaction");
        AddIfContains(section, text, "cyp3a4", "CYP3A4");
        AddIfContains(section, text, "cyp2d6", "CYP2D6");
        AddIfContains(section, text, "cyp2c9", "CYP2C9");
        AddIfContains(section, text, "cyp2c19", "CYP2C19");
        AddIfContains(section, text, "p-gp", "P-gp");

        AddIfContains(section, text, "qt", "qt_prolongation");
        AddIfContains(section, text, "krwaw", "bleeding_risk");
        AddIfContains(section, text, "serotonin", "serotonergic");
        AddIfContains(section, text, "nerek", "renal_impairment");
        AddIfContains(section, text, "wątro", "hepatic_impairment");

        AddIfContains(section, text, "zmniejszenie dawki", "dose_reduction_candidate");
        AddIfContains(section, text, "zwiększenie dawki", "dose_increase_candidate");
        AddIfContains(section, text, "dostosowanie dawki", "dose_adjustment_candidate");
    }

    private static void AddIfContains(
        ChplSection section,
        string text,
        string needle,
        string tag)
    {
        if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            section.Tags.Add(tag);
        }
    }
}