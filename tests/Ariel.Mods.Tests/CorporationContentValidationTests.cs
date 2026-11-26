using System.Collections.Immutable;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Corporations;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The corporation content rules the schema cannot express. The shipped data passes them on every load, so
/// these tests have a mod define one corporation that breaks each rule and check the load fails naming that
/// corporation and that rule. A corporation any mod defines is checked, not only this mod's own.</summary>
public sealed class CorporationContentValidationTests
{
    // A resource and a competitor the shipped content defines, so only the rule under test is wrong.
    private const string Resource = "world:wheat", Competitor = "corporations:1", Building = "cities:apothecary";

    /// <summary>A corporation row that breaks no rule (it consumes a resource and spreads); each argument replaces one field.</summary>
    private static CorporationDef Row(string id = "probe:corp", int spreadCost = 100, int maintenance = 50, int natural = 10,
        string[]? resources = null, string[]? competing = null, (string Building, int Count)[]? founding = null) => new()
    {
        Id = id, Name = id, SpreadCost = spreadCost, MaintenanceHundredths = maintenance, NaturalSpreadPercent = natural,
        PrereqBonuses = [.. resources ?? [Resource]],
        CompetingCorporations = [.. competing ?? []],
        FoundingBuildingClasses = (founding ?? []).ToImmutableDictionary(f => f.Building, f => f.Count),
    };

    private sealed class CorporationMod(params CorporationDef[] rows) : IMod
    {
        public void Register(IModContext context)
        {
            foreach (var row in rows) context.Content<CorporationDef>().Register(row);
        }
    }

    private static string Failure(params CorporationDef[] rows) =>
        Assert.Throws<InvalidDataException>(() => P6RulesScenario.With(new CorporationMod(rows), ["cities", "military", "foundation", "world", "corporations"])).Message;

    public static TheoryData<string, CorporationDef, string> BrokenRows() => new()
    {
        { "negative spread cost", Row(spreadCost: -1), "probe:corp: negative SpreadCost would pay the spreader" },
        { "negative maintenance", Row(maintenance: -1), "probe:corp: negative MaintenanceHundredths would subsidise the branch" },
        { "competes with itself", Row(competing: [Competitor, "probe:corp"]), "probe:corp: competes with itself" },
        { "never spreads naturally", Row(natural: 0), "probe:corp: NaturalSpreadPercent 0 would never spread" },
        { "no resource and no founding classes", Row(resources: []), "probe:corp: consumes no resource and has no FoundingBuildingClasses" },
        {
            "resource and founding classes", Row(founding: [(Building, 1)]),
            "probe:corp: consumes a resource, which gates its founding; FoundingBuildingClasses is only for a corporation that consumes none"
        },
        {
            "founding class count below one", Row(resources: [], founding: [(Building, 0)]),
            $"probe:corp: FoundingBuildingClasses needs 0 of {Building}; a count must be at least 1"
        },
    };

    [Theory]
    [MemberData(nameof(BrokenRows))]
    public void A_corporation_row_that_breaks_a_rule_fails_the_load_naming_the_corporation_and_the_rule(string rule, CorporationDef broken, string expected)
    {
        Assert.False(string.IsNullOrEmpty(rule));
        Assert.Contains(expected, Failure(broken));
    }

    [Fact]
    public void Every_broken_corporation_is_named_and_a_valid_one_is_not()
    {
        string message = Failure(Row("probe:b", spreadCost: -1), Row("probe:ok"), Row("probe:a", natural: 0));
        Assert.Contains("probe:a: NaturalSpreadPercent 0", message);
        Assert.Contains("probe:b: negative SpreadCost", message);
        Assert.DoesNotContain("probe:ok: negative", message);
        Assert.DoesNotContain("probe:ok: NaturalSpread", message);
    }

    [Fact]
    public void A_corporation_no_building_founds_fails_the_load()
    {
        Assert.Contains("probe:corp: 0 buildings found it", Failure(Row()));
    }
}
