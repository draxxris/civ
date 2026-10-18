using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.World;

/// <summary>CvPlayer::findStartingPlot / foundValue: food, production, coast, fresh water and distance pressure.</summary>
internal sealed class StartingPositions(IWaterService water) : IStartingPositionService
{
    public IReadOnlyList<TileId> Select(Simulation sim, int count)
    {
        using var trace = Civ.Modding.StartupTrace.Phase("world starting positions");
        var tuning = Geography.Tuning(sim.Content);
        // Selection reads an unchanged map. Reuse each tile's potential across overlapping start ranges.
        var potentials = sim.State.Grid.Tiles.Select(t => TilePotential(sim, t, tuning)).ToArray();
        var candidates = sim.State.Grid.Tiles.Where(t => CanStart(sim, t))
            .Select(t => (Tile: t, Score: Score(sim, t, tuning, potentials)))
            .OrderByDescending(c => c.Score).ThenBy(c => c.Tile.Value).ToArray();
        if (candidates.Length == 0) throw new InvalidOperationException("Map has no habitable starts");

        var starts = new List<TileId>();
        foreach (var candidate in candidates)
        {
            if (starts.Any(t => sim.State.Grid.Distance(t, candidate.Tile) < tuning.StartSpacing)) continue;
            starts.Add(candidate.Tile);
            if (starts.Count != count) continue;
            return starts;
        }
        throw new InvalidOperationException("Map cannot supply fair separated starts for this player count; use a larger world or another seed");
    }
    // CvGame::normalizeStartingPlots: improve weak starts without changing land shape or existing bonuses.
    public void Normalize(Simulation sim, IReadOnlyList<TileId> starts)
    {
        using var trace = Civ.Modding.StartupTrace.Phase("world normalize starts");
        var tuning = Geography.Tuning(sim.Content);
        int minimum = starts.Max(t => Score(sim, t, tuning)) * tuning.StartQualityPercent / 100;
        foreach (var start in starts) NormalizeStart(sim, start, minimum, tuning);
    }
    private void NormalizeStart(Simulation sim, TileId center, int minimum, S.HexOverridesDef tuning)
    {
        foreach (var tile in sim.State.Grid.Range(center, tuning.StartRadius)
            .OrderBy(t => TilePotential(sim, t, tuning)).ThenBy(t => t.Value))
        {
            if (Score(sim, center, tuning) >= minimum) return;
            var plot = sim.State.Plots[tile.Value];
            if (Geography.Water(sim.Content, plot) || plot.Kind == PlotKind.Peak || plot.Bonus is not null) continue;
            plot.Terrain = "world:grass";
            plot.Feature = null;
            var forest = sim.Content.Get<S.FeatureDef>("world:forest");
            if (tile != center && Placement.FeatureValid(sim.State.Grid, sim.State.Plots, sim.Content, tile, forest)) plot.Feature = forest.Id;
        }
    }
    private static bool CanStart(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        if (Geography.Water(sim.Content, plot) || Geography.Impassable(sim.Content, plot)) return false;
        if (plot.Feature is { } feature && sim.Content.Get<S.FeatureDef>(feature).NoCity) return false;
        return terrain.CanFoundCity || (terrain.FoundCoast && Geography.Coast(sim.State.Grid, sim.State.Plots, sim.Content, tile))
            || (terrain.FoundFreshWater && sim.Content.Services.Get<IWaterService>().Single().IsFreshWater(sim, tile));
    }
    private static int TilePotential(Simulation sim, TileId tile, S.HexOverridesDef tuning)
    {
        var plot = sim.State.Plots[tile.Value];
        if (Geography.Impassable(sim.Content, plot)) return 0;
        var potential = sim.Content.Get<S.TerrainDef>(plot.Terrain).Yields;
        if (plot.Feature is { } feature) potential += sim.Content.Get<S.FeatureDef>(feature).YieldChanges;
        if (plot.Kind == PlotKind.Hills) potential += new Civ.Content.YieldVector(sim.Content.Get<S.YieldDef>("foundation:yield_food").HillsChange,
            sim.Content.Get<S.YieldDef>("foundation:yield_production").HillsChange, 0);
        return checked((int)(Math.Max(0, potential.Food) * tuning.StartFoodWeight + Math.Max(0, potential.Production) * tuning.StartProductionWeight));
    }
    private int Score(Simulation sim, TileId center, S.HexOverridesDef tuning, int[]? potentials = null)
    {
        int total = 0;
        foreach (var tile in sim.State.Grid.Range(center, tuning.StartOuterRadius))
        {
            int score = potentials is null ? TilePotential(sim, tile, tuning) : potentials[tile.Value];
            total += sim.State.Grid.Distance(center, tile) <= tuning.StartRadius ? score * 2 : score;
        }
        if (Geography.Coast(sim.State.Grid, sim.State.Plots, sim.Content, center)) total += tuning.StartCoastWeight;
        if (water.IsFreshWater(sim, center)) total += tuning.StartFreshWaterWeight;
        return total;
    }
}
