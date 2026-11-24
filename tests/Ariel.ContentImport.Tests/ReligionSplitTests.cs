using Ariel.Content;
using Ariel.ContentImport;
using Xunit;
using Schema = Ariel.Content.Schema;

namespace Ariel.ContentImport.Tests;

/// <summary>The Religion mod owns every building, unit and promotion of a faith, so no base row names one. These tests
/// pin the importer to that structure with small synthetic source records (no extraction is needed): the religious
/// records land under <c>religion:*</c>, and what base rows said about them (a replacement, a granted promotion, a
/// prophet's shrines, a civilization's overrides, a requirement alternative, a text link) is the mod's own row, or is
/// left out and reported.</summary>
public sealed class ReligionSplitTests
{
    [Fact]
    public void A_temple_is_the_mods_own_and_the_base_building_that_replaces_it_is_named_on_the_temple()
    {
        var rows = Map(Faith("RELIGION_ISLAM"),
            Class("BUILDINGCLASS_ISLAMIC_TEMPLE", "BUILDING_ISLAMIC_TEMPLE"), Class("BUILDINGCLASS_FIREPIT", "BUILDING_FIREPIT"),
            Record("BuildingInfo", "BUILDING_ISLAMIC_TEMPLE", Leaf("BuildingClass", "BUILDINGCLASS_ISLAMIC_TEMPLE"), Leaf("ReligionType", "RELIGION_ISLAM")),
            Record("BuildingInfo", "BUILDING_FIREPIT", Leaf("BuildingClass", "BUILDINGCLASS_FIREPIT"),
                Pairs("ReplaceBuildings", "BuildingClassType", "bReplace", ("BUILDINGCLASS_ISLAMIC_TEMPLE", "1"))));
        var temple = rows.One<Schema.BuildingDef>("religion:islamic_temple");
        Assert.Equal("religion", temple.Mod);
        Assert.Equal(["cities:firepit"], temple.Definition.ReplacedBy.ToArray());
        Assert.Empty(rows.One<Schema.BuildingDef>("cities:firepit").Definition.ReplaceBuildings);
        Assert.DoesNotContain(rows.Of<Schema.BuildingDef>(), d => d.Definition.Id == "cities:islamic_temple");
    }

    [Fact]
    public void What_base_buildings_units_and_civilizations_say_about_religious_records_is_the_mods_amending_row()
    {
        var rows = Map(Faith("RELIGION_TAOISM"),
            Record("PromotionInfo", "PROMOTION_MARTIAL_ARTS", Leaf("PrereqReligion", "RELIGION_TAOISM")),
            Class("BUILDINGCLASS_SHAOLIN", "BUILDING_SHAOLIN"), Class("BUILDINGCLASS_TAOIST_SHRINE", "BUILDING_TAOIST_SHRINE"),
            Record("BuildingInfo", "BUILDING_SHAOLIN", Leaf("BuildingClass", "BUILDINGCLASS_SHAOLIN"), Leaf("FreePromotion", "PROMOTION_MARTIAL_ARTS")),
            Record("BuildingInfo", "BUILDING_TAOIST_SHRINE", Leaf("BuildingClass", "BUILDINGCLASS_TAOIST_SHRINE"), Leaf("ReligionType", "RELIGION_TAOISM")),
            Record("UnitClassInfo", "UNITCLASS_PROPHET", Leaf("DefaultUnit", "UNIT_PROPHET")),
            Record("UnitInfo", "UNIT_PROPHET", Leaf("Class", "UNITCLASS_PROPHET"),
                Pairs("Buildings", "BuildingType", "bBuilding", ("BUILDING_TAOIST_SHRINE", "1")),
                Pairs("FreePromotions", "PromotionType", "bFreePromotion", ("PROMOTION_MARTIAL_ARTS", "1"))),
            Record("CivilizationInfo", "CIVILIZATION_BARBARIAN",
                Pairs("Buildings", "BuildingClassType", "BuildingType", ("BUILDINGCLASS_TAOIST_SHRINE", "NONE"))));
        Assert.Equal("religion", rows.One<Schema.PromotionDef>("religion:martial_arts").Mod);
        // The base rows name nothing of the mod's.
        Assert.Null(rows.One<Schema.BuildingDef>("cities:shaolin").Definition.FreePromotion);
        var prophet = rows.One<Schema.UnitDef>("military:prophet").Definition;
        Assert.Empty(prophet.ConstructibleBuildings);
        Assert.Empty(prophet.FreePromotions);
        Assert.Empty(rows.One<Schema.CivilizationDef>("peoples:barbarian").Definition.BuildingOverrides);
        // The mod's rows add it back, in their own row types.
        var shaolin = rows.One<Ariel.Mods.Religion.BuildingGrantsReligionDef>("religion:shaolin");
        Assert.Equal(("religion", "cities:shaolin", "religion:martial_arts"), (shaolin.Mod, shaolin.Definition.Target, shaolin.Definition.FreePromotion));
        var grants = rows.One<Ariel.Mods.Religion.UnitGrantsReligionDef>("religion:prophet").Definition;
        Assert.Equal("military:prophet", grants.Target);
        Assert.True(grants.ConstructibleBuildings["religion:taoist_shrine"]);
        Assert.True(grants.FreePromotions["religion:martial_arts"]);
        var barbarian = rows.One<Ariel.Mods.Religion.CivilizationReligionDef>("religion:barbarian").Definition;
        Assert.Equal("peoples:barbarian", barbarian.Target);
        Assert.Null(barbarian.BuildingOverrides["religion:taoist_shrine"]);
        // A religious record's own row says it itself: the mod writes no amendment about its own content.
        Assert.DoesNotContain(rows.Of<Ariel.Mods.Religion.BuildingGrantsReligionDef>(), d => d.Definition.Id == "religion:taoist_shrine");
        ContentEmission.Validate(rows.All, rows.Claimant);
    }

