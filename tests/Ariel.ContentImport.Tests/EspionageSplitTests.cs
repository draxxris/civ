using System.Text.Json;
using System.Text.Json.Nodes;
using Ariel.Content;
using Ariel.ContentImport;
using Xunit;
using Schema = Ariel.Content.Schema;

namespace Ariel.ContentImport.Tests;

/// <summary>The Espionage mod is self-contained: base rows carry three commerce keys and name no espionage record, and the
/// mod's amounts and records are its own rows. These tests pin the importer to that structure with small synthetic source
/// records (no extraction is needed): a vector with an espionage slot splits into a base row and an amending row, a record
/// only the mod has lands under its namespace, and the rows match what is committed in <c>src/Mods/Espionage/data</c>.</summary>
public sealed class EspionageSplitTests
{
    [Fact]
    public void A_building_vector_with_espionage_splits_into_a_three_key_base_row_and_an_amending_row()
    {
        var rows = Map(Class("BUILDINGCLASS_SPLIT", "BUILDING_SPLIT"), Spy("SPECIALIST_SPY"),
            Record("BuildingInfo", "BUILDING_SPLIT", Leaf("BuildingClass", "BUILDINGCLASS_SPLIT"), Vector("CommerceChanges", 1, 2, 0, 3),
                Vector("CommerceModifiers", 0, 10, 0, 0), Pairs("SpecialistCounts", "SpecialistType", "iSpecialistCount", ("SPECIALIST_SPY", 2))));
        var building = rows.One<Schema.BuildingDef>("cities:split").Definition;
        // The base row has the base game's types only and no mention of the mod's specialist.
        Assert.Equal(["culture", "gold", "research"], Keys(building.CommerceChanges));
        Assert.Equal((1, 2), (building.CommerceChanges.Gold, building.CommerceChanges["research"]));
        Assert.Equal(["culture", "gold", "research"], Keys(building.CommerceModifiers));
        Assert.Empty(building.SpecialistSlots);
        // The mod's share is its own row, shaped like the committed ones: the same id, target and fields.
        var amendment = rows.One<Ariel.Mods.Espionage.BuildingEspionageDef>("espionage:split");
        Assert.Equal("espionage", amendment.Mod);
        Assert.Equal("cities:split", amendment.Definition.Target);
        Assert.Equal(["espionage"], Keys(amendment.Definition.CommerceChanges));
        Assert.Equal(3, amendment.Definition.CommerceChanges["espionage"]);
        Assert.True(amendment.Definition.CommerceModifiers.Layout is null, "an unchanged vector is left out of the amendment");
        Assert.Equal(2, amendment.Definition.SpecialistSlots["espionage:specialist_spy"]);
    }

    [Fact]
    public void A_building_with_no_espionage_gets_no_amending_row_and_the_split_is_exact()
    {
        var rows = Map(Class("BUILDINGCLASS_PLAIN", "BUILDING_PLAIN"), Spy("SPECIALIST_SPY"),
            Record("BuildingInfo", "BUILDING_PLAIN", Leaf("BuildingClass", "BUILDINGCLASS_PLAIN"), Vector("CommerceChanges", 4, 0, 1, 0)));
        Assert.Empty(rows.Of<Ariel.Mods.Espionage.BuildingEspionageDef>());
        Assert.Equal(4, rows.One<Schema.BuildingDef>("cities:plain").Definition.CommerceChanges.Gold);
    }

