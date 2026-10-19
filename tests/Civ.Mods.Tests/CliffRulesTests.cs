using Civ.Content.Schema;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class CliffRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static bool Water(Simulation sim, TileId tile) => sim.Content.Get<TerrainDef>(sim.State.Plots[tile.Value].Terrain).IsWater;
    private static bool Sea(Simulation sim, TileId tile) => Water(sim, tile) && !sim.State.Plots[tile.Value].IsLake;
    private static bool Cliff(Simulation sim, TileId tile, int direction) => (sim.State.Plots[tile.Value].CliffEdges & (1 << direction)) != 0;
    private static int[] SeaEdges(Simulation sim, TileId tile) => Enumerable.Range(0, 6)
        .Where(d => sim.State.Grid.Neighbor(tile, d) is { } next && Sea(sim, next)).ToArray();
    private static Unit Add(Simulation sim, int tile, string type, int owner = 0)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = sim.State.Grid.HexOf(new(tile)) };
        sim.State.AddUnit(unit);
        Service<IUnitService>(sim).Initialize(sim, unit.Id);
        Service<IVisibilityService>(sim).Recompute(sim);
        return unit;
    }
    private static MoveUnitPathCommand Move(Unit unit, Hex destination) => new() { Player = unit.Owner, Unit = unit.Id, Path = [destination] };

    [Theory]
    [InlineData("world:continents")]
    [InlineData("world:inland_sea")]
    [InlineData("world:king_of_the_hill")]
    [InlineData("world:new_world")]
    [InlineData("world:twin_continents")]
    [InlineData("world:great_plains")]
    public void Generated_cliffs_are_repeatable_with_beaches_and_open_river_mouths(string generator)
    {
        foreach (ulong seed in new ulong[] { 1, 42 })
        {
            var settings = GameFactory.Settings(seed) with { MapGenerator = generator };
            var sim = new Simulation(mods.Content, settings);
            var repeat = new Simulation(mods.Content, settings);
            Assert.Equal(sim.State.Plots.Select(p => p.CliffEdges), repeat.State.Plots.Select(p => p.CliffEdges));
            var coast = sim.State.Grid.Tiles.Where(t => !Water(sim, t))
                .Select(t => (Tile: t, Edges: SeaEdges(sim, t))).Where(c => c.Edges.Length > 0).ToArray();
            int edges = coast.Sum(c => c.Edges.Length);
            int cliffs = coast.Sum(c => c.Edges.Count(d => Cliff(sim, c.Tile, d)));
            if (edges > 0) Assert.InRange((double)cliffs / edges, .15, .25);
            foreach (var tile in sim.State.Grid.Tiles)
                foreach (int direction in Enumerable.Range(0, 6).Where(d => Cliff(sim, tile, d)))
                {
                    var next = sim.State.Grid.Neighbor(tile, direction);
                    Assert.NotNull(next);
                    Assert.True(Cliff(sim, next.Value, (direction + 3) % 6));
                    Assert.NotEqual(Water(sim, tile), Water(sim, next.Value));
                    Assert.False(sim.State.Plots[Water(sim, tile) ? tile.Value : next.Value.Value].IsLake);
                }
            foreach (var area in coast.GroupBy(c => sim.State.Plots[c.Tile.Value].AreaId))
            {
                int seaEdges = area.Sum(c => c.Edges.Length);
                int beaches = area.Sum(c => c.Edges.Count(d => !Cliff(sim, c.Tile, d)));
                int size = sim.State.Plots.Count(p => p.AreaId == area.Key);
                Assert.True(beaches >= (size <= 4 ? (seaEdges + 1) / 2 : 1));
            }
            foreach (var start in sim.State.Players.Select(p => p.StartingPlot!.Value))
            {
                var seaEdges = SeaEdges(sim, start);
                if (seaEdges.Length > 0) Assert.Contains(seaEdges, d => !Cliff(sim, start, d));
            }
            foreach (var c in coast)
            {
                byte rivers = sim.State.Plots[c.Tile.Value].RiverEdges;
                if (c.Edges.Any(d => (rivers & (1 << d)) != 0)) Assert.Equal(0, sim.State.Plots[c.Tile.Value].CliffEdges);
                foreach (int d in c.Edges.Where(d => Cliff(sim, c.Tile, d)))
                    Assert.Equal(0, rivers & ((1 << d) | (1 << ((d + 5) % 6)) | (1 << ((d + 1) % 6))));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cargo_uses_beaches_for_boarding_and_landing_even_with_amphibious_training(bool amphibious)
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Plots[40].Terrain = "world:coast";
        var ship = Add(sim, 40, "military:cog");
        var cargo = Add(sim, 39, "military:warrior");
        if (amphibious) cargo.Promotions.Add("military:amphibious");
        sim.State.SetCliff(new(39), 0, true);
        var boarding = Move(cargo, ship.Position);
        var hash = sim.ComputeHash();
        Assert.False(sim.TryApply(boarding, out var error)); Assert.Equal("Cliff", error);
        Assert.Equal(hash, sim.ComputeHash());
        var path = Service<IPathfindingService>(sim).FindPath(sim, cargo.Id, ship.Position);
        Assert.NotNull(path);
        Assert.NotEqual(ship.Position, path.Steps[0]);
        // The next coast edge is a beach: walk around, then board the same transport.
        var beach = sim.State.Grid.Neighbors(new TileId(40)).First(t => t != new TileId(39) && !Water(sim, t)
            && sim.State.Grid.Neighbors(new TileId(39)).Contains(t));
        Assert.True(sim.TryApply(Move(cargo, sim.State.Grid.HexOf(beach)), out error), error);
        cargo.MovesLeft = 120;
        Assert.True(sim.TryApply(boarding, out error), error);
        Assert.Equal(ship.Id, cargo.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
        cargo.MovesLeft = 120;
        var cliffLand = sim.State.Grid.HexOf(new(39));
        Assert.Equal("Cliff", Service<IUnitService>(sim).ValidateUnload(sim, 0, cargo.Id, cliffLand));
        Assert.False(sim.TryApply(Move(cargo, cliffLand), out error)); Assert.Equal("Cliff", error);
        Assert.True(sim.TryApply(new UnloadUnitCommand { Player = 0, Unit = cargo.Id, Destination = sim.State.Grid.HexOf(beach) }, out error), error);
        Assert.Null(cargo.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
    }

    [Fact]
    public void Amphibious_assault_is_blocked_but_all_terrain_movement_ignores_cliffs()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Plots[40].Terrain = "world:coast";
        var ship = Add(sim, 40, "military:cog");
        var cargo = Add(sim, 40, "military:warrior");
        cargo.Promotions.Add("military:amphibious");
        Assert.True(sim.TryApply(new LoadUnitCommand { Player = 0, Unit = cargo.Id, Transport = ship.Id }, out var error), error);
        var enemy = Add(sim, 39, "military:warrior", 1);
        sim.State.SetWar(0, 1, true);
        sim.State.SetCliff(new(39), 0, true);
        Assert.False(sim.TryApply(Move(cargo, enemy.Position), out error)); Assert.Equal("Cliff", error);
        sim.State.SetCliff(new(39), 0, false);
        Assert.Null(Move(cargo, enemy.Position).Validate(sim)); // Same assault is legal via a beach.
        sim.State.SetCliff(new(39), 0, true);
        var gunship = Add(sim, 41, "military:gunship");
        sim.State.SetCliff(new(40), 0, true);
        Assert.True(sim.TryApply(Move(gunship, ship.Position), out error), error);
        Assert.True(sim.TryApply(Move(gunship, sim.State.Grid.HexOf(new(41))), out error), error);
    }

    [Fact]
    public void Ships_enter_and_leave_cities_only_across_beach_edges()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Plots[39].Terrain = "world:coast";
        sim.State.Plots[41].Terrain = "world:coast";
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new(40)) };
        sim.State.AddCity(city);
        sim.State.SetCliff(new(39), 0, true);
        var ship = Add(sim, 39, "military:cog");
        Assert.False(sim.TryApply(Move(ship, city.Position), out var error)); Assert.Equal("Cliff", error);
        var beachShip = Add(sim, 41, "military:cog");
        Assert.True(sim.TryApply(Move(beachShip, city.Position), out error), error);
        Assert.False(sim.TryApply(Move(beachShip, ship.Position), out error)); Assert.Equal("Cliff", error);
        beachShip.MovesLeft = 240;
        Assert.True(sim.TryApply(Move(beachShip, sim.State.Grid.HexOf(new(41))), out error), error);
    }

    [Fact]
    public void Founding_a_city_opens_one_beach_without_changing_other_cliffs()
    {
        var sim = GameFactory.Scenario(mods.Content);
        foreach (var next in sim.State.Grid.Neighbors(new TileId(39))) sim.State.Plots[next.Value].Terrain = "world:coast";
        for (int d = 0; d < 6; d++) sim.State.SetCliff(new(39), d, true);
        var settler = Add(sim, 39, "military:settler");
        Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = 0, Settler = settler.Id }, out var error), error);
        Assert.Single(SeaEdges(sim, new(39)), d => !Cliff(sim, new(39), d));
        Assert.Equal(62, sim.State.Plots[39].CliffEdges); // First direction is the deterministic beach.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scenario_and_load_clear_invalid_cliffs_without_rng_or_events(bool load)
    {
        var plots = Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray();
        void Author(Simulation sim)
        {
            sim.State.Plots[40].Terrain = "world:coast";
            sim.State.Plots[42].Terrain = "world:coast"; sim.State.Plots[42].IsLake = true;
            sim.State.SetCliff(new(39), 0, true); // Valid sea edge remains intact.
            sim.State.SetCliff(new(40), 0, true); // Terrain changes after authorship.
            sim.State.Plots[41].Terrain = "world:coast";
            sim.State.SetCliff(new(41), 0, true); // Lake edge is invalid.
            sim.State.SetCliff(new(50), 0, true); // Land/land is invalid.
            sim.State.Plots[0].CliffEdges = 2; // Off-map north edge.
        }
        var sim = Simulation.CreateScenario(mods.Content, GameFactory.Settings(), new(12, 8, true), plots, load ? null : Author);
        if (load)
        {
            Author(sim);
            var rng = sim.Rng.State;
            sim = SaveGame.Load(mods.Content, SaveGame.Save(sim));
            Assert.Equal(rng, sim.Rng.State);
            Assert.Empty(sim.Events.Published);
        }
        Assert.Equal(1, sim.State.Plots[39].CliffEdges);
        Assert.Equal(8, sim.State.Plots[40].CliffEdges);
        Assert.All(sim.State.Plots.Where((_, i) => i != 39 && i != 40), p => Assert.Equal(0, p.CliffEdges));
    }
}
