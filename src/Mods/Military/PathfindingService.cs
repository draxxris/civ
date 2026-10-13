using Civ.Game;

namespace Civ.Mods.Military;

internal sealed class PathfindingService(IMovementService movement) : IPathfindingService
{
    public UnitPath? FindPath(Simulation sim, int unit, Hex destination, bool attack = false)
    {
        var grid = sim.State.Grid;
        if (!grid.TryTile(destination, out var goal)) return null;
        var start = grid.TileAt(sim.State.UnitById(unit).Position);
        var costs = new Dictionary<TileId, long> { [start] = 0 };
        var parents = new Dictionary<TileId, TileId>();
        var open = new PriorityQueue<TileId, (long Estimate, long Cost, int Tile)>();
        open.Enqueue(start, (grid.Distance(start, goal), 0, start.Value));
        while (open.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost != costs[current]) continue;
            if (current == goal) return Build(grid, start, goal, parents, costs[goal]);
            foreach (var next in grid.Neighbors(current))
            {
                if (movement.CanEnter(sim, unit, current, next, attack && next == goal) is not null) continue;
                long cost = costs[current] + movement.StepCost(sim, unit, current, next);
                if (costs.TryGetValue(next, out var previous) && previous <= cost) continue;
                costs[next] = cost; parents[next] = current;
                // Every legal edge costs at least one sub-move, including fractional routes.
                open.Enqueue(next, (cost + grid.Distance(next, goal), cost, next.Value));
            }
        }
        return null;
    }
    private static UnitPath Build(HexGrid grid, TileId start, TileId goal, IReadOnlyDictionary<TileId, TileId> parents, long cost)
    {
        var steps = new List<Hex>();
        for (var tile = goal; tile != start; tile = parents[tile]) steps.Add(grid.HexOf(tile));
        steps.Reverse(); return new(steps.AsReadOnly(), cost);
    }
}
