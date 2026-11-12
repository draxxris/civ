using System.Text.Json;
using Ariel.Ai;
using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Corporations;
using Ariel.Mods.Espionage;
using Ariel.Mods.GreatPeople;
using Ariel.Mods.ProjectsVictory;
using Ariel.Mods.Revolution;
using Ariel.Mods.ScriptedWonders;
using Ariel.Mods.Trade;
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
        Assert.Null(content.Services.Find<IGreatPeopleService>());
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
             Root(GameContent.OptionalModsFolder, "Espionage"), Root(GameContent.OptionalModsFolder, "Revolution"),
             Root(GameContent.ModsFolder, "GreatPeople"), Root("test-mods", "AiPlanner")]));
        var sim = GameFactory.CreateAi(content, players: 2);
        var catalog = AiType(sim, "AiCatalog").GetMethod("For")!.Invoke(null, [content])!;
        var ids = Property<IReadOnlyList<IAiPlanner>>(catalog, "Planners").Select(p => p.Id).ToArray();

        Assert.Equal(
        [
            "ai:research", "ai:war", "ai:civics", "diplomacy:planner", "ai:settlers", "ai:citizens", "ai:religion",
            "corporations:planner", "ai_planner_probe:planner", "great_people:planner",
            "ai:cities", "ai:workers", "ai:air", "ai:military", "espionage:planner", "revolution:planner", "ai:economy", "ai:hurry",
        ], ids);
    }

    [Fact]
    public void A_mod_production_wish_makes_cities_queue_a_unit_the_ai_would_not_build()
    {
        // The probe adds a courier, which cannot fight, settle, work or explore, and wishes for one from turn 45 on.
        const string courier = "production_wish_probe:courier";
        const int firstTurn = 45;
        var content = GameContent.Build(Rules, ModLoader.FromDirectories([Root("test-mods", "ProductionWish")]));
        var sim = GameFactory.CreateAi(content, seed: 5, players: 2);
        bool Wanted() => sim.State.Units.Any(u => u.Type == courier)
            || sim.State.Cities.Any(c => c.ProductionQueue.Any(o => o.ContentId == courier));

        // Until the wish starts, nothing in the city planner values such a unit, though every city could build it.
        while (sim.State.Turn < firstTurn)
        {
            Assert.False(Wanted(), $"turn {sim.State.Turn}: a courier was queued before anyone wished for it");
            sim.AdvanceTurn();
        }
        Assert.All(sim.State.Cities, c => Assert.True(sim.Rules().CityProduction.Validate(sim, c.Id, "cities:unit", courier).Success));
        for (int turn = 0; turn < 40 && !Wanted(); turn++) sim.AdvanceTurn();
        Assert.True(Wanted(), "no AI city queued the wished-for courier");
    }

    [Fact]
    public void The_base_game_plays_ai_turns_with_none_of_the_feature_mods()
    {
        var content = GameContent.Build(Rules, []);
        var rules = content.Rules();
        Assert.Null(content.Services.Find<IEspionageService>());
        Assert.Null(content.Services.Find<IGreatPeopleService>());
        Assert.Null(content.Services.Find<ITradeRouteService>());
        Assert.Null(content.Services.Find<IRevolutionService>());
        Assert.Null(content.Services.Find<IScriptedWonderService>());
        Assert.Null(content.Services.Find<IProjectVictoryService>());
        Assert.Null(rules.EspionageSpending);
        Assert.Null(rules.TradeLinks);
        Assert.Null(rules.CityUnrest);
        Assert.Null(rules.Scores);
        Assert.Null(rules.CombatLeaders);
        Assert.Empty(rules.FreeBuildingGrants);
        Assert.Empty(rules.BarbarianBarriers);
        Assert.DoesNotContain(content.Services.Get<IAiPlanner>(), p => !p.Id.StartsWith("ai:", StringComparison.Ordinal));
        Assert.Empty(content.Services.Get<IProductionWishes>());

        var sim = GameFactory.CreateAi(content, seed: 3, players: 3);
        for (int turn = 0; turn < 40; turn++) sim.AdvanceTurn();
        foreach (var player in sim.State.Players.Where(p => p.Ai == "ai:standard"))
        {
            Assert.True(sim.State.Cities.Count(c => c.Owner == player.Id) >= 1, $"player {player.Id} has no city");
            Assert.True(sim.TeamOf(player.Id).Techs.Count > 0, $"player {player.Id} researched nothing");
        }
    }

    private static JsonElement Log(Simulation sim, Player player)
    {
        // The probe's state type lives in the test mod's own load context, so read it as saved.
        var extensions = JsonSerializer.SerializeToElement(player.Extensions, CanonicalJson.Options);
        Assert.True(extensions.TryGetProperty("ai_planner_probe:log", out var log), $"player {player.Id}: the probe planner never ran");
        return log;
    }
}
