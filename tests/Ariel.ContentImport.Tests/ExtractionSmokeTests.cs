using System.Text.Json;
using Ariel.Content;
using Ariel.Content.Schema;
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
        return FindRepository() is not { } root ? null : Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal)
            .FirstOrDefault(x => File.Exists(System.IO.Path.Combine(x, "data/module-controls.yaml")));
    }

    public static string Repository() =>
        FindRepository() ?? throw new DirectoryNotFoundException("Ariel.slnx not found above " + AppContext.BaseDirectory);

    private static string? FindRepository()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "Ariel.slnx"))) root = root.Parent;
        return root?.FullName;
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
        var firstRoots = Roots(first.Path);
        var secondRoots = Roots(second.Path);
        foreach (var roots in new[] { firstRoots, secondRoots })
        {
            var data = System.IO.Path.Combine(roots.Module("foundation"), "data");
            Directory.CreateDirectory(data);
            File.WriteAllText(System.IO.Path.Combine(data, "hex-overrides.json"), handAuthored);
        }
        ContentEmission.Write(firstEmission, firstRoots, first.Path);
        ContentEmission.Write(secondEmission, secondRoots, second.Path);
        Assert.Equal(handAuthored, File.ReadAllText(System.IO.Path.Combine(first.Path, "rules", "Foundation", "data", "hex-overrides.json")));
        Assert.Equal(handAuthored, File.ReadAllText(System.IO.Path.Combine(second.Path, "rules", "Foundation", "data", "hex-overrides.json")));
        var payloads = new[] { firstRoots.Rules, firstRoots.Mods }.Where(Directory.Exists)
            .SelectMany(root => Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)).ToArray();
        Assert.NotEmpty(payloads);
        foreach (var payload in payloads)
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
        Assert.DoesNotContain(result.Definitions, d => d.Definition is GameContentDef { Era: { } era } && era != "foundation:era_ancient");
        using var directory = new TemporaryDirectory();
        ContentEmission.Write(result, Roots(directory.Path), directory.Path);
        // One file per family, one per mod's rule constants row, Foundation's conditions and rule constants, and the
        // buildings and units Corporations owns outright, written even when the filter leaves none.
        Assert.Equal(SchemaCatalog.Definitions.Count + SchemaCatalog.Constants.Attachments.Length + 2 + 2, ContentEmission.Families(result).Count);
        Assert.Equal(ContentEmission.Families(result).Count, Directory.GetFiles(directory.Path, "*.json", SearchOption.AllDirectories).Count(p => p.Contains("/data/", StringComparison.Ordinal)));
        Assert.Equal("[]", File.ReadAllText(System.IO.Path.Combine(directory.Path, "rules/Foundation/data/handicap.json")).Trim());
        Assert.Equal("[]", File.ReadAllText(System.IO.Path.Combine(directory.Path, "mods/Corporations/data/building.json")).Trim());
    }

    [SourceFact]
    public void Victory_source_records_keep_deferred_identities_and_corporations_are_the_Corporations_mods_own_records()
    {
        var result = ContentEmission.Compile(Registry.Value);
        var corporations = result.Definitions.Where(d => d.Definition is Ariel.Mods.Corporations.CorporationDef).ToArray();
        var victories = result.Definitions.Select(d => d.Definition).OfType<Ariel.Content.Schema.VictoryIdentityDef>().ToArray();
        Assert.NotEmpty(corporations);
        Assert.NotEmpty(victories);
        // A corporation has no base identity: its one record is the mod's rules, written to the mod's folder.
        Assert.Equal(Registry.Value.Ids.Where(i => i.Class == "CorporationInfo").Select(i => i.Id).Order(StringComparer.Ordinal),
            corporations.Select(c => c.Definition.Id).Order(StringComparer.Ordinal));
        Assert.All(corporations, c => Assert.Equal("corporations", c.Mod));
        Assert.Equal(Registry.Value.Ids.Where(i => i.Class == "VictoryInfo").Select(i => i.Id).Order(StringComparer.Ordinal),
            victories.Select(v => v.Id).Order(StringComparer.Ordinal));
        Assert.All(victories, v => Assert.True(v.Deferred));
    }

    [SourceFact]
    public void Every_corporation_store_and_its_special_building_type_belong_to_Corporations_and_base_buildings_name_none()
    {
        var result = ContentEmission.Compile(Registry.Value);
        var stores = result.Definitions.Where(d => d.Definition is Ariel.Content.Schema.BuildingDef b
            && ContentReferences.Leaves(b.Requirements ?? new Requirement.All([])).Any(l => l.Kind == "corporation")).ToArray();
        Assert.Equal(20, stores.Length);
        Assert.All(stores, s => Assert.Equal(("corporations", "corporations:"), (s.Mod, s.Definition.Id[..13])));
        Assert.Equal(["corporations:specialbuilding_corporate_store", "corporations:specialbuilding_corporation_hq", "corporations:specialbuilding_guild_hq"],
            result.Definitions.Where(d => d.Definition is Ariel.Content.Schema.BuildingCategoryDef && d.Mod == "corporations").Select(d => d.Definition.Id).Order(StringComparer.Ordinal));
        // The base theatre replaces the Red Curtains Theater from the store's own row, which may name it.
        var theater = (Ariel.Content.Schema.BuildingDef)stores.Single(s => s.Definition.Id == "corporations:corporation_red_theater").Definition;
        Assert.Contains("cities:theatre", theater.ReplacedBy);
        Assert.DoesNotContain(result.Definitions.Where(d => d.Mod != "corporations").Select(d => d.Definition).OfType<Ariel.Content.Schema.BuildingDef>(),
            b => b.ReplaceBuildings.Keys.Any(k => k.StartsWith("corporations:", StringComparison.Ordinal)) || b.Founds is not null);
    }

    [SourceFact]
    public void Every_source_corporation_and_guild_gets_its_rules_and_a_buildable_founding_headquarters()
    {
        var result = ContentEmission.Compile(Registry.Value);
        var definitions = result.Definitions.Select(d => d.Definition).ToArray();
        var corporations = definitions.OfType<Ariel.Mods.Corporations.CorporationDef>().ToArray();
        // 36 source records: 21 corporations and 15 guilds.
        Assert.Equal(36, corporations.Length);
        Assert.Equal(15, corporations.Count(c => c.Seat == "Master Guild"));
        Assert.Equal("Sid's Sushi Co", corporations.Single(c => c.Id == "corporations:2").Name);
        var cereal = corporations.Single(c => c.Id == "corporations:1");
        Assert.Equal(75, cereal.YieldProducedHundredths.Food);
        Assert.Equal(50, cereal.SpreadCost);
        Assert.Equal(4, cereal.HeadquartersCommerce.Gold);

        // Every founding building costs 70% of the source's price or of the median national wonder of its era (the source left the
        // headquarters to a great person, and each such price is a data issue).
        var buildings = definitions.OfType<Ariel.Content.Schema.BuildingDef>().Where(b => b.Founds?.Kind == "corporation").ToArray();
        Assert.Equal(36, buildings.Length);
        Assert.All(buildings, b => Assert.True(b.Cost > 0, b.Id));
        Assert.Equal(210, buildings.Single(b => b.Id == "corporations:corporation_masonguild").Cost);
        Assert.Equal(490, buildings.Single(b => b.Id == "corporations:corporation_1").Cost);
        Assert.Equal(910, buildings.Single(b => b.Id == "corporations:corporation_7").Cost);
        Assert.Equal(21, result.Issues.Count(i => i.Field == "iCost"));
    }

    private static ContentRoots Roots(string directory) =>
        new(System.IO.Path.Combine(directory, "rules"), System.IO.Path.Combine(directory, "mods"));

    private static EffectiveRecord Record(ImportRegistry registry, string family, string type) =>
        registry.Records.Single(x => x.Class == family && x.Type == type);
}
