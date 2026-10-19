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
        // Cost and parent share one per-search table. No mutable buffers live on the shared service.
        var reached = new Dictionary<TileId, (long Cost, TileId Parent)> { [start] = (0, start) };
        var open = new PriorityQueue<TileId, (long Estimate, long Cost, int Tile)>();
        open.Enqueue(start, (minimumStep * grid.Distance(start, goal), 0, start.Value));
        while (open.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost != reached[current].Cost) continue;
            if (current == goal) return Build(sim, unit, start, goal, reached, priority.Cost);
            foreach (var next in grid.Neighbors(current))
            {
                if (query.CanEnter(current, next, attack && next == goal) is not null) continue;
                long cost = priority.Cost + query.StepCost(current, next);
                if (reached.TryGetValue(next, out var previous) && previous.Cost <= cost) continue;
                reached[next] = (cost, current);
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
        // Regular movement can be halved once. Routes can be cheaper. The minimum over every route definition is never
        // above the cheapest route on the map, so it stays a valid lower bound without scanning every plot per search.
        foreach (var route in sim.Content.Get<Civ.Content.Schema.RouteDef>().All)
            minimum = Math.Min(minimum, Math.Max(1, Math.Min(route.MovementCost, route.FlatMovement)));
        return minimum;
    }

    private static UnitPath Build(Simulation sim, int unitId, TileId start, TileId goal,
        IReadOnlyDictionary<TileId, (long Cost, TileId Parent)> reached, long cost)
    {
        var grid = sim.State.Grid;
        var tiles = new List<TileId>();
        for (var tile = goal; tile != start; tile = reached[tile].Parent) tiles.Add(tile);
        tiles.Reverse();
        var unit = sim.State.UnitById(unitId);
        long remaining = Math.Max(0, unit.MovesLeft);
        int maximum = Math.Max(1, sim.Content.Services.Get<IUnitService>().Single().MaximumMoves(sim, unitId));
        int turn = 1;
        var turns = new List<int>(tiles.Count);
        foreach (var tile in tiles)
        {
            if (remaining == 0) { turn++; remaining = maximum; }
            turns.Add(turn);
            // Like MoveStep, entering a tile can spend more than remains, but never borrows from the next turn.
            long stepCost = reached[tile].Cost - reached[reached[tile].Parent].Cost;
            remaining = Math.Max(0, remaining - stepCost);
        }
        return new(tiles.Select(grid.HexOf).ToList().AsReadOnly(), cost, turns.AsReadOnly());
    }
}