    [Fact]
    public void The_records_only_the_mod_has_land_under_its_namespace_and_carry_the_commerce_type_in_their_own_rows()
    {
        var rows = Map(
            Record("CommerceInfo", "COMMERCE_ESPIONAGE"), Spy("SPECIALIST_SPY", 0, 0, 0, 4),
            Record("SpecialistInfo", "SPECIALIST_GREAT_SPY", Vector("Commerces", 0, 0, 0, 12)),
            Record("ProcessInfo", "PROCESS_ESPIONAGE", Vector("ProductionToCommerceModifiers", 0, 0, 0, 50)),
            Record("ProcessInfo", "PROCESS_CULTURE", Vector("ProductionToCommerceModifiers", 0, 0, 100, 0)),
            Record("GameOptionInfo", "GAMEOPTION_NO_ESPIONAGE"), Record("GameOptionInfo", "GAMEOPTION_ENGLISH"),
            Record("UnitClassInfo", "UNITCLASS_SPY", Leaf("DefaultUnit", "UNIT_SPY")),
            Record("UnitInfo", "UNIT_SPY", Leaf("Class", "UNITCLASS_SPY"), Leaf("bSpy", "1"), Leaf("bCounterSpy", "1")),
            Record("UnitClassInfo", "UNITCLASS_PLAIN", Leaf("DefaultUnit", "UNIT_PLAIN")),
            Record("UnitInfo", "UNIT_PLAIN", Leaf("Class", "UNITCLASS_PLAIN")));
        Assert.Equal("espionage", rows.One<Schema.CommerceDef>("espionage:commerce_espionage").Mod);
        Assert.Equal("espionage", rows.One<Schema.ProcessDef>("espionage:process_espionage").Mod);
        Assert.Equal("economy", rows.One<Schema.ProcessDef>("economy:process_culture").Mod);
        Assert.Equal("espionage", rows.One<Schema.GameOptionDef>("espionage:gameoption_no_espionage").Mod);
        Assert.Equal("foundation", rows.One<Schema.GameOptionDef>("foundation:gameoption_english").Mod);
        Assert.Equal("espionage", rows.One<Schema.UnitDef>("espionage:spy").Mod);
        Assert.Equal("military", rows.One<Schema.UnitDef>("military:plain").Mod);
        // Its own units are flagged in its own row, which names the unit it shares an id with.
        var flags = rows.One<Ariel.Mods.Espionage.UnitEspionageDef>("espionage:spy");
        Assert.Equal(("espionage:spy", true, true), (flags.Definition.Target, flags.Definition.Spy, flags.Definition.CounterSpy));
        Assert.DoesNotContain(rows.Of<Ariel.Mods.Espionage.UnitEspionageDef>(), r => r.Definition.Id == "espionage:plain");
        // The spy specialist is the mod's and keeps its espionage in its own row, with all four types.
        var spy = rows.One<Schema.SpecialistDef>("espionage:specialist_spy");
        Assert.Equal("espionage", spy.Mod);
        Assert.Equal(["culture", "espionage", "gold", "research"], Keys(spy.Definition.Commerces));
        Assert.Equal(4, spy.Definition.Commerces["espionage"]);
        // The great spy stays in the base, so the mod's amendment of it is the only place its espionage appears.
        Assert.Equal(["culture", "gold", "research"], Keys(rows.One<Schema.SpecialistDef>("foundation:specialist_great_spy").Definition.Commerces));
        var great = rows.One<Ariel.Mods.Espionage.SpecialistEspionageDef>("espionage:specialist_great_spy");
        Assert.Equal(("foundation:specialist_great_spy", 12), (great.Definition.Target, great.Definition.Commerces["espionage"]));
        // The mod's own specialist is not amended: its amount would count twice.
        Assert.DoesNotContain(rows.Of<Ariel.Mods.Espionage.SpecialistEspionageDef>(), r => r.Definition.Id == "espionage:specialist_spy");
        // The mod's own process carries its commerce type; the base's carries the base game's.
        Assert.Equal(50, rows.One<Schema.ProcessDef>("espionage:process_espionage").Definition.ProductionToCommerceModifiers["espionage"]);
        Assert.Equal(["culture", "gold", "research"], Keys(rows.One<Schema.ProcessDef>("economy:process_culture").Definition.ProductionToCommerceModifiers));
    }

