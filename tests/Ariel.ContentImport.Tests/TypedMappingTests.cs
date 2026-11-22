using Ariel.Content;
using Ariel.ContentImport;
using Xunit;
using Schema = Ariel.Content.Schema;

namespace Ariel.ContentImport.Tests;

public sealed class TypedMappingTests
{
    [Fact]
    public void Base_modules_emit_under_the_rules_root_and_other_mods_under_the_mods_root()
    {
        var roots = new ContentRoots("/out/rules", "/out/mods");
        Assert.Equal("/out/rules/Foundation", roots.Module("foundation"));
        Assert.Equal("/out/rules/World", roots.Module("world"));
        Assert.Equal("/out/mods/Trade", roots.Module("trade"));
    }

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
    public void A_unit_that_needs_a_corporation_headquarters_needs_to_be_trained_in_that_corporations_headquarters()
    {
        // The imported headquarters cannot be built (Ariel founds with the Corporations mod's own), so an executive
        // that named it could never be trained; it names the corporation's headquarters city instead.
        var mapper = Mapper(Record("CorporationInfo", "CORPORATION_1"),
            Record("BuildingClassInfo", "BUILDINGCLASS_CORPORATION_1", DataNode.Leaf("DefaultBuilding", "BUILDING_CORPORATION_1")),
            Record("BuildingInfo", "BUILDING_CORPORATION_1", DataNode.Leaf("BuildingClass", "BUILDINGCLASS_CORPORATION_1"),
                DataNode.Leaf("FoundsCorporation", "CORPORATION_1"), DataNode.Leaf("iCost", "200")),
            Record("BuildingClassInfo", "BUILDINGCLASS_PLAIN", DataNode.Leaf("DefaultBuilding", "BUILDING_PLAIN")),
            Record("BuildingInfo", "BUILDING_PLAIN", DataNode.Leaf("BuildingClass", "BUILDINGCLASS_PLAIN")),
            Record("UnitClassInfo", "UNITCLASS_EXECUTIVE_1", DataNode.Leaf("DefaultUnit", "UNIT_EXECUTIVE_1")),
            Record("UnitInfo", "UNIT_EXECUTIVE_1", DataNode.Leaf("Class", "UNITCLASS_EXECUTIVE_1"),
                DataNode.Leaf("PrereqBuilding", "BUILDING_CORPORATION_1"), DataNode.Leaf("PrereqCorporation", "CORPORATION_1")),
            Record("UnitClassInfo", "UNITCLASS_PLAIN", DataNode.Leaf("DefaultUnit", "UNIT_PLAIN")),
            Record("UnitInfo", "UNIT_PLAIN", DataNode.Leaf("Class", "UNITCLASS_PLAIN"), DataNode.Leaf("PrereqBuilding", "BUILDING_PLAIN")));
        var units = mapper.Map().Select(d => d.Definition).OfType<Schema.UnitDef>().ToDictionary(u => u.Id);
        var executive = ContentReferences.Leaves(units["military:executive_1"].Requirements!).ToArray();
        Assert.Contains(new Requirement.Leaf("corporation", "foundation:1", null, 2), executive);
        Assert.Contains(new Requirement.Leaf("corporation", "foundation:1"), executive);
        Assert.DoesNotContain(executive, l => l.Kind == "building");
        // A building that founds nothing stays a building requirement.
        Assert.Equal([new Requirement.Leaf("building", "cities:plain")], ContentReferences.Leaves(units["military:plain"].Requirements!));
    }

    [Fact]
    public void Mandatory_unknown_references_fail_instead_of_granting_unrestricted_content()
    {
        var mapper = Mapper(Record("TechInfo", "TECH_EXAMPLE", DataNode.Branch("AndPreReqs", [DataNode.Leaf("PrereqTech", "TECH_MISSING")])));
        var error = Assert.Throws<InvalidDataException>(() => mapper.Map());
        Assert.Contains("TECH_MISSING", error.Message);
        Assert.Contains("AndPreReqs", error.Message);
    }

    [Fact]
    public void A_mod_constant_goes_to_the_mods_row_about_the_rule_constants_and_not_to_the_raw_defines()
    {
        var definitions = ContentEmission.Constants(new Dictionary<string, DefineValue>
        {
            ["TRADE_PROFIT_PERCENT"] = new("20", "iDefineIntVal", "fixture"),
            ["MAX_HIT_POINTS"] = new("100", "iDefineIntVal", "fixture"),
            ["SOMETHING_ELSE"] = new("1.5", "fDefineFloatVal", "fixture"),
        });
        var constants = Assert.Single(definitions.Select(d => d.Definition).OfType<Schema.RuleConstantsDef>());
        Assert.Equal(100, constants.Values.MaxHitPoints);
        Assert.Equal(["SOMETHING_ELSE"], constants.Raw.Keys);
        var trade = Assert.Single(definitions, d => d.Definition is Ariel.Mods.Trade.RuleConstantsTradeDef);
        Assert.Equal("trade", trade.Mapping.Mod);
        var row = (Ariel.Mods.Trade.RuleConstantsTradeDef)trade.Definition;
        Assert.Equal(("trade:rule_constants", "foundation:rule_constants", 20), (row.Id, row.Target, row.TradeProfitPercent));
    }

    [Fact]
    public void A_mod_constant_at_its_default_emits_no_row()
    {
        var definitions = ContentEmission.Constants(new Dictionary<string, DefineValue> { ["TRADE_PROFIT_PERCENT"] = new("0", "iDefineIntVal", "fixture") });
        Assert.IsType<Schema.RuleConstantsDef>(Assert.Single(definitions).Definition);
    }

    [Fact]
    public void A_building_field_a_mod_maps_becomes_that_mods_row_and_leaves_the_building()
    {
        var mapper = Mapper(Record("BuildingClassInfo", "BUILDINGCLASS_EXAMPLE", DataNode.Leaf("DefaultBuilding", "BUILDING_EXAMPLE")),
            Record("BuildingClassInfo", "BUILDINGCLASS_PLAIN", DataNode.Leaf("DefaultBuilding", "BUILDING_PLAIN")),
            Record("BuildingInfo", "BUILDING_EXAMPLE", DataNode.Leaf("BuildingClass", "BUILDINGCLASS_EXAMPLE"), DataNode.Leaf("iGlobalTradeRoutes", "2")),
            Record("BuildingInfo", "BUILDING_PLAIN", DataNode.Leaf("BuildingClass", "BUILDINGCLASS_PLAIN"), DataNode.Leaf("iGlobalTradeRoutes", "0")));
        var mapped = mapper.Map().Select(d => d.Definition).ToArray();
        var row = Assert.Single(mapped.OfType<Ariel.Mods.Trade.BuildingTradeDef>());
        Assert.Equal(("trade:example", "cities:example", 2), (row.Id, row.Target, row.GlobalTradeRoutes));
        Assert.All(mapped.OfType<Schema.BuildingDef>(), b => Assert.Empty(b.Unsupported));
    }

    private static ContentMapper Mapper(params EffectiveRecord[] records) => new(records, key => key == "TXT_KEY_EXAMPLE" ? "Example" : null);
    private static EffectiveRecord Record(string family, string type, params DataNode[] fields) =>
        new(family, type, DataNode.Branch(family, new[] { DataNode.Leaf("Type", type) }.Concat(fields)), []);
    private static DataNode Pair(string list, string key, string identity, string value, string amount) =>
        DataNode.Branch(list, [DataNode.Branch("Entry", [DataNode.Leaf(key, identity), DataNode.Leaf(value, amount)])]);
}
