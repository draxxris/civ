using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

public sealed class BarbarianRulesTests
{
    // xunit builds a new test class instance per test, so the probe content must be static to be built once.
    private static readonly Lazy<ContentDb> NavalContent = new(() =>
    {
        var loaded = new LoadedMods();
        var probe = new ModSource { Manifest = new() { Id = "naval_probe", Version = "1", Dependencies = ["foundation"], References = ["foundation"] },
            Root = loaded.Root, Instance = new NavalSpawns() };
        return ModLoader.Build(loaded.Sources.Append(probe));
    });
    private static Simulation Coast()
    {
        var sim = GameFactory.Scenario(NavalContent.Value);
        GameFactory.NeutralPolitics(sim); sim.State.Turn = 100;
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var plot = sim.State.Plots[tile.Value];
            plot.Owner = null; plot.Visible.Clear();
            plot.Terrain = tile.Value % 12 % 3 == 0 ? "world:grass" : "world:coast";
            plot.Height = plot.Terrain == "world:coast" ? Levels.ShallowWater : Levels.Flat;
            plot.AreaId = plot.Terrain == "world:coast" ? 1 : 0;
        }
        foreach (int tile in new[] { 0, 3, 6 }) sim.State.AddCity(new City
        {
            Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new(tile)),
        });
        sim.TeamOf(0).Techs.UnionWith(sim.Content.Get<S.TechDef>().All.Select(t => t.Id));
        return sim;
    }
    private static IEnumerable<Unit> Ships(Simulation sim) => sim.State.Units.Where(u =>
        PlayerRules.IsBarbarian(sim, u.Owner)
        && sim.Content.Get<S.UnitDef>(u.Type).Domain == "military:domain_sea");

    [Fact]
    public void The_mod_answers_the_hostile_faction_and_runs_spawns_and_behaviour()
    {
        var content = new LoadedMods().Content;
        Assert.NotNull(content.Rules().HostileFaction);
        Assert.Equal(TurnPhase.GameBegin, content.TurnSystems.Get("barbarians:behavior").Order);
        // Just after the spawn phase's random events, where barbarian spawns ran as part of the base game.
        Assert.Equal(TurnPhase.Spawns + 1, content.TurnSystems.Get("barbarians:spawns").Order);
    }

    [Fact]
    public void Without_the_mod_no_hostile_player_appears_and_the_ai_still_plays()
    {
        var content = ModLoader.Build(new LoadedMods().Sources.Where(s => s.Manifest.Id != "barbarians"));
        Assert.Null(content.Rules().HostileFaction);
        Assert.DoesNotContain(content.TurnSystems.All, s => s.Id.StartsWith("barbarians:", StringComparison.Ordinal));

        var sim = GameFactory.CreateAi(content, seed: 5, players: 2);
        var goodies = new List<GoodyReceived>();
        for (int turn = 0; turn < 30; turn++)
        {
            sim.AdvanceTurn();
            goodies.AddRange(sim.Events.PreviousTurn.OfType<GoodyReceived>());
        }
        Assert.Null(PlayerRules.BarbarianPlayer(sim));
        Assert.Equal(2, sim.State.Players.Count);
        Assert.All(sim.State.Players, p => Assert.True(sim.State.Cities.Any(c => c.Owner == p.Id), $"player {p.Id} has no city"));
        // Huts still pay out, but never with hostile villagers: they would need a hostile faction to belong to.
        Assert.All(goodies, g => Assert.Null(sim.Content.Get<S.GoodyDef>(g.Goody).BarbarianUnit));
    }

    [Fact]
    public void Naval_spawns_use_only_unseen_unowned_nonlake_coastal_water_and_replay()
    {
        var sim = Coast();
        foreach (int tile in new[] { 1, 2, 4, 5, 7, 8 }) sim.State.Plots[tile].Visible.Add(0);
        foreach (int tile in new[] { 13, 14, 16 }) sim.State.Plots[tile].Owner = 0;
        foreach (int tile in new[] { 25, 26, 28 }) sim.State.Plots[tile].IsLake = true;
        var eligible = sim.State.Grid.Tiles.Where(t => sim.State.Plots[t.Value].Terrain == "world:coast"
            && sim.State.Plots[t.Value].Owner is null && !sim.State.Plots[t.Value].IsLake && !sim.State.Plots[t.Value].Visible.Contains(0))
            .Select(t => sim.State.Grid.HexOf(t)).ToHashSet();
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.Content.TurnSystems.Get("barbarians:spawns").Run(sim);
        loaded.Content.TurnSystems.Get("barbarians:spawns").Run(loaded);
        Assert.NotEmpty(Ships(sim));
        Assert.All(Ships(sim), ship =>
        {
            Assert.Contains(ship.Position, eligible);
            Assert.Contains(sim.State.Grid.Neighbors(ship.Position), h => sim.State.PlotAt(h).Terrain == "world:grass");
            Assert.Contains(0, sim.TeamOf(ship.Owner).Wars);
        });
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Naval_raiders_sail_to_coastal_fishing_boats_and_pillage_them()
    {
        var sim = Coast(); sim.Content.TurnSystems.Get("barbarians:spawns").Run(sim);
        var raider = Ships(sim).First();
        foreach (var unit in sim.State.Units.Where(u => u.Id != raider.Id).ToArray()) sim.State.RemoveUnit(unit.Id);
        sim.State.MoveUnit(raider.Id, sim.State.Grid.HexOf(new(40)));
        var fish = sim.State.Plots[41]; fish.Owner = 0; fish.Improvement = "world:improvement_improvement_fishing_boats";
        sim.Rules().Visibility.Recompute(sim);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.Content.TurnSystems.Get("barbarians:behavior").Run(sim);
        loaded.Content.TurnSystems.Get("barbarians:behavior").Run(loaded);
        Assert.Equal(sim.State.Grid.HexOf(new(41)), raider.Position); Assert.Null(fish.Improvement);
        Assert.True(raider.MovesLeft < sim.Rules().Units.MaximumMoves(sim, raider.Id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData("research:ship_building", "military:galley")]
    [InlineData("research:seafaring", "military:wargalley")]
    public void Early_naval_technology_unlocks_barbarian_raiders(string tech, string expectedShip)
    {
        var sim = Coast();
        foreach (var team in sim.State.Teams) team.Techs.Clear();
        sim.Content.TurnSystems.Get("barbarians:spawns").Run(sim);
        Assert.Empty(Ships(sim));
        sim.TeamOf(0).Techs.Add(tech);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.Content.TurnSystems.Get("barbarians:spawns").Run(sim);
        loaded.Content.TurnSystems.Get("barbarians:spawns").Run(loaded);
        Assert.NotEmpty(Ships(sim)); Assert.All(Ships(sim), ship => Assert.Equal(expectedShip, ship.Type));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    private sealed class NavalSpawns : IMod
    {
        public void Register(IModContext context) => context.Content<S.HandicapDef>().Override(new()
        {
            Id = "foundation:handicap_noble", UnownedWaterTilesPerBarbarianUnit = 1,
        });
    }
}
