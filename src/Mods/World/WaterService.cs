using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.World;

internal sealed class WaterService : IWaterService
{
    public bool IsFreshWater(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (Geography.Water(sim.Content, plot) || plot.Kind == PlotKind.Peak) return false;
        if (plot.RiverEdges != 0) return true;
        return sim.State.Grid.Range(tile, 1).Any(t => Source(sim, t));
    }
    private static bool Source(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        return plot.IsLake || (plot.Feature is { } id && sim.Content.Get<S.FeatureDef>(id).AddsFreshWater);
    }
    public bool IsIrrigated(Simulation sim, int player, TileId tile)
    {
        if (IsFreshWater(sim, tile)) return true;
        bool carries = sim.TeamOf(player).Techs.Any(t => sim.Content.Get<S.TechDef>(t).Irrigation);
        if (!carries) return false;
        var seen = new bool[sim.State.Grid.Count];
        var queue = new Queue<TileId>();
        queue.Enqueue(tile);
        seen[tile.Value] = true;
        while (queue.TryDequeue(out var current))
            foreach (var neighbor in sim.State.Grid.Neighbors(current))
            {
                if (seen[neighbor.Value] || !Carrier(sim, player, neighbor)) continue;
                seen[neighbor.Value] = true;
                if (IsFreshWater(sim, neighbor)) return true;
                queue.Enqueue(neighbor);
            }
        return false;
    }
    private static bool Carrier(Simulation sim, int player, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Owner != player) return false;
        return sim.State.CityAt(sim.State.Grid.HexOf(tile)) is not null
            || (plot.Improvement is { } id && sim.Content.Get<S.ImprovementDef>(id).CarriesIrrigation);
    }
}