    [Fact]
    public void Traits_civics_leaders_and_civilizations_split_the_same_way()
    {
        var rows = Map(Spy("SPECIALIST_SPY"), Record("SpecialistInfo", "SPECIALIST_GREAT_SPY"),
            Record("UnitClassInfo", "UNITCLASS_NANITE_SPY", Leaf("DefaultUnit", "UNIT_NANITE_SPY")),
            Record("UnitInfo", "UNIT_NANITE_SPY", Leaf("Class", "UNITCLASS_NANITE_SPY"), Leaf("bSpy", "1")),
            Record("TraitInfo", "TRAIT_DECEIVER", Vector("CommerceChanges", 0, 0, 0, 4), Vector("CommerceModifiers", 0, 25, 0, 0)),
            Record("CivicOptionInfo", "CIVICOPTION_X"),
            Record("CivicInfo", "CIVIC_MARXIST", Leaf("CivicOptionType", "CIVICOPTION_X"), Vector("CommerceModifiers", 5, 0, 0, 35),
                Pairs("SpecialistYieldPercentChanges", "SpecialistType", "SpecialistYieldPercents", ("SPECIALIST_SPY", new[] { 100, 0, 0 }))),
            Record("LeaderHeadInfo", "LEADER_ABUBAKR",
                Pairs("MemoryAttitudePercents", "MemoryType", "iMemoryAttitudePercent", ("MEMORY_SPY_CAUGHT", -100), ("MEMORY_ACCEPTED_CIVIC", 5))),
            Record("CivilizationInfo", "CIVILIZATION_BARBARIAN",
                Pairs("Units", "UnitClassType", "UnitType", ("UNITCLASS_NANITE_SPY", "NONE"))));
        var trait = rows.One<Schema.TraitDef>("peoples:trait_deceiver").Definition;
        Assert.Equal(["culture", "gold", "research"], Keys(trait.CommerceChanges));
        Assert.Equal(25, trait.CommerceModifiers["research"]);
        Assert.Equal(4, rows.One<Ariel.Mods.Espionage.TraitEspionageDef>("espionage:trait_deceiver").Definition.CommerceChanges["espionage"]);

        // A civic's modifiers are effects: gold, research and culture in the base row, espionage in the mod's.
        var civic = rows.One<Schema.CivicDef>("government:marxist").Definition;
        Assert.Equal([("commerce.percent.gold", (string?)null, 5L)], civic.Effects.Select(e => (e.Key, e.Target, e.Amount)));
        var amended = rows.One<Ariel.Mods.Espionage.CivicEspionageDef>("espionage:marxist").Definition;
        Assert.Equal([("cities:specialist_food_hundredths", (string?)"espionage:specialist_spy", 100L), ("commerce.percent.espionage", null, 35L)],
            amended.Effects.Select(e => (e.Key, e.Target, e.Amount)));

        // A leader's attitude to the mod's memory kind is the mod's row; the base row keeps the rest.
        var leader = rows.One<Schema.LeaderDef>("peoples:leader_abubakr").Definition;
        Assert.Equal(["accepted_civic"], leader.MemoryAttitudePercents.Keys);
        Assert.Equal(-100, rows.One<Ariel.Mods.Espionage.LeaderEspionageDef>("espionage:leader_abubakr").Definition.MemoryAttitudePercents["spy_caught"]);

        // Barbarians cannot train the nanite spy: the base row cannot name it, so the mod's row says so.
        Assert.DoesNotContain("espionage:nanite_spy", rows.One<Schema.CivilizationDef>("peoples:barbarian").Definition.UnitOverrides.Keys);
        Assert.Null(rows.One<Ariel.Mods.Espionage.CivilizationEspionageDef>("espionage:barbarian").Definition.UnitOverrides["espionage:nanite_spy"]);
    }

