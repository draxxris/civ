using Ariel.Content.Schema;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class CliffRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static bool Water(Simulation sim, TileId tile) => sim.Content.Get<TerrainDef>(sim.State.Plots[tile.Value].Terrain).IsWater;
    private static bool Sea(Simulation sim, TileId tile) => Water(sim, tile) && !sim.State.Plots[tile.Value].IsLake;
    private static byte CliffMask(Simulation sim, TileId tile) => sim.State.Plots[tile.Value].CliffEdges;
    private static byte LiftMask(Simulation sim, TileId tile) => sim.State.Plots[tile.Value].CliffLiftEdges;
    private static void SetCliff(Simulation sim, TileId tile, int direction, bool present) => sim.State.SetCliff(tile, direction, present);
    private static bool Cliff(Simulation sim, TileId tile, int direction) => (CliffMask(sim, tile) & (1 << direction)) != 0;
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
    [Trait("Category", "WorldGen")]
    public void Generated_cliffs_have_beaches_and_open_river_mouths(string generator)
    {
        foreach (ulong seed in new ulong[] { 1, 42 })
        {
            var settings = GameFactory.Settings(seed) with { MapGenerator = generator };
            var sim = new Simulation(mods.Content, settings);
            var coast = sim.State.Grid.Tiles.Where(t => !Water(sim, t))
                .Select(t => (Tile: t, Edges: SeaEdges(sim, t))).Where(c => c.Edges.Length > 0).ToArray();
            // The generator fills a share of the sea edges instead of rolling per edge: some but not most sea edges
            // become cliffs. Cliffs are mirrored onto the sea plot, so they are counted from the land side only. The
            // exact share is a tuning knob, so it is deliberately not restated here.
            int cliffs = coast.Sum(c => c.Edges.Count(d => Cliff(sim, c.Tile, d)));
            Assert.InRange(cliffs, 1, coast.Sum(c => c.Edges.Length) / 2);
            var terraces = new List<(TileId High, int Direction)>();
            foreach (var tile in sim.State.Grid.Tiles)
                foreach (int direction in Enumerable.Range(0, 6).Where(d => Cliff(sim, tile, d)))
                {
                    var next = sim.State.Grid.Neighbor(tile, direction);
                    Assert.NotNull(next);
                    Assert.True(Cliff(sim, next.Value, (direction + 3) % 6));
                    int mirror = (direction + 3) % 6;
                    bool lifted = (LiftMask(sim, tile) & (1 << direction)) != 0;
                    bool mirrored = (LiftMask(sim, next.Value) & (1 << mirror)) != 0;
                    Assert.NotEqual(lifted, mirrored);
                    bool water = Water(sim, tile), otherWater = Water(sim, next.Value);
                    if (water || otherWater)
                    {
                        Assert.True(water != otherWater, "A cliff needs land on exactly one side, or land on both.");
                        Assert.False(sim.State.Plots[water ? tile.Value : next.Value.Value].IsLake);
                        bool landHigh = (LiftMask(sim, water ? next.Value : tile) & (1 << (water ? mirror : direction))) != 0;
                        Assert.True(landHigh, "Water never stands above land.");
                        continue;
                    }
                    // A terrace joins two tiles of one walkable area and lifts exactly the higher side.
                    Assert.Equal(sim.State.Plots[tile.Value].AreaId, sim.State.Plots[next.Value.Value].AreaId);
                    if (lifted) terraces.Add((tile, direction));
                }
            // Every land area stays connected without crossing a cliff, so the area id still means walkable.
            foreach (var area in sim.State.Grid.Tiles.Where(t => !Water(sim, t)).GroupBy(t => sim.State.Plots[t.Value].AreaId))
            {
                var members = area.Select(t => t.Value).ToHashSet();
                var reached = new HashSet<int> { area.First().Value };
                var queue = new Queue<int>(reached);
                while (queue.Count > 0)
                {
                    var tile = new TileId(queue.Dequeue());
                    for (int d = 0; d < 6; d++)
                    {
                        if (Cliff(sim, tile, d)) continue;
                        if (sim.State.Grid.Neighbor(tile, d) is not { } next || reached.Contains(next.Value)) continue;
                        if (Water(sim, next)) continue;
                        reached.Add(next.Value);
                        queue.Enqueue(next.Value);
                    }
                }
                Assert.Equal(members.Count, reached.Count);
            }
            // Terraces in line stay far enough apart for the ground between them to fall back to its normal height; the
            // gap grows with the style's ramp, so retuning the ramp cannot silently break the spacing.
            double ramp = new Ariel.Client.Terrain.TerrainTuning().CliffRamp;
            int sameFacing = (int)Math.Ceiling((1 + ramp) / Math.Sqrt(3)) + 2, facing = Math.Max(2, sameFacing - 1);
            for (int i = 0; i < terraces.Count; i++)
                for (int j = i + 1; j < terraces.Count; j++)
                {
                    int steps = Math.Abs(terraces[i].Direction - terraces[j].Direction);
                    steps = Math.Min(steps, 6 - steps);
                    int gap = steps <= 1 ? sameFacing : steps == 3 ? facing : 0;
                    int distance = sim.State.Grid.Distance(terraces[i].High, terraces[j].High);
                    // Neighbouring lifted tiles are one raised mass; only separate terraces in line need the gap.
                    if (gap > 0 && distance > 1) Assert.True(distance >= gap);
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
                if (c.Edges.Any(d => (rivers & (1 << d)) != 0))
                    Assert.Equal(0, CliffMask(sim, c.Tile) & c.Edges.Aggregate(0, (mask, d) => mask | (1 << d)));
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
        SetCliff(sim, new(39), 0, true);
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
        SetCliff(sim, new(39), 0, true);
        Assert.False(sim.TryApply(Move(cargo, enemy.Position), out error)); Assert.Equal("Cliff", error);
        SetCliff(sim, new(39), 0, false);
        Assert.Null(Move(cargo, enemy.Position).Validate(sim)); // Same assault is legal via a beach.
        SetCliff(sim, new(39), 0, true);
        var gunship = Add(sim, 41, "military:gunship");
        SetCliff(sim, new(40), 0, true);
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
        SetCliff(sim, new(39), 0, true);
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
        for (int d = 0; d < 6; d++) SetCliff(sim, new(39), d, true);
        var settler = Add(sim, 39, "military:settler");
        Assert.True(sim.TryApply(new FoundCityWithSettlerCommand { Player = 0, Settler = settler.Id }, out var error), error);
        Assert.Single(SeaEdges(sim, new(39)), d => !Cliff(sim, new(39), d));
        Assert.Equal(62, CliffMask(sim, new(39))); // First direction is the deterministic beach.
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
            SetCliff(sim, new(39), 0, true); // Valid sea edge remains intact.
            SetCliff(sim, new(40), 0, true); // Terrain changes after authorship.
            sim.State.Plots[41].Terrain = "world:coast";
            SetCliff(sim, new(41), 0, true); // Lake edge is invalid: water on both sides.
            SetCliff(sim, new(50), 0, true); // Land/land is a valid terrace, lifted on the named tile.
            SetCliff(sim, new(0), 1, true); // Off-map north edge.
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
        Assert.Equal(1, CliffMask(sim, new(39)));
        Assert.Equal(8, CliffMask(sim, new(40)));
        Assert.Equal(1, CliffMask(sim, new(50)));
        Assert.Equal(8, CliffMask(sim, new(51)));
        Assert.Equal(1, LiftMask(sim, new(50)));
        Assert.Equal(0, LiftMask(sim, new(51)));
        Assert.Equal(0, CliffMask(sim, new(41)));
        Assert.Equal(0, CliffMask(sim, new(42)));
        Assert.Equal(0, CliffMask(sim, new(0)));
        Assert.All(sim.State.Grid.Tiles.Where(t => t.Value is not (39 or 40 or 50 or 51)), t => Assert.Equal(0, CliffMask(sim, t)));
    }

    [Fact]
    public void Land_terrace_blocks_land_movement_but_not_all_terrain_or_air()
    {
        var sim = GameFactory.Scenario(mods.Content);
        SetCliff(sim, new(39), 0, true); // Grass on both sides: a terrace whose lifted tile is 39.
        Assert.Equal(1, LiftMask(sim, new(39)));
        Assert.Equal(0, LiftMask(sim, new(40)));
        var warrior = Add(sim, 39, "military:warrior");
        Assert.False(sim.TryApply(Move(warrior, sim.State.Grid.HexOf(new(40))), out var error));
        Assert.Equal("Cliff", error);
        var gunship = Add(sim, 39, "military:gunship");
        Assert.True(sim.TryApply(Move(gunship, sim.State.Grid.HexOf(new(40))), out error), error);
    }

    [Fact]
    public void Sanitizer_opens_a_terrace_that_would_cut_a_land_area_in_two()
    {
        // A land bridge through a band of ocean is the only way between the north and south halves of the map.
        var plots = Enumerable.Range(0, 25).Select(_ => new Plot { Terrain = "world:grass" }).ToArray();
        for (int col = 0; col < 5; col++)
            if (col != 2) plots[5 + col].Terrain = "world:ocean";
        var sim = Simulation.CreateScenario(mods.Content, GameFactory.Settings(), new(5, 5, false), plots);
        var bridge = sim.State.Grid.TileAt(2, 1);
        var landEdges = Enumerable.Range(0, 6)
            .Where(d => sim.State.Grid.Neighbor(bridge, d) is { } next && !Water(sim, next)).ToArray();
        Assert.NotEmpty(landEdges);
        int landMask = landEdges.Aggregate(0, (mask, d) => mask | (1 << d));
        foreach (int d in landEdges) SetCliff(sim, bridge, d, true);
        Assert.Equal(landMask, CliffMask(sim, bridge) & landMask);
        sim.SanitizeMap();
        // The pass keeps the area walkable, so at least one edge of the bridge must be open again.
        Assert.Contains(landEdges, d => (CliffMask(sim, bridge) & (1 << d)) == 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cliff_edges_mirror_across_neighbors_and_clear_stale_off_map_bits(bool wrap)
    {
        var plots = Enumerable.Range(0, 12).Select(_ => new Plot { Terrain = "world:grass" }).ToArray();
        var sim = Simulation.CreateScenario(mods.Content, GameFactory.Settings(), new(4, 3, wrap), plots);
        foreach (var tile in sim.State.Grid.Tiles)
            for (int direction = 0; direction < 6; direction++)
            {
                SetCliff(sim, tile, direction, true);
                if (sim.State.Grid.Neighbor(tile, direction) is { } next)
                {
                    Assert.NotEqual(0, CliffMask(sim, tile) & (1 << direction));
                    Assert.NotEqual(0, CliffMask(sim, next) & (1 << ((direction + 3) % 6)));
                    SetCliff(sim, tile, direction, false);
                    Assert.Equal(0, CliffMask(sim, tile) & (1 << direction));
                    Assert.Equal(0, CliffMask(sim, next) & (1 << ((direction + 3) % 6)));
                }
                else
                {
                    Assert.Equal(0, CliffMask(sim, tile) & (1 << direction));
                    SetCliff(sim, tile, direction, true);
                    Assert.Equal(0, CliffMask(sim, tile) & (1 << direction));
                }
            }
        Assert.All(sim.State.Grid.Tiles, t => Assert.Equal(0, CliffMask(sim, t)));
    }
}
