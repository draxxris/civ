using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Religion;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The Religion mod is self-contained: the base game and the other main mods load, play and save without it,
/// and say nothing of religion beyond the leftovers docs/DESIGN.md lists under "Religion" (prose, source tags and the
/// AI's religion flavour).</summary>
public sealed class ReligionAbsenceTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static readonly Lazy<ContentDb> WithoutReligion = new(() =>
        ModLoader.Build(new LoadedMods().Sources.Where(s => s.Manifest.Id != "religion")));
    private static readonly Regex Religious = new("religio", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void Without_the_mod_no_definition_leaf_kind_or_setup_option_is_religions()
    {
        var content = WithoutReligion.Value;
        Assert.DoesNotContain("religion", content.LoadedMods);
        Assert.Null(content.Services.Find<IReligionService>());
        Assert.Throws<InvalidDataException>(() => content.Requirements.Validate(new Requirement.Leaf("religion", ReligionConditionDef.StateReligion)));
        Assert.Throws<InvalidDataException>(() => content.Requirements.Validate(new Requirement.Leaf("religion-condition", ReligionConditionDef.StateReligion)));
        Assert.DoesNotContain(content.Get<GameOptionDef>().All, o => Religious.IsMatch(o.Id));
        Assert.DoesNotContain(content.Get<TechDef>().All, t => Religious.IsMatch(t.Id));
        Assert.DoesNotContain(content.Get<VictoryIdentityDef>().All, v => Religious.IsMatch(v.Id));
        Assert.DoesNotContain(content.Get<RuleConstantsDef>().All.Single().Raw.Keys, Religious.IsMatch);
        Assert.DoesNotContain(content.Services.Get<ISetupPolicy>().SelectMany(p => p.GameOptions(content)), o => Religious.IsMatch(o.Id));
        Assert.All(content.Get<LeaderDef>().All, leader =>
            Assert.DoesNotContain(leader.MemoryAttitudePercents.Keys.Concat(leader.MemoryDecays.Keys), Religious.IsMatch));
        Assert.DoesNotContain(content.Get<CivicOptionDef>().All, o => Religious.IsMatch(o.Id));
        Assert.DoesNotContain(content.Get<CivicDef>().All, c => Religious.IsMatch(c.Id + c.Option));
    }

    [Fact]
    public void With_the_mod_its_rows_put_back_what_the_base_rows_left_out()
    {
        var with = mods.Content;
        var leader = with.Get<LeaderDef>("peoples:leader_abubakr");
        Assert.Equal(100, leader.MemoryAttitudePercents["accepted_religion"]);
        Assert.Equal(1, with.Attachment<LeaderReligionDef>(leader.Id)!.SameReligionAttitudeChange);
        Assert.False(WithoutReligion.Value.Get<LeaderDef>(leader.Id).MemoryAttitudePercents.ContainsKey("accepted_religion"));
        Assert.Contains(with.Get<GameOptionDef>().All, o => o.Id == "religion:gameoption_religion_decay");
        Assert.Equal(1000, with.Attachment<RuleConstantsReligionDef>("foundation:rule_constants")!.SpreadRange);
        // The religion civic category and its civics are the mod's, under the same player-visible name.
        Assert.Equal(("Religion", "religion:folklore"), (with.Get<CivicOptionDef>("religion:civicoption_religion").Name,
            with.Get<CivicOptionDef>("religion:civicoption_religion").DefaultCivic));
        Assert.Equal(9, with.Get<CivicDef>().All.Count(c => c.Option == "religion:civicoption_religion"));
        Assert.Equal("religion:state_church", with.Get<LeaderDef>("peoples:leader_asoka").FavoriteCivic);
        Assert.Null(WithoutReligion.Value.Get<LeaderDef>("peoples:leader_asoka").FavoriteCivic);
        Assert.Contains("religion:folklore", with.Get<CivilizationDef>("peoples:america").StartingCivics);
        Assert.DoesNotContain(WithoutReligion.Value.Get<CivilizationDef>("peoples:america").StartingCivics, Religious.IsMatch);
        // Base civics that forbade a religion civic leave it to that civic, which forbids them in turn.
        Assert.Null(WithoutReligion.Value.Get<CivicDef>("government:theocracy").Requirements is { } r
            ? ContentReferences.Leaves(r).FirstOrDefault(l => l.Kind == "civic") : null);
        Assert.Contains(ContentReferences.Leaves(with.Get<CivicDef>("religion:secular").Requirements!), l => l.Id == "government:theocracy");
    }

    [Fact]
    public void The_throne_room_accepts_the_apadana_palace_and_the_alhambra_only_with_the_mod()
    {
        string[] Palaces(ContentDb content) => ContentReferences.Leaves(content.Get<BuildingDef>("cities:throne_room").Requirements!)
            .Where(l => l.Kind == "building-class").Select(l => l.Id).ToArray();
        Assert.Equal(["cities:palace", "cities:great_palace", "cities:versailles", "religion:apadana_palace", "religion:alhambra"], Palaces(mods.Content));
        Assert.Equal(["cities:palace", "cities:great_palace", "cities:versailles"], Palaces(WithoutReligion.Value));

        // A city holding the Alhambra in place of a palace may build the throne room under Autocracy with Absolutism.
        var sim = GameFactory.Create(mods.Content);
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(0)) };
        sim.State.AddCity(city);
        city.Buildings.Add("religion:alhambra");
        sim.TeamOf(0).Techs.Add("research:absolutism");
        sim.State.PlayerById(0).Civics["government:civicoption_power"] = "government:autocracy";
        var context = new RequirementContext(sim, 0, city);
        Assert.True(sim.Content.Requirements.Evaluate(sim.Content.Get<BuildingDef>("cities:throne_room").Requirements, context).Success);
        city.Buildings.Remove("religion:alhambra");
        Assert.False(sim.Content.Requirements.Evaluate(sim.Content.Get<BuildingDef>("cities:throne_room").Requirements, context).Success);
    }

    [Fact]
    public void No_data_file_of_the_base_game_or_another_main_mod_names_religion_but_in_prose_tags_and_flavours()
    {
        var files = mods.Sources.Where(s => s.Manifest.Id != "religion").Select(s => Path.Combine(s.Root, "data")).Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.json", SearchOption.AllDirectories)).ToArray();
        Assert.NotEmpty(files);
        var found = new List<string>();
        foreach (var file in files)
        {
            string text = File.ReadAllText(file);
            if (!Religious.IsMatch(text)) continue;
            Walk(JsonNode.Parse(text), "", Path.GetFileName(file), found);
        }
        Assert.Empty(found);
    }

    [Fact]
    public void A_game_without_the_mod_saves_no_religion()
    {
        var sim = GameFactory.CreateAi(WithoutReligion.Value, seed: 3, players: 2);
        for (int turn = 0; turn < 20; turn++) sim.AdvanceTurn();
        string save = SaveGame.Save(sim);
        Assert.Empty(Regex.Matches(save, "religio", RegexOptions.IgnoreCase));
    }

    /// <summary>Every key or string naming religion that is none of the allowed leftovers, as "file: path".</summary>
    private static void Walk(JsonNode? node, string path, string file, List<string> found)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    // A religion weight among the AI flavours is read by no rule without the mod's content.
                    if (Religious.IsMatch(key) && !(path.EndsWith(".flavors", StringComparison.Ordinal) && key == "religion"))
                        found.Add($"{file}: {path}.{key} (key)");
                    // Prose, art paths and the names of unmapped source fields are not rules.
                    if (key is "pedia" or "help" or "strategy" or "name" or "civilopedia" or "quote" or "icon" or "button" or "unsupported") continue;
                    Walk(value, path + "." + key, file, found);
                }
                break;
            case JsonArray array:
                foreach (var item in array) Walk(item, path + "[]", file, found);
                break;
            case JsonValue value when value.TryGetValue(out string? text) && Religious.IsMatch(text):
                found.Add($"{file}: {path} = {text}");
                break;
        }
    }
}