    [Fact]
    public void Rows_about_an_optional_providers_content_stay_in_the_mod_under_that_providers_folder_and_pass_validation()
    {
        var rows = Map(Spy("SPECIALIST_SPY"), Record("EraInfo", "ERA_MODERN"), Record("CorporationInfo", "CORPORATION_MAPSTER"),
            Class("BUILDINGCLASS_CORPORATION_MAPSTER_HQ", "BUILDING_CORPORATION_MAPSTER_HQ"),
            Record("BuildingInfo", "BUILDING_CORPORATION_MAPSTER_HQ", Leaf("BuildingClass", "BUILDINGCLASS_CORPORATION_MAPSTER_HQ"),
                Leaf("FoundsCorporation", "CORPORATION_MAPSTER"), Leaf("SpecialBuildingType", "SPECIALBUILDING_CORPORATION_HQ"), Leaf("iCost", "-1"),
                Vector("ObsoleteSafeCommerceChanges", 0, 0, 0, 6)) with { Era = "ERA_MODERN" },
            Class("BUILDINGCLASS_ALHAMBRA", "BUILDING_ALHAMBRA"),
            Record("BuildingInfo", "BUILDING_ALHAMBRA", Leaf("BuildingClass", "BUILDINGCLASS_ALHAMBRA"), Vector("StateReligionCommerces", 1, 0, 0, 1)));
        // Corporations owns the headquarters and writes it with three keys; the mod's row about it is the mod's own, under
        // data/corporations, and names the headquarters although Corporations owns it.
        var headquarters = rows.One<Schema.BuildingDef>("corporations:corporation_mapster_hq");
        Assert.Equal(("corporations", ""), (headquarters.Mod, headquarters.Folder));
        Assert.Equal(["culture", "gold", "research"], Keys(headquarters.Definition.ObsoleteSafeCommerceChanges));
        var amendment = rows.One<Ariel.Mods.Espionage.BuildingEspionageDef>("espionage:corporation_mapster_hq");
        Assert.Equal(("espionage", "corporations", "corporations:corporation_mapster_hq", 6),
            (amendment.Mod, amendment.Folder, amendment.Definition.Target, amendment.Definition.ObsoleteSafeCommerceChanges["espionage"]));
        // The Religion mod's row about a building is the amendment's target, not the building.
        var religion = rows.One<Ariel.Mods.Espionage.BuildingReligionEspionageDef>("espionage:alhambra");
        Assert.Equal(("espionage", "religion", "religion:alhambra", 1),
            (religion.Mod, religion.Folder, religion.Definition.Target, religion.Definition.StateReligionCommerces["espionage"]));
        Assert.Equal(1, rows.One<Ariel.Mods.Religion.BuildingReligionDef>("religion:alhambra").Definition.StateReligionCommerces.Gold);
        Assert.Equal(["culture", "gold", "research"], Keys(rows.One<Ariel.Mods.Religion.BuildingReligionDef>("religion:alhambra").Definition.StateReligionCommerces));
        // A building of the base game has no such row in its own folder.
        Assert.DoesNotContain(rows.Of<Ariel.Mods.Espionage.BuildingEspionageDef>(), r => r.Definition.Id == "espionage:alhambra");
        // The reference check lets the mod name content of its optional providers and nobody else's.
        ContentEmission.Validate(rows.All, rows.Claimant);
        var foreign = new MappedDefinition(rows.One<Ariel.Mods.Espionage.BuildingEspionageDef>("espionage:corporation_mapster_hq").Mapping,
            rows.One<Ariel.Mods.Espionage.BuildingEspionageDef>("espionage:corporation_mapster_hq").Definition, "trade");
        Assert.Throws<InvalidDataException>(() => ContentEmission.Validate([.. rows.All.Where(d => d.Definition.Id != "espionage:corporation_mapster_hq" || d.Definition is not Ariel.Mods.Espionage.BuildingEspionageDef), foreign], rows.Claimant));
    }

    [Fact]
    public void An_amount_no_amending_row_can_carry_is_reported_and_not_dropped_silently()
    {
        var mapper = Mapper(Record("CivicOptionInfo", "CIVICOPTION_X"), Record("CommerceInfo", "COMMERCE_ESPIONAGE"),
            Record("CivicInfo", "CIVIC_CAPITAL", Leaf("CivicOptionType", "CIVICOPTION_X"), Vector("CapitalCommerceModifiers", 0, 0, 0, 5)),
            Record("CivicInfo", "CIVIC_FINE", Leaf("CivicOptionType", "CIVICOPTION_X"), Vector("CapitalCommerceModifiers", 5, 0, 0, 0)));
        mapper.Map();
        var issue = Assert.Single(mapper.Issues);
        Assert.Equal(("government:capital", "CapitalCommerceModifiers", "espionage"), (issue.Record, issue.Field, issue.SourceValue));
    }

