using Ariel.Content;
using Ariel.ContentImport;
using Xunit;
using Schema = Ariel.Content.Schema;

namespace Ariel.ContentImport.Tests;

public sealed class TypedMappingTests
{
    [Fact]
    public void Typed_mapping_preserves_groups_explicit_zero_negative_slots_and_padded_vectors()
    {
        var mapper = Mapper(
            Record("EraInfo", "ERA_ANCIENT"), Record("TechInfo", "TECH_FIRST"), Record("TechInfo", "TECH_SECOND"),
            Record("TechInfo", "TECH_EXAMPLE", DataNode.Leaf("iCost", "0"),
                DataNode.Branch("AndPreReqs", [DataNode.Leaf("PrereqTech", "TECH_FIRST")]),
                DataNode.Branch("OrPreReqs", [DataNode.Leaf("PrereqTech", "NONE"), DataNode.Leaf("PrereqTech", "TECH_SECOND")])),
            Record("BuildingClassInfo", "BUILDINGCLASS_EXAMPLE", DataNode.Leaf("DefaultBuilding", "BUILDING_EXAMPLE"), DataNode.Leaf("iMaxGlobalInstances", "1")),
            Record("SpecialistInfo", "SPECIALIST_EXAMPLE"),
            Record("BuildingInfo", "BUILDING_EXAMPLE", DataNode.Leaf("BuildingClass", "BUILDINGCLASS_EXAMPLE"), DataNode.Leaf("iCost", "-1"),
                DataNode.Branch("CommerceChanges", [DataNode.Leaf("iCommerce", "0"), DataNode.Leaf("iCommerce", "3")]),
                Pair("SpecialistCounts", "SpecialistType", "SPECIALIST_EXAMPLE", "iSpecialistCount", "-1")));
        var mapped = mapper.Map().Select(d => d.Definition).ToArray();
        var tech = mapped.OfType<Schema.TechDef>().Single(d => d.Id == "research:example");
        Assert.Equal(0, tech.Cost);
        var all = Assert.IsType<Requirement.All>(tech.Requirements);
        Assert.Equal(new Requirement.Leaf("tech", "research:first"), Assert.Single(Assert.IsType<Requirement.All>(all.Items[0]).Items));
        Assert.Equal(new Requirement.Leaf("tech", "research:second"), Assert.Single(Assert.IsType<Requirement.Any>(all.Items[1]).Items));
        var building = Assert.Single(mapped.OfType<Schema.BuildingDef>());
        Assert.Equal(-1, building.Cost);
        Assert.Equal(1, building.Limits.World);
        Assert.Equal(-1, building.Limits.Player);
        Assert.Equal(-1, building.SpecialistSlots["foundation:specialist_example"]);
        Assert.Equal(new CommerceVector(0, 3, 0, 0), building.CommerceChanges);
    }

    [Fact]
    public void Civilization_starting_units_resolve_unique_overrides_and_explicit_none_disables_a_class()
    {
        var mapper = Mapper(
            Record("UnitClassInfo", "UNITCLASS_WORKER", DataNode.Leaf("DefaultUnit", "UNIT_WORKER")),
            Record("UnitInfo", "UNIT_WORKER", DataNode.Leaf("Class", "UNITCLASS_WORKER")),
            Record("UnitInfo", "UNIT_UNIQUE", DataNode.Leaf("Class", "UNITCLASS_WORKER")),
            Record("CivilizationInfo", "CIVILIZATION_EXAMPLE",
                Pair("Units", "UnitClassType", "UNITCLASS_WORKER", "UnitType", "UNIT_UNIQUE"),
                Pair("FreeUnitClasses", "UnitClassType", "UNITCLASS_WORKER", "iFreeUnits", "2")),
            Record("CivilizationInfo", "CIVILIZATION_DISABLED",
                Pair("Units", "UnitClassType", "UNITCLASS_WORKER", "UnitType", "NONE"),
                Pair("FreeUnitClasses", "UnitClassType", "UNITCLASS_WORKER", "iFreeUnits", "2")));
        var civs = mapper.Map().Select(d => d.Definition).OfType<Schema.CivilizationDef>().ToDictionary(d => d.Id);
        Assert.Equal("military:unique", civs["peoples:example"].UnitOverrides["military:worker"]);
        Assert.Equal(2, civs["peoples:example"].StartingUnits["military:unique"]);
        Assert.Null(civs["peoples:disabled"].UnitOverrides["military:worker"]);
        Assert.Empty(civs["peoples:disabled"].StartingUnits);
    }

