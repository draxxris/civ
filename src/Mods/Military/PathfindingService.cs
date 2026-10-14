using Civ.Game;

namespace Civ.Mods.Military;

internal sealed class PathfindingService(MovementService movement) : IPathfindingService
{
    public UnitPath? FindPath(Simulation sim, int unit, Hex destination, bool attack = false)
    {
        var grid = sim.State.Grid;
        if (!grid.TryTile(destination, out var goal)) return null;
        var start = grid.TileAt(sim.State.UnitById(unit).Position);
        if (SeparateLandAreas(sim, unit, start, goal)) return null;
        long minimumStep = MinimumStep(sim);
        var query = movement.Query(sim, unit);
        var costs = new Dictionary<TileId, long> { [start] = 0 };
        var parents = new Dictionary<TileId, TileId>();
        var open = new PriorityQueue<TileId, (long Estimate, long Cost, int Tile)>();
        open.Enqueue(start, (minimumStep * grid.Distance(start, goal), 0, start.Value));
        while (open.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost != costs[current]) continue;
            if (current == goal) return Build(grid, start, goal, parents, costs[goal]);
            foreach (var next in grid.Neighbors(current))
            {
                if (query.CanEnter(current, next, attack && next == goal) is not null) continue;
                long cost = costs[current] + query.StepCost(current, next);
                if (costs.TryGetValue(next, out var previous) && previous <= cost) continue;
                costs[next] = cost; parents[next] = current;
                // A lower bound on actual sub-move cost keeps A* admissible without degenerating into Dijkstra.
                open.Enqueue(next, (cost + minimumStep * grid.Distance(next, goal), cost, next.Value));
            }
        }
        return null;
    }
    private static bool SeparateLandAreas(Simulation sim, int unit, TileId start, TileId goal)
    {
        var def = sim.Content.Get<Civ.Content.Schema.UnitDef>(sim.State.UnitById(unit).Type);
        return def.Domain == "military:domain_land" && !def.CanMoveAllTerrain
            && sim.State.Plots[start.Value].AreaId != sim.State.Plots[goal.Value].AreaId;
    }

    private static int MinimumStep(Simulation sim)
    {
        int minimum = Math.Max(1, sim.Content.Get<Civ.Content.Schema.RuleConstantsDef>().All.Single().Values.MoveDenominator / 2);
        // Regular movement can be halved once. Routes can be cheaper; include every route currently on the map.
        foreach (var route in sim.State.Plots.Select(p => p.Route).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var def = sim.Content.Get<Civ.Content.Schema.RouteDef>(route);
            minimum = Math.Min(minimum, Math.Max(1, Math.Min(def.MovementCost, def.FlatMovement)));
        }
        return minimum;
    }

    private static UnitPath Build(HexGrid grid, TileId start, TileId goal, IReadOnlyDictionary<TileId, TileId> parents, long cost)
    {
        var steps = new List<Hex>();
        for (var tile = goal; tile != start; tile = parents[tile]) steps.Add(grid.HexOf(tile));
        steps.Reverse(); return new(steps.AsReadOnly(), cost);
    }
}
