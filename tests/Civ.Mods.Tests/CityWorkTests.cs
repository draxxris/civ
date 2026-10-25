using Civ.Content;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

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
        var a = AddCity(sim, pair.A);
        var b = AddCity(sim, pair.B);
        foreach (var hex in grid.Range(pair.A, 2).Concat(grid.Range(pair.B, 2)).Where(h => grid.Contains(h) && Land(sim, h)))
            sim.State.PlotAt(hex).Owner = 0;
        var work = Service<ICityWorkService>(sim);

        // Both may claim it while nobody works it; only the second claim fails.
        var none = new Dictionary<string, int>();
        Assert.Null(work.Validate(sim, a.Id, [contested], none));
        Assert.Null(work.Validate(sim, b.Id, [contested], none));
        a.Worked.Add(contested);
        Assert.Equal("Work plot is unavailable", work.Validate(sim, b.Id, [contested], none));
        Assert.Null(work.Validate(sim, a.Id, [contested], none));

        // The rule is symmetric: whichever city works the tile first, the other loses it.
        a.Worked.Clear();
        b.Worked.Add(contested);
        Assert.Equal("Work plot is unavailable", work.Validate(sim, a.Id, [contested], none));
        Assert.Null(work.Validate(sim, b.Id, [contested], none));
    }

    /// <summary>The plot can be worked: land, not a peak, and without an impassable feature, which is what
    /// <see cref="ICityWorkService"/>'s own plot check rejects.</summary>
    private static bool Land(Simulation sim, Hex hex)
    {
        if (!sim.State.Grid.Contains(hex)) return false;
        var plot = sim.State.PlotAt(hex);
        if (plot.Kind == PlotKind.Peak) return false;
        if (plot.Feature is { } feature && sim.Content.Get<Civ.Content.Schema.FeatureDef>(feature).Impassable) return false;
        var terrain = sim.Content.Get<Civ.Content.Schema.TerrainDef>(plot.Terrain);
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
