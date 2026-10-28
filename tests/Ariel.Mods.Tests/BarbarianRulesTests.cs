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
        sim.State.PlayerById(u.Owner).Extensions.Get<MilitaryPlayerState>(sim.Content)?.Barbarian == true
        && sim.Content.Get<S.UnitDef>(u.Type).Domain == "military:domain_sea");

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
        sim.Content.TurnSystems.Get("military:barbarian_spawns").Run(sim);
        loaded.Content.TurnSystems.Get("military:barbarian_spawns").Run(loaded);
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
        var sim = Coast(); sim.Content.TurnSystems.Get("military:barbarian_spawns").Run(sim);
        var raider = Ships(sim).First();
        foreach (var unit in sim.State.Units.Where(u => u.Id != raider.Id).ToArray()) sim.State.RemoveUnit(unit.Id);
        sim.State.MoveUnit(raider.Id, sim.State.Grid.HexOf(new(40)));
        var fish = sim.State.Plots[41]; fish.Owner = 0; fish.Improvement = "world:improvement_improvement_fishing_boats";
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.Content.TurnSystems.Get("military:barbarian_behavior").Run(sim);
        loaded.Content.TurnSystems.Get("military:barbarian_behavior").Run(loaded);
        Assert.Equal(sim.State.Grid.HexOf(new(41)), raider.Position); Assert.Null(fish.Improvement);
        Assert.True(raider.MovesLeft < sim.Content.Services.Get<IUnitService>().Single().MaximumMoves(sim, raider.Id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData("research:ship_building", "military:galley")]
    [InlineData("research:seafaring", "military:wargalley")]
    public void Early_naval_technology_unlocks_barbarian_raiders(string tech, string expectedShip)
    {
        var sim = Coast();
        foreach (var team in sim.State.Teams) team.Techs.Clear();
        sim.Content.TurnSystems.Get("military:barbarian_spawns").Run(sim);
        Assert.Empty(Ships(sim));
        sim.TeamOf(0).Techs.Add(tech);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.Content.TurnSystems.Get("military:barbarian_spawns").Run(sim);
        loaded.Content.TurnSystems.Get("military:barbarian_spawns").Run(loaded);
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
