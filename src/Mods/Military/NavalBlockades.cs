using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Military;

/// <summary>Derived from live state, so removal, cargo, movement and peace cannot leave stale counters.</summary>
internal sealed class NavalBlockades : INavalBlockadeService
{
    internal static string? PositionError(Simulation sim, Unit unit)
    {
        var def = MilitaryData.Definition(sim, unit);
        var plot = sim.State.PlotAt(unit.Position);
        if (def.Domain != "military:domain_sea" || !def.Pillage) return "Unit cannot blockade";
        if (unit.Health <= 0 || MilitaryData.Read(sim, unit).TransportId is not null) return "Cargo or dead units cannot blockade";
        if (!Water(sim, plot) || plot.IsLake) return "Blockades require non-lake water";
        if (plot.Owner is { } owner && MilitaryData.Friendly(sim, unit.Owner, owner)) return "Cannot blockade team territory";
        return null;
    }
    public bool IsBlockaded(Simulation sim, TileId tile, int team)
    {
        var plot = sim.State.Plots[tile.Value];
        if (!Water(sim, plot)) return false;
        int range = Math.Max(0, MilitaryData.Raw(sim, "SHIP_BLOCKADE_RANGE", 3));
        foreach (var unit in sim.State.Units)
        {
            if (!sim.State.TeamById(team).Wars.Contains(sim.TeamOf(unit.Owner).Id)) continue;
            if (MilitaryData.Read(sim, unit).Activity != UnitActivity.Blockade || PositionError(sim, unit) is not null) continue;
            if (Covers(sim, unit, tile, range)) return true;
        }
        return false;
    }
    private static bool Water(Simulation sim, Plot plot) => sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater;

    // Water-path distance prevents a blockade from crossing an isthmus, even within the same sea area.
    private static bool Covers(Simulation sim, Unit unit, TileId target, int range)
    {
        var origin = sim.State.Grid.TileAt(unit.Position);
        int area = sim.State.Plots[origin.Value].AreaId;
        if (sim.State.Plots[target.Value].AreaId != area || sim.State.Grid.Distance(origin, target) > range) return false;
        var seen = new HashSet<TileId> { origin };
        var queue = new Queue<(TileId Tile, int Distance)>();
        queue.Enqueue((origin, 0));
        while (queue.TryDequeue(out var step))
        {
            if (step.Tile == target) return true;
            if (step.Distance == range) continue;
            foreach (var neighbor in sim.State.Grid.Neighbors(step.Tile))
            {
                var plot = sim.State.Plots[neighbor.Value];
                if (plot.AreaId != area || !Water(sim, plot) || !seen.Add(neighbor)) continue;
                queue.Enqueue((neighbor, step.Distance + 1));
            }
        }
        return false;
    }
}
