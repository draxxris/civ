using System.Text.Json;
using Ariel.Ai;
using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Corporations;
using Xunit;
using static Ariel.Mods.Tests.AiBindings;

namespace Ariel.Mods.Tests;

/// <summary>Mods contribute planners to the standard computer player through <see cref="IAiPlanner"/>.</summary>
public sealed class AiPlannerTests
{
    private static string Root(string folder, string name) => Path.Combine(AppContext.BaseDirectory, folder, name);
    private static string Rules => Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder);

    /// <summary>The base game and the probe test mod, and no feature mod at all: no corporations, great people or trade.</summary>
    private static readonly Lazy<ContentDb> BaseWithProbe = new(() =>
        GameContent.Build(Rules, ModLoader.FromDirectories([Root("test-mods", "AiPlanner")])));

    [Fact]
    public void A_mod_planner_issues_its_command_during_every_ai_turn()
    {
        var sim = GameFactory.CreateAi(BaseWithProbe.Value, seed: 5, players: 2);
        for (int turn = 0; turn < 6; turn++) sim.AdvanceTurn();

        foreach (var player in sim.State.Players.Where(p => p.Ai == "ai:standard"))
        {
            // One note per turn, the current one included: the AI plays a turn as soon as it starts.
            var turns = Log(sim, player).GetProperty("Turns").EnumerateArray().Select(t => t.GetInt32()).ToArray();
            Assert.Equal(Enumerable.Range(sim.State.Turn - turns.Length + 1, turns.Length), turns);
            Assert.True(turns.Length >= 6, $"player {player.Id}: {turns.Length} notes in six turns");
        }
    }

    [Fact]
    public void Without_the_feature_mods_the_ai_still_plays()
    {
        var content = BaseWithProbe.Value;
        Assert.Null(content.Services.Find<ICorporationService>());
        Assert.Null(content.Rules().GreatPeople);
        Assert.DoesNotContain(content.Services.Get<IAiPlanner>(), p => p.Id is "corporations:planner" or "great_people:planner");

        var sim = GameFactory.CreateAi(content, seed: 5, players: 2);
        for (int turn = 0; turn < 30; turn++) sim.AdvanceTurn();
        foreach (var player in sim.State.Players.Where(p => p.Ai == "ai:standard"))
        {
            Assert.True(sim.State.Cities.Count(c => c.Owner == player.Id) >= 1, $"player {player.Id} has no city");
            Assert.True(sim.TeamOf(player.Id).Techs.Count > 0, $"player {player.Id} researched nothing");
        }
    }

    [Fact]
    public void Planners_run_by_order_then_id_with_contributed_ones_in_their_slots()
    {
        var content = GameContent.Build(Rules, ModLoader.FromDirectories(
            [Root(GameContent.OptionalModsFolder, "Corporations"), Root(GameContent.OptionalModsFolder, "Diplomacy"),
             Root(GameContent.ModsFolder, "GreatPeople"), Root("test-mods", "AiPlanner")]));
        var sim = GameFactory.CreateAi(content, players: 2);
        var catalog = AiType(sim, "AiCatalog").GetMethod("For")!.Invoke(null, [content])!;
        var ids = Property<IReadOnlyList<IAiPlanner>>(catalog, "Planners").Select(p => p.Id).ToArray();

        Assert.Equal(
        [
            "ai:research", "ai:war", "ai:civics", "ai:diplomacy", "ai:settlers", "ai:citizens", "ai:religion",
            "corporations:planner", "ai_planner_probe:planner", "great_people:planner",
            "ai:cities", "ai:workers", "ai:air", "ai:military", "ai:espionage", "ai:stability", "ai:economy", "ai:hurry",
        ], ids);
    }

    private static JsonElement Log(Simulation sim, Player player)
    {
        // The probe's state type lives in the test mod's own load context, so read it as saved.
        var extensions = JsonSerializer.SerializeToElement(player.Extensions, CanonicalJson.Options);
        Assert.True(extensions.TryGetProperty("ai_planner_probe:log", out var log), $"player {player.Id}: the probe planner never ran");
        return log;
    }
}
