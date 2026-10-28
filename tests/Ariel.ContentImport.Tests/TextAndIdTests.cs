using System.Text.Json;
using Ariel.ContentImport;
using Xunit;

namespace Ariel.ContentImport.Tests;

public sealed class TextAndIdTests
{
    [Theory]
    [InlineData("[ICON_GOLD][COLOR_POSITIVE_TEXT]Gold[COLOR_REVERT][NEWLINE][BULLET]One[PARAGRAPH]Two",
        "{icon:gold}{color:positive_text}Gold{/color}\n● One\n\nTwo")]
    [InlineData("[LINK=TECH_BRONZE_WORKING]Bronze[\\LINK]: %s1_city, %d2_percent%%[SPACE]•",
        "{link:tech_bronze_working}Bronze{/link}: {arg:1:text:city}, {arg:2:number:percent}% ●")]
    [InlineData("[LINK=literal]A[/LINK][TAB][UNKNOWN]", "{link:literal}A{/link}\t[UNKNOWN]")]
    public void Civ_markup_becomes_neutral_markup_without_losing_arguments_or_unknown_tokens(string input, string expected) =>
        Assert.Equal(expected, EnglishText.ConvertMarkup(input));

    [Fact]
    public void English_lookup_uses_visible_grammar_text_and_module_precedence()
    {
        using var directory = new TemporaryDirectory();
        directory.Write("localization/english/core.yaml", """
            format: and-english-localization-v1
            source: {"layer":"and","path":"XML/Text/core.xml"}
            records:
              - {"id":"TXT_KEY_NAME","english":[["English",[["Text","core"],["Gender","masculine"],["Plural","0"]]]],"sourceOrdinal":0}
              - {"id":"TXT_KEY_HELP","english":[["English","[ICON_GOLD]Help"]],"sourceOrdinal":1}
            """);
        var module = new TreeDocument(DataNode.Branch("Civ4GameText", [DataNode.Branch("TEXT",
            [DataNode.Leaf("Tag", "TXT_KEY_NAME"), DataNode.Leaf("English", "module")])]), "and", "Modules/Text.xml", "hash", "and-xml-tree-v1");
        var text = new EnglishText(directory.Path, [], [module]);
        Assert.Equal("module", text.Lookup("TXT_KEY_NAME"));
        Assert.Equal("{icon:gold}Help", text.Lookup("TXT_KEY_HELP"));
        Assert.Null(text.Lookup("TXT_KEY_MISSING"));
        Assert.Equal(1, text.DuplicateKeys);
        var withoutModule = new EnglishText(directory.Path, [], []);
        Assert.Equal("core", withoutModule.Lookup("TXT_KEY_NAME"));
        Assert.Equal("masculine", withoutModule.Entries["TXT_KEY_NAME"].English.Value("Gender"));
    }

    [Theory]
    [InlineData("TechInfo", "TECH_BRONZE_WORKING", "research:bronze_working")]
    [InlineData("BuildingInfo", "BUILDING_GRANARY", "cities:granary")]
    [InlineData("UnitInfo", "UNIT_WARRIOR", "military:warrior")]
    [InlineData("CivilizationInfo", "CIVILIZATION_ROME", "peoples:rome")]
    [InlineData("LeaderHeadInfo", "LEADER_ROME", "peoples:leader_rome")]
    [InlineData("TraitInfo", "TRAIT_ROME", "peoples:trait_rome")]
    [InlineData("EventTriggerInfo", "EVENTTRIGGER_TEST", "events:eventtrigger_test")]
    public void Game_ids_have_mod_ownership_and_stable_secondary_prefixes(string family, string type, string expected) =>
        Assert.Equal(expected, IdMapping.Id(family, type));

    [Fact]
    public void Cross_class_name_conflicts_are_disambiguated_for_both_records()
    {
        var records = new[] { Record("TerrainInfo", "TERRAIN_TEST"), Record("FeatureInfo", "FEATURE_TEST") };
        var mapped = IdMapping.Create(records);
        Assert.Equal(new[] { "world:feature_feature_test", "world:terrain_terrain_test" }, mapped.Select(x => x.Id));
    }

    private static EffectiveRecord Record(string family, string type) => new(family, type,
        DataNode.Branch(family, [DataNode.Leaf("Type", type)]), []);

    [Fact]
    public void Branding_reports_named_rules_and_scrubs_all_requested_source_names()
    {
        const string input = "\u0041ND welcome. \u0041 New Dawn and \u0052ise of Mankind / \u0052oM.";
        var hits = Branding.Scan("TXT_KEY_HELP", input).ToArray();
        Assert.Equal(4, hits.Length);
        Assert.Equal("Source welcome. the source mod and the source mod / Source.", Branding.Scrub(input));
        Assert.All(hits, x => Assert.DoesNotContain("\u0052oM", x.NeutralExcerpt));
    }

    [Theory]
    [InlineData("iAttackOddsChangeRand")]
    [InlineData("iContactRand")]
    [InlineData("CIVILIZATION_ANDORRA")]
    [InlineData("A command")]
    [InlineData("both sides and")]
    public void Branding_does_not_change_ordinary_names_or_conjunctions(string input)
    {
        Assert.Equal(input, Branding.Identifier(input));
        Assert.Empty(Branding.Scan("ordinary", input));
    }

    [Fact]
    public void Output_serialization_scrubs_values_and_dictionary_keys_without_corrupting_field_names()
    {
        using var directory = new TemporaryDirectory();
        Reports.WriteJson(directory.Path, "neutral", new Dictionary<string, string>
        {
            ["TXT_KEY_\u0041ND_HELP"] = "Welcome to \u0041 New Dawn.",
            ["iAttackOddsChangeRand"] = "2"
        });
        using var document = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(directory.Path, "neutral.json")));
        Assert.Equal("Welcome to the source mod.", document.RootElement.GetProperty("TXT_KEY_Source_HELP").GetString());
        Assert.Equal("2", document.RootElement.GetProperty("iAttackOddsChangeRand").GetString());
    }
}