    [Fact]
    public void The_mods_records_and_constants_are_the_ones_the_committed_data_has()
    {
        // The 25 constants removed from the base game's raw constants when the mod became self-contained.
        string[] constants =
        [
            "BASE_SPY_DESTROY_COST", "BASE_SPY_SABOTAGE_COST", "BASE_SPY_STEAL_PLANS_COST", "EMBASSY_ESPIONAGE_MISSION_COST_MODIFIER",
            "ESPIONAGE_CITY_HOLY_CITY_MOD", "ESPIONAGE_CITY_POP_EACH_MOD", "ESPIONAGE_CITY_RELIGION_STATE_MOD", "ESPIONAGE_CITY_TRADE_ROUTE_MOD",
            "ESPIONAGE_CULTURE_MULTIPLIER_MOD", "ESPIONAGE_DISTANCE_MULTIPLIER_MOD", "ESPIONAGE_EACH_TURN_UNIT_COST_DECREASE",
            "ESPIONAGE_INTERCEPT_COUNTERESPIONAGE_MISSION", "ESPIONAGE_INTERCEPT_COUNTERSPY", "ESPIONAGE_INTERCEPT_RECENT_MISSION",
            "ESPIONAGE_INTERCEPT_SPENDING_MAX", "ESPIONAGE_SPENDING_MULTIPLIER", "ESPIONAGE_SPY_INTERCEPT_MOD", "ESPIONAGE_SPY_MISSION_ESCAPE_MOD",
            "ESPIONAGE_SPY_NO_INTRUDE_INTERCEPT_MOD", "ESPIONAGE_SPY_REVEAL_IDENTITY_PERCENT", "FREE_TRADE_AGREEMENT_ESPIONAGE_MISSION_COST_MODIFIER",
            "NO_ESPIONAGE_CULTURE_LEVEL_MODIFIER", "SPY_DESTROY_COST_MULTIPLIER", "SPY_DESTROY_COST_MULTIPLIER_LIMITED", "SPY_STEAL_PLANS_COST_MULTIPLIER",
        ];
        Assert.Equal(25, constants.Length);
        Assert.All(constants, c => Assert.Equal("espionage", IdMapping.ConstantOwner(c)));
        var defines = constants.ToDictionary(c => c, c => new DefineValue("1", "iDefineIntVal", "fixture"));
        defines["MAX_HIT_POINTS"] = new("100", "iDefineIntVal", "fixture");
        defines["SOMETHING_ELSE"] = new("2", "iDefineIntVal", "fixture");
        var raw = Assert.Single(ContentEmission.Constants(defines).Select(d => d.Definition).OfType<Schema.RuleConstantsDef>()).Raw;
        Assert.Equal(["SOMETHING_ELSE"], raw.Keys);
        // No define the base game reads, and no constant it keeps, belongs to a mod; a mod's typed constant is its own.
        var committed = JsonNode.Parse(File.ReadAllText(Committed("src/Ariel.Rules/Foundation/data/rule-constants.json")))!.AsArray()[0]!["raw"]!.AsObject();
        Assert.All(committed.Select(p => p.Key), key => Assert.Null(IdMapping.ConstantOwner(key)));
        Assert.All(SchemaCatalog.Constants.Values.Keys, key => Assert.Null(IdMapping.ConstantOwner(key)));
        Assert.All(SchemaCatalog.Constants.Attachments.SelectMany(a => a.Constants.Keys.Select(key => (a.Mod, Key: key))),
            constant => Assert.True(IdMapping.ConstantOwner(constant.Key) is null || IdMapping.ConstantOwner(constant.Key) == constant.Mod, constant.Key));
    }

