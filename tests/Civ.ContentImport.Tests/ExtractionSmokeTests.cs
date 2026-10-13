using System.Text.Json;
using Civ.ContentImport;
using Xunit;

namespace Civ.ContentImport.Tests;

public sealed class SourceFactAttribute : FactAttribute
{
    public SourceFactAttribute()
    {
        if (ExtractionLocation.Find() is null) Skip = "The local source extraction is absent.";
    }
}

internal static class ExtractionLocation
{
    public static string? Find()
    {
        var supplied = Environment.GetEnvironmentVariable("CIV_CONTENT_SOURCE");
        if (!string.IsNullOrWhiteSpace(supplied)) return Directory.Exists(supplied) ? supplied : null;
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "Civ.slnx"))) root = root.Parent;
        return root is null ? null : Directory.EnumerateDirectories(root.FullName).Order(StringComparer.Ordinal)
            .FirstOrDefault(x => File.Exists(System.IO.Path.Combine(x, "data/module-controls.yaml")));
    }
}

public sealed class ExtractionSmokeTests
{
    private static readonly Lazy<ImportRegistry> Registry = new(() => new ImportRegistry(ExtractionLocation.Find()!));

    [SourceFact]
    public void Default_profile_has_expected_counts_and_preserves_selected_merge_and_era_results()
    {
        var registry = Registry.Value;
        Assert.Equal(8033, registry.Records.Count);
        var counts = registry.Records.GroupBy(x => x.Class).ToDictionary(x => x.Key, x => x.Count());
        Assert.Equal(252, counts["TechInfo"]);
        Assert.Equal(645, counts["BuildingInfo"]);
        Assert.Equal(359, counts["UnitInfo"]);
        Assert.Equal(266, counts["PromotionInfo"]);
        Assert.Equal(81, counts["CivicInfo"]);
        Assert.Equal(135, counts["LeaderHeadInfo"]);
        Assert.Equal(17, counts["TraitInfo"]);
        Assert.Equal(60, counts["CivilizationInfo"]);
        Assert.Equal(64, registry.Collisions.Count);
        Assert.Equal(109, registry.Merges.Count);
        Assert.Equal(2, registry.Merges.Count(x => x.Rule == "force-overwrite"));
        Assert.Equal(81, registry.Recovery.Records);
        Assert.Equal(2, registry.Recovery.RepairedComments);
        Assert.True(registry.Recovery.TreeMatches);
        Assert.Equal("90", Record(registry, "TechInfo", "TECH_BRONZE_WORKING").Node.Value("iCost"));
        Assert.Equal("ERA_ANCIENT", Record(registry, "BuildingInfo", "BUILDING_GRANARY").Era);
        Assert.Null(Record(registry, "UnitInfo", "UNIT_WARRIOR").Era);
        var germany = Record(registry, "CivilizationInfo", "CIVILIZATION_GERMANY");
        Assert.Equal(new[] { "LEADER_BARBAROSSA", "LEADER_FREDERICK", "LEADER_BISMARCK" },
            germany.Node.Child("Leaders")!.Children.Select(x => x.Value("LeaderName")));
        Assert.Equal("65", Record(registry, "EraInfo", "ERA_ANCIENT").Node.Value("iResearchPercent"));
        Assert.Equal(768, registry.Defines.Count);
        Assert.Equal("319", registry.Defines["CIV4_VERSION"].Value);
        Assert.NotNull(registry.Text.Lookup("TXT_KEY_TECH_BRONZE_WORKING"));
    }

    [SourceFact]
    public void Independent_imports_write_identical_reports_and_brand_free_generic_data()
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        var source = ExtractionLocation.Find()!;
        var registry = Registry.Value;
        Reports.Write(registry, source, first.Path, System.IO.Path.Combine(first.Path, "dump"));
        Reports.Write(new ImportRegistry(source), source, second.Path, null);
        var reports = Directory.GetFiles(first.Path).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(reports);
        foreach (var report in reports)
            Assert.Equal(File.ReadAllBytes(report), File.ReadAllBytes(System.IO.Path.Combine(second.Path, System.IO.Path.GetFileName(report))));
        foreach (var file in Directory.GetFiles(first.Path, "*", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotMatch(@"(?<![A-Za-z0-9])\u0041ND(?= )|\u0041 New Dawn|\u0052ise of Mankind|\u0052oM", text);
        }
        using var census = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(first.Path, "census.json")));
        var leader = census.RootElement.EnumerateArray().Single(x => x.GetProperty("class").GetString() == "LeaderHeadInfo");
        Assert.Contains(leader.GetProperty("fields").EnumerateArray(), x => x.GetProperty("field").GetString() == "iAttackOddsChangeRand");
        using var counts = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(first.Path, "counts.json")));
        var buildings = counts.RootElement.GetProperty("counts").EnumerateArray().Single(x => x.GetProperty("class").GetString() == "BuildingInfo");
        Assert.Equal(625, buildings.GetProperty("mainTable").GetInt32());
        Assert.Equal(20, buildings.GetProperty("delta").GetInt32());
        Assert.Equal(registry.Records.Count, registry.Ids.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count());
    }

    private static EffectiveRecord Record(ImportRegistry registry, string family, string type) =>
        registry.Records.Single(x => x.Class == family && x.Type == type);
}
