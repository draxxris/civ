using Ariel.Game;

namespace Ariel.Rules.Military;

internal sealed class PathfindingService(MovementService movement) : IPathfindingService
{
    public UnitPath? FindPath(Simulation sim, int unit, Hex destination, bool attack = false) =>
        FindPath(sim, unit, destination, attack, out _);

    public UnitPath? FindPath(Simulation sim, int unit, Hex destination, bool attack, out IReadOnlySet<TileId>? reach)
    {
        reach = null;
        var grid = sim.State.Grid;
        if (!grid.TryTile(destination, out var goal)) return null;
        var start = grid.TileAt(sim.State.UnitById(unit).Position);
        if (SeparateLandAreas(sim, unit, start, goal)) return null;
        long minimumStep = MinimumStep(sim);
        using var search = PathSearch.Begin(grid.Count);
        var query = movement.Query(sim, unit, search.Plots);
        search.Reach(start, 0, start);
        var open = search.Open;
        open.Enqueue(start, (minimumStep * grid.Distance(start, goal), 0, start.Value));
        while (open.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost != search.Cost(current)) continue;
            if (current == goal) return Build(sim, unit, start, goal, search, priority.Cost);
            foreach (var next in grid.Neighbors(current))
            {
                if (!query.CanStep(current, next, attack && next == goal)) continue;
                long cost = priority.Cost + query.StepCost(current, next);
                if (search.Reached(next) && search.Cost(next) <= cost) continue;
                search.Reach(next, cost, current);
                // A lower bound on actual sub-move cost keeps A* admissible without degenerating into Dijkstra.
                open.Enqueue(next, (cost + minimumStep * grid.Distance(next, goal), cost, next.Value));
            }
        }
        // The search ran out of tiles. Without an attack the goal changes no step, so what it reached is everything the
        // unit can reach.
        if (!attack) reach = new HashSet<TileId>(search.Touched);
        return null;
    }
    private static bool SeparateLandAreas(Simulation sim, int unit, TileId start, TileId goal)
    {
        var def = sim.Content.Get<Ariel.Content.Schema.UnitDef>(sim.State.UnitById(unit).Type);
        return def.Domain == "military:domain_land" && !def.CanMoveAllTerrain
            && sim.State.Plots[start.Value].AreaId != sim.State.Plots[goal.Value].AreaId;
    }

    private static int MinimumStep(Simulation sim)
    {
        int minimum = Math.Max(1, sim.Content.Get<Ariel.Content.Schema.RuleConstantsDef>().All.Single().Values.MoveDenominator / 2);
        // Regular movement can be halved once. Routes can be cheaper. The minimum over every route definition is never
        // above the cheapest route on the map, so it stays a valid lower bound without scanning every plot per search.
        foreach (var route in sim.Content.Get<Ariel.Content.Schema.RouteDef>().All)
            minimum = Math.Min(minimum, Math.Max(1, Math.Min(route.MovementCost, route.FlatMovement)));
        return minimum;
    }

    private static UnitPath Build(Simulation sim, int unitId, TileId start, TileId goal, PathSearch reached, long cost)
    {
        var grid = sim.State.Grid;
        var tiles = new List<TileId>();
        for (var tile = goal; tile != start; tile = reached.Parent(tile)) tiles.Add(tile);
        tiles.Reverse();
        var unit = sim.State.UnitById(unitId);
        long remaining = Math.Max(0, unit.MovesLeft);
        int maximum = Math.Max(1, sim.Rules().Units.MaximumMoves(sim, unitId));
        int turn = 1;
        var turns = new List<int>(tiles.Count);
        var steps = new List<Hex>(tiles.Count);
        foreach (var tile in tiles)
        {
            if (remaining == 0) { turn++; remaining = maximum; }
            turns.Add(turn);
            steps.Add(grid.HexOf(tile));
            // Like MoveStep, entering a tile can spend more than remains, but never borrows from the next turn.
            long stepCost = reached.Cost(tile) - reached.Cost(reached.Parent(tile));
            remaining = Math.Max(0, remaining - stepCost);
        }
        return new(steps.AsReadOnly(), cost, turns.AsReadOnly());
    }
}

/// <summary>One path search's table of reached tiles (cost and parent), its open set and its movement query's plot
/// facts. Searches run thousands of times a turn, on several threads at once, so each thread keeps one set of map-sized
/// buffers and reuses it: a tile counts as reached only when its stamp is this search's, so nothing is cleared between
/// searches. A search begun while another is open on the thread gets its own set. Disposing ends the search.</summary>
internal sealed class PathSearch : IDisposable
{
    [ThreadStatic] private static PathSearch? _spare;
    private long[] _cost = [];
    private int[] _parent = [];
    private int[] _stamp = [];
    private int _generation;
    private bool _open;
    public readonly PriorityQueue<TileId, (long Estimate, long Cost, int Tile)> Open = new();
    /// <summary>Every tile reached, in the order first reached.</summary>
    public readonly List<TileId> Touched = new();
    /// <summary>The search's movement query's plot facts.</summary>
    public readonly PlotFactTable Plots = new();

    public static PathSearch Begin(int tiles)
    {
        var search = _spare is { _open: false } spare ? spare : new PathSearch();
        _spare ??= search;
        search.Reset(tiles);
        return search;
    }

    private void Reset(int tiles)
    {
        _open = true;
        if (_stamp.Length != tiles)
        {
            _cost = new long[tiles]; _parent = new int[tiles]; _stamp = new int[tiles];
            _generation = 0;
        }
        if (_generation == int.MaxValue) { Array.Clear(_stamp); _generation = 0; }
        _generation++;
        Open.Clear();
        Touched.Clear();
        Plots.Begin(tiles);
    }

    public bool Reached(TileId tile) => _stamp[tile.Value] == _generation;
    /// <summary>The cost found so far to <paramref name="tile"/>, which must be reached.</summary>
    public long Cost(TileId tile) => _cost[tile.Value];
    public TileId Parent(TileId tile) => new(_parent[tile.Value]);

    /// <summary>Record the cheapest cost found so far to <paramref name="tile"/> and the tile it was reached from.</summary>
    public void Reach(TileId tile, long cost, TileId parent)
    {
        if (_stamp[tile.Value] != _generation) { _stamp[tile.Value] = _generation; Touched.Add(tile); }
        _cost[tile.Value] = cost;
        _parent[tile.Value] = parent.Value;
    }

    public void Dispose() => _open = false;
}