    [Fact]
    public void The_optional_providers_are_the_ones_the_Espionage_manifest_names_and_the_amending_types_resolve()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Committed("src/Mods/Espionage/mod.json")))!;
        Assert.Equal(manifest["optionalReferences"]!.AsArray().Select(n => n!.GetValue<string>()).Order(StringComparer.Ordinal),
            IdMapping.OptionalProviders["espionage"].Order(StringComparer.Ordinal));
        // Every espionage family the catalog names is a type the importer can build, and has the committed file name.
        foreach (var mapping in SchemaCatalog.Definitions.Where(m => m.Mod == "espionage"))
        {
            Assert.True(mapping.IsAttachment);
            var directory = Committed("src/Mods/Espionage/data" + (mapping.Folder.Length == 0 ? "" : "/" + mapping.Folder));
            Assert.True(File.Exists(Path.Combine(directory, ContentEmission.FileName(mapping.Definition))), mapping.Definition);
        }
        Assert.Equal("religion", SchemaCatalog.Definitions.Single(m => m.Definition == "BuildingReligionEspionageDef").Folder);
    }

    [Fact]
    public void Staging_writes_rows_about_an_optional_providers_content_beside_the_committed_layout_and_the_diff_pairs_them()
    {
        var rows = Map(Spy("SPECIALIST_SPY"), Record("EraInfo", "ERA_MODERN"), Record("CorporationInfo", "CORPORATION_MAPSTER"),
            Class("BUILDINGCLASS_CORPORATION_MAPSTER_HQ", "BUILDING_CORPORATION_MAPSTER_HQ"),
            Record("BuildingInfo", "BUILDING_CORPORATION_MAPSTER_HQ", Leaf("BuildingClass", "BUILDINGCLASS_CORPORATION_MAPSTER_HQ"),
                Leaf("FoundsCorporation", "CORPORATION_MAPSTER"), Leaf("SpecialBuildingType", "SPECIALBUILDING_CORPORATION_HQ"), Leaf("iCost", "-1"),
                Vector("ObsoleteSafeCommerceChanges", 0, 0, 0, 6)) with { Era = "ERA_MODERN" },
            Class("BUILDINGCLASS_ALHAMBRA", "BUILDING_ALHAMBRA"),
            Record("BuildingInfo", "BUILDING_ALHAMBRA", Leaf("BuildingClass", "BUILDINGCLASS_ALHAMBRA"), Vector("StateReligionCommerces", 1, 0, 0, 1)));
        using var staged = new TemporaryDirectory();
        var result = new EmissionResult(rows.All, [], [], new Dictionary<string, HashSet<string>>());
        ContentEmission.Write(result, new(Path.Combine(staged.Path, "rules"), Path.Combine(staged.Path, "mods")), Path.Combine(staged.Path, "reports"));
        var families = ContentEmission.Families(result);
        Assert.Contains(("espionage", "BuildingEspionageDef", "corporations"), families);
        Assert.Contains(("espionage", "BuildingReligionEspionageDef", "religion"), families);
        Assert.DoesNotContain(("espionage", "BuildingReligionEspionageDef", ""), families);
        foreach (var path in new[] { "mods/Espionage/data/corporations/building-espionage.json", "mods/Espionage/data/religion/building-religion-espionage.json",
                     "mods/Espionage/data/building-espionage.json", "mods/Espionage/data/commerce.json", "mods/Espionage/data/specialist.json" })
            Assert.True(File.Exists(Path.Combine(staged.Path, path)), path);
        Assert.False(File.Exists(Path.Combine(staged.Path, "mods/Espionage/data/building-religion-espionage.json")));
        Assert.Contains("\"espionage:corporation_mapster_hq\"", File.ReadAllText(Path.Combine(staged.Path, "mods/Espionage/data/corporations/building-espionage.json")));

        // The committed copy of those rows has a layout the staged one is compared against, row by row.
        using var committed = new TemporaryDirectory();
        committed.Write("mods/Espionage/data/corporations/building-espionage.json",
            File.ReadAllText(Path.Combine(staged.Path, "mods/Espionage/data/corporations/building-espionage.json")));
        // A row that sat in the flat file before (a layout the importer no longer writes) is paired and reported as moved.
        committed.Write("mods/Espionage/data/building-religion-espionage.json",
            File.ReadAllText(Path.Combine(staged.Path, "mods/Espionage/data/religion/building-religion-espionage.json")));
        committed.Write("mods/Espionage/data/building-espionage.json", "[]");
        var output = new StringWriter();
        var total = ImportDiff.Compare(output, new(Path.Combine(staged.Path, "rules"), Path.Combine(staged.Path, "mods")),
            new(Path.Combine(committed.Path, "rules"), Path.Combine(committed.Path, "mods")), ["Espionage"], values: false)!;
        Assert.Equal(0, total.Changed);
        Assert.Equal(1, total.Moved);
        Assert.Equal(0, total.Gone);
        Assert.Contains("building-religion-espionage.json", output.ToString());
        Assert.Contains("espionage:alhambra: committed in Espionage/data/building-religion-espionage.json", output.ToString());
    }

    [Fact]
    public void The_importers_rows_match_the_committed_amending_rows_for_the_same_input()
    {
        var rows = Map(Spy("SPECIALIST_SPY"), Record("SpecialistInfo", "SPECIALIST_GREAT_SPY"), Record("SpecialistInfo", "SPECIALIST_LAWYER"),
            Class("BUILDINGCLASS_AI_SURVEILLANCE", "BUILDING_AI_SURVEILLANCE"),
            Record("BuildingInfo", "BUILDING_AI_SURVEILLANCE", Leaf("BuildingClass", "BUILDINGCLASS_AI_SURVEILLANCE"), Vector("CommerceChanges", 0, 0, 0, 6),
                Pairs("SpecialistCounts", "SpecialistType", "iSpecialistCount", ("SPECIALIST_SPY", 2))),
            Record("TraitInfo", "TRAIT_DECEIVER", Vector("CommerceChanges", 0, 0, 0, 4)),
            Record("CivicOptionInfo", "CIVICOPTION_X"),
            Record("CivicInfo", "CIVIC_CASTE_SYSTEM", Leaf("CivicOptionType", "CIVICOPTION_X"),
                Pairs("SpecialistYieldPercentChanges", "SpecialistType", "SpecialistYieldPercents", ("SPECIALIST_SPY", new[] { 100, 0, 0 }))),
            Record("CivicInfo", "CIVIC_COSMOPOLITAN", Leaf("CivicOptionType", "CIVICOPTION_X"), Vector("CommerceModifiers", 0, 0, 0, -20)),
            Record("SpecialistInfo", "SPECIALIST_GREAT_DIPLOMAT", Vector("Commerces", 0, 0, 0, 6)),
            Record("LeaderHeadInfo", "LEADER_ALARIC", Pairs("MemoryAttitudePercents", "MemoryType", "iMemoryAttitudePercent", ("MEMORY_SPY_CAUGHT", -100))),
            Record("UnitClassInfo", "UNITCLASS_NANITE_SPY", Leaf("DefaultUnit", "UNIT_NANITE_SPY")),
            Record("UnitInfo", "UNIT_NANITE_SPY", Leaf("Class", "UNITCLASS_NANITE_SPY"), Leaf("bSpy", "1")),
            Record("CivilizationInfo", "CIVILIZATION_BARBARIAN", Pairs("Units", "UnitClassType", "UnitType", ("UNITCLASS_NANITE_SPY", "NONE"))));
        var checkedRows = 0;
        foreach (var (file, id) in new[]
                 {
                     ("building-espionage.json", "espionage:ai_surveillance"), ("trait-espionage.json", "espionage:trait_deceiver"),
                     ("civic-espionage.json", "espionage:caste_system"), ("civic-espionage.json", "espionage:cosmopolitan"),
                     ("specialist-espionage.json", "espionage:specialist_great_diplomat"), ("leader-espionage.json", "espionage:leader_alaric"),
                     ("civilization-espionage.json", "espionage:barbarian"),
                 })
        {
            var emitted = rows.All.Single(d => d.Definition.Id == id && d.Mod == "espionage" && ContentEmission.FileName(d.Mapping.Definition) == file);
            var written = JsonSerializer.SerializeToNode(emitted.Definition, emitted.Definition.GetType(), ContentMapper.Json)!;
            var committed = JsonNode.Parse(File.ReadAllText(Committed("src/Mods/Espionage/data/" + file)))!.AsArray()
                .Single(n => n!["id"]!.GetValue<string>() == id)!;
            AssertSameModuloDefaults(committed, written, id);
            checkedRows++;
        }
        Assert.Equal(7, checkedRows);
    }

    /// <summary>The committed row leaves out the fields it does not use; the emitted one writes every property. Equal
    /// where either has a value, and the one that is missing a field has it empty.</summary>
    private static void AssertSameModuloDefaults(JsonNode committed, JsonNode emitted, string where)
    {
        switch (committed, emitted)
        {
            case (JsonObject a, JsonObject b):
                foreach (var key in a.Select(p => p.Key).Union(b.Select(p => p.Key)).Order(StringComparer.Ordinal))
                    AssertSameModuloDefaults(a[key]!, b[key]!, where + "." + key);
                break;
            case (JsonArray a, JsonArray b):
                Assert.True(a.Count == b.Count, where + ": lengths differ");
                for (int i = 0; i < a.Count; i++) AssertSameModuloDefaults(a[i]!, b[i]!, where + "[" + i + "]");
                break;
            default:
                Assert.True((committed is null || IsEmpty(committed)) && (emitted is null || IsEmpty(emitted)) || JsonNode.DeepEquals(committed, emitted),
                    $"{where}: committed {committed?.ToJsonString() ?? "(absent)"} but emitted {emitted?.ToJsonString() ?? "(absent)"}");
                break;
        }
    }
    private static bool IsEmpty(JsonNode node) => node switch
    {
        JsonObject o => o.Count == 0, JsonArray a => a.Count == 0,
        JsonValue v => v.TryGetValue<string>(out var s) ? s.Length == 0 : v.TryGetValue<bool>(out var b) ? !b : v.TryGetValue<long>(out var n) && n == 0,
        _ => false,
    };

    private static string Committed(string relative)
    {
        var path = Path.Combine(ExtractionLocation.Repository(), relative);
        return File.Exists(path) || Directory.Exists(path) ? path : throw new FileNotFoundException(path);
    }

    private static string[] Keys(CommerceVector vector)
    {
        var keys = new List<string>();
        foreach (var (key, _) in vector) keys.Add(key);
        return [.. keys.Order(StringComparer.Ordinal)];
    }

    // Source fixtures.
    private static EffectiveRecord Record(string family, string type, params DataNode[] fields) =>
        new(family, type, DataNode.Branch(family, new[] { DataNode.Leaf("Type", type) }.Concat(fields)), []);
    private static DataNode Leaf(string name, string value) => DataNode.Leaf(name, value);
    /// <summary>A native commerce array: gold, research, culture, espionage.</summary>
    private static DataNode Vector(string name, params int[] amounts) =>
        DataNode.Branch(name, amounts.Select(a => DataNode.Leaf("iCommerce", a.ToString(System.Globalization.CultureInfo.InvariantCulture))));
    private static EffectiveRecord Class(string classType, string building) =>
        Record("BuildingClassInfo", classType, Leaf("DefaultBuilding", building));
    private static EffectiveRecord Spy(string type, params int[] commerces) =>
        Record("SpecialistInfo", type, commerces.Length == 0 ? [] : [Vector("Commerces", commerces)]);
    /// <summary>A list of (reference, value) pairs; a value is a number, a reference, or an array for a yield or commerce vector.</summary>
    private static DataNode Pairs(string list, string key, string value, params (string Key, object Value)[] entries) =>
        DataNode.Branch(list, entries.Select(e => DataNode.Branch("Entry", [DataNode.Leaf(key, e.Key), e.Value switch
        {
            int[] vector => DataNode.Branch(value, vector.Select(v => DataNode.Leaf("iYield", v.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
            int number => DataNode.Leaf(value, number.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            _ => DataNode.Leaf(value, (string)e.Value),
        }])));

    private static ContentMapper Mapper(params EffectiveRecord[] records) => new(records, _ => null);

    private static Mapped Map(params EffectiveRecord[] records)
    {
        var mapper = Mapper(records);
        return new(mapper.Map(), mapper.Claimant);
    }

    private sealed record Mapped(IReadOnlyList<MappedDefinition> All, Func<string, string?> Claimant)
    {
        public IEnumerable<MappedDefinition> Of<T>() where T : ContentDef => All.Where(d => d.Definition is T);
        /// <summary>The one row of this type with this id, as a typed view of the mapped definition.</summary>
        public Row<T> One<T>(string id) where T : ContentDef
        {
            var mapped = Assert.Single(All, d => d.Definition is T && d.Definition.Id == id);
            return new(mapped.Mod, mapped.Folder, (T)mapped.Definition, mapped.Mapping);
        }
    }

    private sealed record Row<T>(string Mod, string Folder, T Definition, DefinitionMapping Mapping);
}
