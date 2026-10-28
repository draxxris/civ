using System.Text.Json;
using Ariel.ContentImport;
using Xunit;

namespace Ariel.ContentImport.Tests;

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
        var supplied = Environment.GetEnvironmentVariable("ARIEL_CONTENT_SOURCE");
        if (!string.IsNullOrWhiteSpace(supplied)) return Directory.Exists(supplied) ? supplied : null;
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "Ariel.slnx"))) root = root.Parent;
        return root is null ? null : Directory.EnumerateDirectories(root.FullName).Order(StringComparer.Ordinal)
            .FirstOrDefault(x => File.Exists(System.IO.Path.Combine(x, "data/module-controls.yaml")));
    }
}

[Trait("Category", "Extractor")]
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
        var firstEmission = ContentEmission.Compile(registry);
        var secondRegistry = new ImportRegistry(source);
        var secondEmission = ContentEmission.Compile(secondRegistry);
        Reports.Write(registry, source, first.Path, System.IO.Path.Combine(first.Path, "dump"), firstEmission.MappedPaths);
        Reports.Write(secondRegistry, source, second.Path, null, secondEmission.MappedPaths);
        const string handAuthored = "[{\"id\":\"foundation:hex_overrides\",\"waterPercent\":71}]\n";
        foreach (var root in new[] { first.Path, second.Path })
        {
            var data = System.IO.Path.Combine(root, "mods", "Foundation", "data");
            Directory.CreateDirectory(data);
            File.WriteAllText(System.IO.Path.Combine(data, "hex-overrides.json"), handAuthored);
        }
        ContentEmission.Write(firstEmission, System.IO.Path.Combine(first.Path, "mods"), first.Path);
        ContentEmission.Write(secondEmission, System.IO.Path.Combine(second.Path, "mods"), second.Path);
        Assert.Equal(handAuthored, File.ReadAllText(System.IO.Path.Combine(first.Path, "mods", "Foundation", "data", "hex-overrides.json")));
        Assert.Equal(handAuthored, File.ReadAllText(System.IO.Path.Combine(second.Path, "mods", "Foundation", "data", "hex-overrides.json")));
        foreach (var payload in Directory.GetFiles(System.IO.Path.Combine(first.Path, "mods"), "*.json", SearchOption.AllDirectories))
            Assert.Equal(File.ReadAllBytes(payload), File.ReadAllBytes(System.IO.Path.Combine(second.Path, System.IO.Path.GetRelativePath(first.Path, payload))));
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

    [SourceFact]
    public void Era_filter_drops_later_content_and_transitive_dependents_and_overwrites_empty_families()
    {
        var result = ContentEmission.Compile(Registry.Value, "foundation:era_ancient");
        Assert.Contains(result.Dropped, d => d.Id == "research:absolutism" && d.Reason.Contains("exceeds", StringComparison.Ordinal));
        Assert.Contains(result.Dropped, d => d.Reason.StartsWith("References dropped content", StringComparison.Ordinal));
        var ids = result.Definitions.Select(d => d.Definition.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(result.Definitions.SelectMany(d => ContentEmission.References(d.Definition)), r => Assert.Contains(r.Id, ids));
        Assert.DoesNotContain(result.Definitions, d => d.Definition.Era is not null && d.Definition.Era != "foundation:era_ancient");
        using var directory = new TemporaryDirectory();
        ContentEmission.Write(result, directory.Path, directory.Path);
        Assert.Equal(SchemaCatalog.Definitions.Count + 2, Directory.GetFiles(directory.Path, "*.json", SearchOption.AllDirectories).Count(p => p.Contains("/data/", StringComparison.Ordinal)));
        Assert.Equal("[]", File.ReadAllText(System.IO.Path.Combine(directory.Path, "Foundation/data/handicap.json")).Trim());
    }

    [SourceFact]
    public void Corporation_and_victory_source_records_keep_deferred_identities_without_invented_gameplay_rules()
    {
        var result = ContentEmission.Compile(Registry.Value);
        var corporations = result.Definitions.Select(d => d.Definition).OfType<Ariel.Content.Schema.CorporationIdentityDef>().ToArray();
        var victories = result.Definitions.Select(d => d.Definition).OfType<Ariel.Content.Schema.VictoryIdentityDef>().ToArray();
        Assert.NotEmpty(corporations);
        Assert.NotEmpty(victories);
        Assert.Equal(Registry.Value.Ids.Where(i => i.Class == "CorporationInfo").Select(i => i.Id).Order(StringComparer.Ordinal),
            corporations.Select(c => c.Id).Order(StringComparer.Ordinal));
        Assert.Equal(Registry.Value.Ids.Where(i => i.Class == "VictoryInfo").Select(i => i.Id).Order(StringComparer.Ordinal),
            victories.Select(v => v.Id).Order(StringComparer.Ordinal));
        Assert.All(corporations, c => Assert.True(c.Deferred));
        Assert.All(victories, v => Assert.True(v.Deferred));
    }

    private static EffectiveRecord Record(ImportRegistry registry, string family, string type) =>
        registry.Records.Single(x => x.Class == family && x.Type == type);
}
