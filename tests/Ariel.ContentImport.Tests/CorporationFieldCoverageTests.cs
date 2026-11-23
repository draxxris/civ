using Ariel.ContentImport;
using Xunit;

namespace Ariel.ContentImport.Tests;

/// <summary>Every field of the source's corporation records that carries data for any corporation is either mapped to
/// <c>CorporationDef</c> (and so read by a Corporations rule) or named here with the reason it is not. The default suite's
/// counterpart (<c>CorporationSourceRulesTests</c>) checks the committed rows' <c>unsupported</c> lists against the same
/// reasons.</summary>
[Trait("Category", "Extractor")]
public sealed class CorporationFieldCoverageTests
{
    /// <summary>Text, art and identity every content row carries, read by the common schema.</summary>
    private static readonly string[] Common = ["Type", "Description", "Civilopedia", "Button"];
    /// <summary>Source fields with data that no rule reads, and why.</summary>
    private static readonly Dictionary<string, string> Unread = new()
    {
        ["Sound"] = "presentation: the client plays no corporation sounds yet",
        ["MovieFile"] = "presentation: no founding movies",
        ["MovieSound"] = "presentation: no founding movies",
        ["iTGAIndex"] = "presentation: font-atlas glyph index; Ariel has no font atlas",
        ["PrereqBuildingClasses"] = "owner decision: a guild works like a corporation, so its branches need no group Guild Hall (the only buildings the source lists here); those halls are not built",
    };

    [SourceFact]
    public void Every_corporation_field_with_data_is_mapped_or_listed_with_a_reason()
    {
        var registry = new ImportRegistry(ExtractionLocation.Find()!);
        var records = registry.Records.Where(r => r.Class == "CorporationInfo").ToList();
        Assert.Equal(36, records.Count);
        var mapped = SchemaCatalog.Definitions.Where(d => d.SourceClass == "CorporationInfo")
            .SelectMany(d => d.Fields).Select(f => f.Source).ToHashSet(StringComparer.Ordinal);
        var withData = records.SelectMany(r => r.Node.Children.Where(HasData).Select(c => c.Name)).ToHashSet(StringComparer.Ordinal);
        foreach (var field in withData.Order(StringComparer.Ordinal))
            Assert.True(mapped.Contains(field) || Common.Contains(field) || Unread.ContainsKey(field),
                $"CorporationInfo.{field} carries data that is neither mapped to CorporationDef nor listed with a reason");
        // The fields this work brought in are mapped, so the rules that read them see the source's data.
        foreach (var field in new[] { "CompetingCorporations", "iHappiness", "iHealth", "iMilitaryProductionModifier", "ObsoleteTech",
                     "iSpreadFactor", "BonusProduced", "iSpread" })
            Assert.Contains(field, mapped);
    }

    /// <summary>A field has data when any leaf under it is neither empty, zero, false nor NONE.</summary>
    private static bool HasData(DataNode node) => node.Children.Length == 0
        ? node.Text is { } text && text.Trim() is not ("" or "0" or "NONE" or "false")
        : node.Children.Any(HasData);
}