    [Fact]
    public void A_base_requirement_alternative_naming_a_religious_building_is_the_mods_amending_row_and_a_text_link_keeps_its_words()
    {
        var mapper = Mapper(Faith("RELIGION_ISLAM"),
            Class("BUILDINGCLASS_PALACE", "BUILDING_PALACE"), Class("BUILDINGCLASS_ALHAMBRA", "BUILDING_ALHAMBRA"),
            Class("BUILDINGCLASS_THRONE_ROOM", "BUILDING_THRONE_ROOM"),
            Record("BuildingInfo", "BUILDING_PALACE", Leaf("BuildingClass", "BUILDINGCLASS_PALACE"),
                Leaf("Civilopedia", "Rivalled by the {link:BUILDING_ALHAMBRA}Alhambra{/link}.")),
            Record("BuildingInfo", "BUILDING_ALHAMBRA", Leaf("BuildingClass", "BUILDINGCLASS_ALHAMBRA"), Leaf("ReligionType", "RELIGION_ISLAM")),
            Record("BuildingInfo", "BUILDING_THRONE_ROOM", Leaf("BuildingClass", "BUILDINGCLASS_THRONE_ROOM"),
                Pairs("PrereqOrBuildingClasses", "BuildingClassType", "iNumBuildingNeeded", ("BUILDINGCLASS_PALACE", "1"), ("BUILDINGCLASS_ALHAMBRA", "1"))));
        var rows = new Mapped(mapper.Map(), mapper.Claimant);
        var throne = rows.One<Schema.BuildingDef>("cities:throne_room").Definition;
        Assert.Equal(["cities:palace"], ContentReferences.Leaves(throne.Requirements!).Select(l => l.Id));
        // Religion's row adds the Alhambra back beside the palace, so nothing is lost and nothing is reported.
        var grants = rows.One<Ariel.Mods.Religion.BuildingGrantsReligionDef>("religion:throne_room");
        Assert.Equal(("religion", "cities:throne_room"), (grants.Mod, grants.Definition.Target));
        var added = Assert.Single(grants.Definition.RequirementAlternatives["cities:palace"]);
        Assert.Equal(new Requirement.Leaf("building-class", "religion:alhambra", 1, 0), added);
        Assert.DoesNotContain(mapper.Issues, i => i.Record == "cities:throne_room");
        Assert.Equal("Rivalled by the Alhambra.", rows.One<Schema.BuildingDef>("cities:palace").Definition.Pedia);
        ContentEmission.Validate(rows.All, rows.Claimant);
    }

    // Source fixtures.
    private static EffectiveRecord Record(string family, string type, params DataNode[] fields) =>
        new(family, type, DataNode.Branch(family, new[] { DataNode.Leaf("Type", type) }.Concat(fields)), []);
    private static DataNode Leaf(string name, string value) => DataNode.Leaf(name, value);
    private static EffectiveRecord Class(string classType, string building) =>
        Record("BuildingClassInfo", classType, Leaf("DefaultBuilding", building));
    private static EffectiveRecord Faith(string type) => Record("ReligionInfo", type);
    /// <summary>A list of (reference, value) pairs.</summary>
    private static DataNode Pairs(string list, string key, string value, params (string Key, string Value)[] entries) =>
        DataNode.Branch(list, entries.Select(e => DataNode.Branch("Entry", [DataNode.Leaf(key, e.Key), DataNode.Leaf(value, e.Value)])));

    private static ContentMapper Mapper(params EffectiveRecord[] records) => new(records, _ => null);
    private static Mapped Map(params EffectiveRecord[] records)
    {
        var mapper = Mapper(records);
        return new(mapper.Map(), mapper.Claimant);
    }

    private sealed record Mapped(IReadOnlyList<MappedDefinition> All, Func<string, string?> Claimant)
    {
        public IEnumerable<MappedDefinition> Of<T>() where T : ContentDef => All.Where(d => d.Definition is T);
        public Row<T> One<T>(string id) where T : ContentDef
        {
            var mapped = Assert.Single(All, d => d.Definition is T && d.Definition.Id == id);
            return new(mapped.Mod, mapped.Folder, (T)mapped.Definition, mapped.Mapping);
        }
    }

    private sealed record Row<T>(string Mod, string Folder, T Definition, DefinitionMapping Mapping);
}
