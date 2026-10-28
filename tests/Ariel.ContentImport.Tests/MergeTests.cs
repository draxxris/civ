using System.Text.Json;
using Ariel.ContentImport;
using Xunit;

namespace Ariel.ContentImport.Tests;

public sealed class MergeTests
{
    private static DataNode Node(string json)
    {
        using var document = JsonDocument.Parse(json);
        return TreeReader.ParseNode(document.RootElement);
    }
    private static RecordMerger Merger() => new(new Dictionary<string, DataNode>(), new Dictionary<string, DataNode>());

    [Fact]
    public void Era_inherits_default_scalars_but_keeps_nondefault_changes_and_appends_old_tracks()
    {
        var old = Node("""
            ["EraInfo",[["Type","ERA_ANCIENT"],["iResearchPercent","65"],["iStartingGold","20"],["bNoAnimals","1"],
              ["Description","TXT_KEY_ANCIENT"],["EraInfoSoundtracks",[["EraInfoSoundtrack","OLD"],["EraInfoSoundtrack","COMMON"]]]]]
            """);
        var next = Node("""
            ["EraInfo",[["Type","ERA_ANCIENT"],["iResearchPercent","0"],["iStartingGold","30"],["bNoAnimals","false"],
              ["Description",""],["EraInfoSoundtracks",[["EraInfoSoundtrack","NEW"],["EraInfoSoundtrack","COMMON"]]]]]
            """);
        var merged = Merger().Merge(old, next);
        Assert.Equal("65", merged.Node.Value("iResearchPercent"));
        Assert.Equal("30", merged.Node.Value("iStartingGold"));
        Assert.Equal("1", merged.Node.Value("bNoAnimals"));
        Assert.Equal("TXT_KEY_ANCIENT", merged.Node.Value("Description"));
        Assert.Equal(new[] { "NEW", "COMMON", "OLD", "COMMON" }, merged.Node.Child("EraInfoSoundtracks")!.Children.Select(x => x.Text));
        Assert.Equal(new[] { "EraInfoSoundtracks", "iStartingGold" }, merged.ChangedFields);
    }

    [Theory]
    [InlineData("CivilizationInfo", true)]
    [InlineData("UnitArtInfo", false)]
    public void Force_overwrite_and_art_replacement_do_not_inherit_any_old_fields(string family, bool force)
    {
        var old = DataNode.Branch(family, [DataNode.Leaf("Type", "TEST"), DataNode.Leaf("Description", "old"), DataNode.Leaf("iValue", "8")]);
        var next = DataNode.Branch(family, force
            ? [DataNode.Leaf("Type", "TEST"), DataNode.Leaf("bForceOverwrite", "1"), DataNode.Leaf("iValue", "0")]
            : [DataNode.Leaf("Type", "TEST"), DataNode.Leaf("iValue", "0")]);
        var result = Merger().Merge(old, next);
        Assert.Null(result.Node.Child("Description"));
        Assert.Equal("0", result.Node.Value("iValue"));
    }

    [Fact]
    public void Civilization_keeps_previous_leaders_unique_cities_and_class_overrides_but_allows_explicit_none()
    {
        var old = Node("""
            ["CivilizationInfo",[["Type","CIVILIZATION_TEST"],["bPlayable","1"],["Cities",[["City","Old"],["City","Shared"]]],
            ["Units",[["Unit",[["UnitClassType","UNITCLASS_A"],["UnitType","UNIT_UNIQUE"]]],["Unit",[["UnitClassType","UNITCLASS_B"],["UnitType","UNIT_B"]]]]],
            ["Leaders",[["Leader",[["LeaderName","LEADER_OLD"],["bLeaderAvailability","1"]]]]]]]
            """);
        var next = Node("""
            ["CivilizationInfo",[["Type","CIVILIZATION_TEST"],["bPlayable","0"],["Cities",[["City","New"],["City","Shared"]]],
            ["Units",[["Unit",[["UnitClassType","UNITCLASS_A"],["UnitType","UNIT_A"]]],["Unit",[["UnitClassType","UNITCLASS_B"],["UnitType","NONE"]]]]],
            ["Leaders",[["Leader",[["LeaderName","LEADER_NEW"],["bLeaderAvailability","1"]]],["Leader",[["LeaderName","LEADER_OLD"],["bLeaderAvailability","0"]]]]]]]
            """);
        var classes = new Dictionary<string, DataNode> { ["UNITCLASS_A"] = Node("""["UnitClassInfo",[["DefaultUnit","UNIT_A"]]]""") };
        var result = new RecordMerger(classes, new Dictionary<string, DataNode>()).Merge(old, next).Node;
        Assert.Equal("1", result.Value("bPlayable"));
        Assert.Equal(new[] { "New", "Shared", "Old" }, result.Child("Cities")!.Children.Select(x => x.Text));
        Assert.Equal(new[] { "UNIT_UNIQUE", "NONE" }, result.Child("Units")!.Children.Select(x => x.Value("UnitType")));
        Assert.Equal(new[] { "LEADER_NEW", "LEADER_OLD" }, result.Child("Leaders")!.Children.Select(x => x.Value("LeaderName")));
        Assert.All(result.Child("Leaders")!.Children, x => Assert.Equal("1", x.Value("bLeaderAvailability")));
    }