    [Fact]
    public void Positional_pair_aliases_last_assignment_and_text_links_survive_typed_emission()
    {
        var mapper = Mapper(
            Record("BuildingClassInfo", "BUILDINGCLASS_EXAMPLE", DataNode.Leaf("DefaultBuilding", "BUILDING_EXAMPLE")),
            Record("BonusInfo", "BONUS_STONE"), Record("TechInfo", "TECH_EXAMPLE"),
            Record("BuildingInfo", "BUILDING_EXAMPLE", DataNode.Leaf("BuildingClass", "BUILDINGCLASS_EXAMPLE"),
                DataNode.Leaf("Description", "TXT_KEY_EXAMPLE"), DataNode.Leaf("Strategy", "{link:tech_example}Tech{/link}"),
                DataNode.Branch("BonusProductionModifiers", [
                    DataNode.Branch("BonusProductionModifier", [DataNode.Leaf("BonusType", "BONUS_STONE"), DataNode.Leaf("iProductonModifier", "25")]),
                    DataNode.Branch("BonusProductionModifier", [DataNode.Leaf("BonusType", "BONUS_STONE"), DataNode.Leaf("iProductionModifier", "50")])
                ]), DataNode.Leaf("FutureMechanic", "7")));
        var building = Assert.Single(mapper.Map().Select(d => d.Definition).OfType<Schema.BuildingDef>());
        Assert.Equal(50, building.BonusProductionModifiers["world:stone"]);
        Assert.Equal("Example", building.Name);
        Assert.Equal("{link:research:example}Tech{/link}", building.Strategy);
        Assert.Equal(new[] { "FutureMechanic" }, building.Unsupported);
    }

    [Fact]
    public void Building_location_preserves_coast_or_river_instead_of_requiring_both()
    {
        var mapper = Mapper(Record("BuildingClassInfo", "BUILDINGCLASS_EXAMPLE", DataNode.Leaf("DefaultBuilding", "BUILDING_EXAMPLE")),
            Record("BuildingInfo", "BUILDING_EXAMPLE", DataNode.Leaf("BuildingClass", "BUILDINGCLASS_EXAMPLE"),
                DataNode.Leaf("bWater", "1"), DataNode.Leaf("bRiver", "1"), DataNode.Leaf("iMinAreaSize", "10")));
        var building = Assert.Single(mapper.Map().Select(d => d.Definition).OfType<Schema.BuildingDef>());
        var location = Assert.IsType<Requirement.Any>(Assert.Single(Assert.IsType<Requirement.All>(building.Requirements).Items));
        Assert.Equal(new Requirement.Leaf("condition", "foundation:coastal", 10), location.Items[0]);
        Assert.Equal(new Requirement.Leaf("condition", "foundation:river"), location.Items[1]);
    }

    [Fact]
    public void Mandatory_unknown_references_fail_instead_of_granting_unrestricted_content()
    {
        var mapper = Mapper(Record("TechInfo", "TECH_EXAMPLE", DataNode.Branch("AndPreReqs", [DataNode.Leaf("PrereqTech", "TECH_MISSING")])));
        var error = Assert.Throws<InvalidDataException>(() => mapper.Map());
        Assert.Contains("TECH_MISSING", error.Message);
        Assert.Contains("AndPreReqs", error.Message);
    }

    [Theory]
    [InlineData("1", true, null)]
    [InlineData("KNOWN_POLICY", true, "KNOWN_POLICY")]
    [InlineData("MISSING_POLICY", false, "MISSING_POLICY")]
    public void Unit_outcomes_keep_chances_yields_and_native_boolean_define_resolution(string policy, bool delivered, string? key)
    {
        var records = new[]
        {
            Record("UnitClassInfo", "UNITCLASS_HUNTER", DataNode.Leaf("DefaultUnit", "UNIT_HUNTER")),
            Record("OutcomeInfo", "OUTCOME_SUBDUE"),
            Record("UnitInfo", "UNIT_HUNTER", DataNode.Leaf("Class", "UNITCLASS_HUNTER"),
                DataNode.Branch("KillOutcomes", [DataNode.Branch("Outcome", [
                    DataNode.Leaf("OutcomeType", "OUTCOME_SUBDUE"), DataNode.Leaf("iChance", "10"),
                    DataNode.Leaf("UnitType", "UNIT_HUNTER"), DataNode.Leaf("bUnitToCity", policy),
                    DataNode.Branch("Yields", [DataNode.Leaf("iYield", "2"), DataNode.Leaf("iYield", "4")])
                ])]))
        };
        var mapper = new ContentMapper(records, _ => null,
            new Dictionary<string, DefineValue> { ["KNOWN_POLICY"] = new("1", "iDefineIntVal", "fixture") });
        var definitions = mapper.Map().Select(d => d.Definition).ToArray();
        var outcome = Assert.Single(Assert.Single(definitions.OfType<Schema.UnitDef>()).KillOutcomes);
        Assert.Equal(10, outcome.Chance);
        Assert.Equal(new YieldVector(2, 4, 0), outcome.Yields);
        Assert.Equal("military:hunter", outcome.Unit);
        Assert.Equal(delivered, outcome.DeliverUnitToCity);
        Assert.Equal(key, outcome.DeliveryPolicyKey);
        Assert.True(Assert.Single(definitions.OfType<Schema.OutcomeDef>()).FriendlyTerritory);
        Assert.Equal(policy == "MISSING_POLICY", mapper.Issues.Count > 0);
    }

    private static ContentMapper Mapper(params EffectiveRecord[] records) => new(records, key => key == "TXT_KEY_EXAMPLE" ? "Example" : null);
    private static EffectiveRecord Record(string family, string type, params DataNode[] fields) =>
        new(family, type, DataNode.Branch(family, new[] { DataNode.Leaf("Type", type) }.Concat(fields)), []);
    private static DataNode Pair(string list, string key, string identity, string value, string amount) =>
        DataNode.Branch(list, [DataNode.Branch("Entry", [DataNode.Leaf(key, identity), DataNode.Leaf(value, amount)])]);
}
