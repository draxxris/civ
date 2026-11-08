using Ariel.Content;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>Two cities close enough to share workable ground never work the same tile.</summary>
public sealed class CityWorkTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();

    [Fact]
    public void Cities_two_tiles_apart_cannot_work_each_others_tiles()
    {
        var sim = GameFactory.Create(mods.Content, seed: 5);
        GameFactory.NeutralPolitics(sim);
        var grid = sim.State.Grid;
        // A pair of land towns at distance 2 share the tiles between them. That is the only way one tile can be
        // claimed twice: a radius-2 work area reaches past a neighbour that close.
        var pair = grid.Tiles.Select(grid.HexOf)
            .SelectMany(a => grid.Ring(a, 2).Select(b => (A: a, B: b)))
            .First(p => grid.Distance(p.A, p.B) == 2
                && Land(sim, p.A) && Land(sim, p.B)
                && grid.Range(p.A, 1).Any(h => grid.Distance(h, p.B) == 1 && Land(sim, h)));
        var contested = grid.Range(pair.A, 1).First(h => grid.Distance(h, pair.B) == 1 && Land(sim, h));
        var contestedTile = grid.TileAt(contested);
        var a = AddCity(sim, pair.A);
        var b = AddCity(sim, pair.B);
        foreach (var hex in grid.Range(pair.A, 2).Concat(grid.Range(pair.B, 2)).Where(h => grid.Contains(h) && Land(sim, h)))
            sim.State.PlotAt(hex).Owner = 0;
        var work = Service<ICityWorkService>(sim);

        // Both may claim it while nobody works it; only the second claim fails.
        var none = new Dictionary<string, int>();
        Assert.Null(work.Validate(sim, a.Id, [contestedTile], none));
        Assert.Null(work.Validate(sim, b.Id, [contestedTile], none));
        a.Worked.Add(contestedTile);
        Assert.Equal("Work plot is unavailable", work.Validate(sim, b.Id, [contestedTile], none));
        Assert.Null(work.Validate(sim, a.Id, [contestedTile], none));

        // The rule is symmetric: whichever city works the tile first, the other loses it.
        a.Worked.Clear();
        b.Worked.Add(contestedTile);
        Assert.Equal("Work plot is unavailable", work.Validate(sim, a.Id, [contestedTile], none));
        Assert.Null(work.Validate(sim, b.Id, [contestedTile], none));
    }

    [Fact]
    public void City_worked_plots_are_canonical_tile_ids_and_queryable_via_gamestate()
    {
        var sim = GameFactory.Create(mods.Content, seed: 7);
        GameFactory.NeutralPolitics(sim);
        var grid = sim.State.Grid;
        var cityPos = new Hex(3, 3);
        var cityTile = grid.TileAt(cityPos);
        var city = AddCity(sim, cityPos);
        var neighbors = grid.Neighbors(cityTile).Take(3).ToArray();
        foreach (var n in neighbors)
        {
            var p = sim.State.Plots[n.Value];
            p.Owner = 0;
            p.Terrain = "world:grass";
            p.Kind = PlotKind.Flat;
            p.Feature = null;
        }

        var tileA = neighbors[0];
        var tileB = neighbors[1];

        Assert.False(city.IsWorking(tileA));
        Assert.False(sim.State.IsPlotWorked(tileA));
        Assert.Null(sim.State.WorkingCity(tileA));

        // Center tile is resolved via CityAtTile, not WorkingCity.
        Assert.Same(city, sim.State.CityAtTile(cityTile));
        Assert.Null(sim.State.WorkingCity(cityTile));

        city.Worked.Add(tileB);
        city.Worked.Add(tileA);
        city.NormalizeWorked();

        // Stored in canonical ascending order
        Assert.True(city.Worked[0].Value < city.Worked[1].Value);
        Assert.True(city.IsWorking(tileA));
        Assert.True(city.IsWorking(tileB));
        Assert.True(sim.State.IsPlotWorked(tileA));
        Assert.True(sim.State.IsPlotWorked(tileB));
        Assert.Same(city, sim.State.WorkingCity(tileA));
        Assert.Same(city, sim.State.WorkingCity(tileB));
    }

    [Fact]
    public void City_assignment_command_accepts_tile_ids_and_normalizes_canonical_order()
    {
        var sim = GameFactory.Create(mods.Content, seed: 11);
        GameFactory.NeutralPolitics(sim);
        var grid = sim.State.Grid;
        var cityPos = new Hex(4, 4);
        var cityTile = grid.TileAt(cityPos);
        var city = AddCity(sim, cityPos);
        city.Population = 5;
        var neighbors = grid.Neighbors(cityTile).Take(3).ToArray();
        foreach (var n in neighbors)
        {
            var p = sim.State.Plots[n.Value];
            p.Owner = 0;
            p.Terrain = "world:grass";
            p.Kind = PlotKind.Flat;
            p.Feature = null;
        }

        var unorderedTiles = new[] { neighbors[2], neighbors[0], neighbors[1], neighbors[0] };
        var command = new SetCityAssignmentsCommand
        {
            Player = 0,
            CityId = city.Id,
            Tiles = unorderedTiles,
        };

        // Duplicate tiles fail command validation
        Assert.Equal("Duplicate work plot", command.Validate(sim));

        // Valid distinct unordered list succeeds and is stored in sorted ascending order
        var distinctUnordered = new[] { neighbors[2], neighbors[0], neighbors[1] };
        var validCommand = new SetCityAssignmentsCommand
        {
            Player = 0,
            CityId = city.Id,
            Tiles = distinctUnordered,
        };

        Assert.Null(validCommand.Validate(sim));
        Assert.True(sim.TryApply(validCommand, out var error), error);

        Assert.Equal(3, city.Worked.Count);
        for (int i = 1; i < city.Worked.Count; i++)
            Assert.True(city.Worked[i - 1].Value < city.Worked[i].Value);
    }

    /// <summary>The plot can be worked: land, not a peak, and without an impassable feature, which is what
    /// <see cref="ICityWorkService"/>'s own plot check rejects.</summary>
    private static bool Land(Simulation sim, Hex hex)
    {
        if (!sim.State.Grid.Contains(hex)) return false;
        var plot = sim.State.PlotAt(hex);
        if (plot.Kind == PlotKind.Peak) return false;
        if (plot.Feature is { } feature && sim.Content.Get<Ariel.Content.Schema.FeatureDef>(feature).Impassable) return false;
        var terrain = sim.Content.Get<Ariel.Content.Schema.TerrainDef>(plot.Terrain);
        return !terrain.IsWater && !terrain.Impassable;
    }

    private static City AddCity(Simulation sim, Hex position)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = position, Population = 3 };
        sim.State.AddCity(city);
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        return city;
    }
}