    [Fact]
    public void Diplomacy_updates_matching_masks_and_retains_unmatched_responses()
    {
        var old = Node("""
            ["DiplomacyInfo",[["Type","AI_DIPLOCOMMENT_TEST"],["Responses",[
            ["Response",[["Leaders",[["Leader",[["LeaderType","LEADER_A"],["bLeaderType","1"]]]]],["Attitudes",""],["DiplomacyPowers",""],["DiplomacyText",[["Text","OLD_A"]]]]],
            ["Response",[["Leaders",[["Leader",[["LeaderType","LEADER_B"],["bLeaderType","1"]]]]],["Attitudes",""],["DiplomacyPowers",""],["DiplomacyText",[["Text","OLD_B"]]]]]]]]]
            """);
        var next = Node("""
            ["DiplomacyInfo",[["Type","AI_DIPLOCOMMENT_TEST"],["Responses",[
            ["Response",[["Leaders",[["Leader",[["LeaderType","LEADER_A"],["bLeaderType","1"]]]]],["Attitudes",""],["DiplomacyPowers",""],["DiplomacyText",[["Text","NEW_A"]]]]]]]]]
            """);
        var responses = Merger().Merge(old, next).Node.Child("Responses")!.Children;
        Assert.Equal(2, responses.Length);
        Assert.Equal("LEADER_B", responses[0].Child("Leaders")!.Children[0].Value("LeaderType"));
        Assert.Equal(new[] { "NEW_A", "OLD_A" }, responses[1].Child("DiplomacyText")!.Children.Select(x => x.Text));
    }

    [Fact]
    public void Census_counts_repeated_paths_per_record_and_preserves_sentinels_without_claiming_runtime_defaults()
    {
        var records = new[]
        {
            new EffectiveRecord("TechInfo", "TECH_A", Node("""["TechInfo",[["Type","TECH_A"],["iCost","0"],["List",[["Value","0"],["Value","2"]]],["Empty",""],["None","NONE"],["Minus","-1"],["Bool","false"]]]"""), [], "ERA_ANCIENT"),
            new EffectiveRecord("TechInfo", "TECH_B", Node("""["TechInfo",[["Type","TECH_B"],["iCost","4"],["List",[["Value","0"]]]]]"""), [])
        };
        var census = Assert.Single(FieldCensus.Build(records));
        var cost = census.Fields.Single(x => x.Field == "iCost");
        Assert.Equal(2, cost.Present);
        Assert.Equal(1, cost.NonDefault);
        var values = census.Fields.Single(x => x.Field == "List/Value");
        Assert.Equal(2, values.Present);
        Assert.Equal(1, values.NonDefault);
        Assert.Equal(new[] { "2" }, values.Examples);
        Assert.All(census.Fields.Where(x => x.Field is "Empty" or "None" or "Minus" or "Bool"), x => Assert.Equal(0, x.NonDefault));
        Assert.Equal(1, census.EraCounts["ERA_ANCIENT"]);
        Assert.Equal(1, census.EraCounts["unknown"]);
    }
}
