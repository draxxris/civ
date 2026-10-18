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
        int range = Range(sim);
        return Blockaders(sim, team).Any(unit => Covers(sim, unit, tile, range));
    }
    public IReadOnlySet<TileId> BlockadedTiles(Simulation sim, int team)
    {
        var tiles = new HashSet<TileId>();
        int range = Range(sim);
        foreach (var unit in Blockaders(sim, team))
            foreach (var tile in sim.State.Grid.Range(sim.State.Grid.TileAt(unit.Position), range))
                if (!tiles.Contains(tile) && Water(sim, sim.State.Plots[tile.Value]) && Covers(sim, unit, tile, range)) tiles.Add(tile);
        return tiles;
    }
    private static int Range(Simulation sim) => Math.Max(0, MilitaryData.Raw(sim, "SHIP_BLOCKADE_RANGE", 3));
    private static IEnumerable<Unit> Blockaders(Simulation sim, int team) => sim.State.Units.Where(unit =>
        sim.State.TeamById(team).Wars.Contains(sim.TeamOf(unit.Owner).Id)
        && MilitaryData.Read(sim, unit).Activity == UnitActivity.Blockade && PositionError(sim, unit) is null);
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
