using Ariel.Content;
using Ariel.Game;

namespace Ariel.Rules.World;

/// <summary>Cliff invariants and deterministic access rules. A cliff stands on the edge between two on-map plots, at
/// least one of them land, is mirrored on both plots, and drops from the higher height to the lower one; water is never
/// the high side. Heights follow terrain and relief, so a flat may stay raised only while a land cliff drops from it.
/// Generation prunes with these rules; a map that breaks them is reported by <see cref="Report"/> and never corrected.</summary>
internal sealed class CliffRules : IMapGeometry
{
    /// <summary>Distance in tiles between the raised sides of two separate cliffs, counting the tiles between them. Two
    /// cliffs need two tiles of raised ground between them so each reads on its own. One tile of low ground between them
    /// is the one exception: two cliffs that drop onto the same low tile face each other across it, which is exactly the
    /// single tile that allowance asks for. Neighbouring raised tiles are one raised mass, so anything closer than two is
    /// the same cliff.</summary>
    internal const int HighGap = 3;

    /// <summary>Direction names in <see cref="Hex.Directions"/> order, for diagnostics.</summary>
    private static readonly string[] Names = ["E", "NE", "NW", "W", "SW", "SE"];

    /// <summary>How far a bare step's slope runs, in world units where a hex has circumradius 1. A slope has to reach the
    /// ground below before it meets another cliff face or another slope, or the two features merge into one lump. The
    /// length is <see cref="Lattice.CliffRamp"/>, the number the terrain descends over.</summary>
    internal const double CliffRampRoom = Lattice.CliffRamp;

    /// <summary>Half the length of a hex edge when a hex has circumradius 1. A slope is as wide as the edge it leaves.</summary>
    private const double HalfEdge = 0.5;

    private static bool Present(GameState state, TileId tile, int direction) =>
        (state.Plots[tile.Value].CliffEdges & (1 << direction)) != 0;

    private static bool Lifted(GameState state, TileId tile, int direction) =>
        (state.CliffLifts(tile) & (1 << direction)) != 0;

    public sbyte HeightOf(Simulation sim, Plot plot) => Geography.HeightOf(sim.Content, plot);

    /// <summary>Every rule the map breaks. Reports each edge once, from the lower-numbered plot that carries it.</summary>
    public IReadOnlyList<string> Report(Simulation sim)
    {
        var problems = new List<string>();
        foreach (var tile in sim.State.Grid.Tiles) ReportTile(sim.State, sim.Content, tile, problems);
        ReportAreas(sim.State, sim.Content, problems);
        foreach (var player in sim.State.Players)
            if (player.StartingPlot is { } start) ReportAccess(sim.State, sim.Content, start, "start", problems);
        ReportSlopes(sim.State, sim.Content, problems);
        foreach (var city in sim.State.Cities)
            ReportAccess(sim.State, sim.Content, sim.State.Grid.TileAt(city.Position), "city", problems);
        return problems;
    }

    /// <summary>One edge seen as a place in the world: its middle, and the unit normal pointing from the high side to the
    /// low side. Hexes have circumradius 1, so two neighbouring centres are √3 apart and an edge is 1 long.</summary>
    private readonly record struct EdgePoint(double MX, double MZ, double NX, double NZ)
    {
        public (double X, double Z) At(double forward, double side) =>
            (MX + NX * forward - NZ * side, MZ + NZ * forward + NX * side);
    }

    private static EdgePoint EdgeBetween(GameState state, TileId high, TileId low)
    {
        var (hx, hz) = state.Grid.HexOf(high).ToPixel(1);
        var (lx, lz) = state.Grid.HexOf(low).ToPixel(1);
        double dx = lx - hx, dz = lz - hz, length = Math.Sqrt(dx * dx + dz * dz);
        return new((hx + lx) / 2, (hz + lz) / 2, dx / length, dz / length);
    }

    private static string Where(GameState state, TileId tile)
    {
        var at = state.Grid.OffsetOf(tile);
        return $"tile ({at.Col}, {at.Row})";
    }

    /// <summary>A plot's height against the ground under it, and every edge it carries.</summary>
    private static void ReportTile(GameState state, ContentDb content, TileId tile, List<string> problems)
    {
        var plot = state.Plots[tile.Value];
        string where = Where(state, tile);
        sbyte implied = Geography.HeightOf(content, plot);
        if (implied == Levels.Flat && Supported(state, content, tile)) implied = Levels.Hills;
        if (plot.Height != implied) problems.Add($"{where}: height {plot.Height} where the ground implies {implied}");
        for (int direction = 0; direction < 6; direction++)
        {
            if (!Present(state, tile, direction)) continue;
            string edge = $"{where} direction {Names[direction]}";
            if (state.Grid.Neighbor(tile, direction) is not { } next)
            {
                problems.Add($"{edge}: cliff on an edge with no neighbour");
                continue;
            }
            if (!Present(state, next, (direction + 3) % 6)) problems.Add($"{edge}: cliff is not mirrored on the other plot");
            if (next.Value < tile.Value) continue; // The other side reports this edge's rules.
            var other = state.Plots[next.Value];
            bool water = Geography.Water(content, plot), otherWater = Geography.Water(content, other);
            if (water && otherWater) { problems.Add($"{edge}: cliff between two water plots"); continue; }
            if (water || otherWater)
            {
                bool waterHigh = water ? plot.Height > other.Height : other.Height > plot.Height;
                if (waterHigh) problems.Add($"{edge}: water on the high side");
                continue;
            }
            if (plot.Height == other.Height) { problems.Add($"{edge}: land cliff with no height step"); continue; }
            var high = plot.Height > other.Height ? plot : other;
            if ((high == plot ? other : plot).Kind == PlotKind.Peak) { problems.Add($"{edge}: peak is the low side of a cliff"); continue; }
            int step = Math.Abs(plot.Height - other.Height);
            if (high.Kind == PlotKind.Peak ? step > 2 : step != 1)
                problems.Add($"{edge}: land cliff steps {step} tiers below a {high.Kind}");
        }
    }

    /// <summary>Areas a cliff has cut in two, coasts without a beach, and terraces standing too close together. The
    /// areas are the land plots reachable from each other, not the stored area ids, so a hand-made map is judged by its
    /// ground rather than by the ids a generator left behind.</summary>
    private static void ReportAreas(GameState state, ContentDb content, List<string> problems)
    {
        var area = new int[state.Plots.Length];
        int next = 0;
        foreach (var start in state.Grid.Tiles)
        {
            if (area[start.Value] != 0 || Geography.Water(content, state.Plots[start.Value])) continue;
            int id = ++next;
            var members = new List<TileId>();
            var queue = new Queue<TileId>();
            area[start.Value] = id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                members.Add(tile);
                for (int direction = 0; direction < 6; direction++)
                {
                    if (state.Grid.Neighbor(tile, direction) is not { } neighbor || area[neighbor.Value] != 0) continue;
                    if (Geography.Water(content, state.Plots[neighbor.Value])) continue;
                    area[neighbor.Value] = id;
                    queue.Enqueue(neighbor);
                }
            }
            ReportArea(state, content, id, members, problems);
        }
    }

    private static void ReportArea(GameState state, ContentDb content, int area, List<TileId> tiles, List<string> problems)
    {
        var reached = new bool[state.Plots.Length];
        Connect(state, content, tiles[0], reached);
        foreach (var tile in tiles)
            if (!reached[tile.Value])
            {
                problems.Add($"{Where(state, tile)}: cliffs cut land area {area} in two");
                break;
            }
        var coasts = tiles.Where(t => SeaDirections(state, content, t).Any()).ToArray();
        int seaEdges = coasts.Sum(t => SeaDirections(state, content, t).Count());
        if (seaEdges > 0)
        {
            int beaches = coasts.Sum(t => SeaDirections(state, content, t).Count(d => !Present(state, t, d)));
            // A small island keeps half its sea edges open, matching the generator's beach budget.
            int least = tiles.Count <= CliffGenerator.SmallIslandTiles ? (seaEdges + 1) / 2 : 1;
            if (beaches < least)
                problems.Add($"{Where(state, coasts[0])}: land area {area} keeps {beaches} of the {least} open sea edges it needs");
        }
        var accepted = new CliffSpacing();
        foreach (var tile in tiles)
            for (int direction = 0; direction < 6; direction++)
            {
                if (!Present(state, tile, direction) || !Lifted(state, tile, direction)) continue;
                if (state.Grid.Neighbor(tile, direction) is not { } below || Geography.Water(content, state.Plots[below.Value])) continue;
                if (!accepted.Fits(state, tile, direction))
                    problems.Add($"{Where(state, tile)} direction {Names[direction]}: terrace stands too close to another one");
                else accepted.Add(state, tile, direction);
            }
    }

    /// <summary>A slope control failure, together with the wall that causes it: clearing that edge is what puts the ground
    /// back. A generator uses the same answer the report prints, so the two can never disagree.</summary>
    internal readonly record struct SlopeProblem(string Message, int High, int ClearTile, int ClearDirection);

    /// <summary>Raised ground needs a way down, and that way down needs room. A raised mass is a group of tiles that are
    /// the high side of a cliff and touch each other; its perimeter is every edge leading off the mass. If every one of
    /// those edges is a cliff, the ground inside is walled in with no slope out of it. Otherwise each bare, land-to-land
    /// step is a slope, and the slope has <see cref="CliffRampRoom"/> of ground to reach the level below before it runs
    /// into another cliff face or another mass's slope.</summary>
    private static void ReportSlopes(GameState state, ContentDb content, List<string> problems)
    {
        foreach (var problem in SlopeProblems(state, content)) problems.Add(problem.Message);
    }

    /// <summary>Whether the slopes and the perimeter of the raised mass at one tile are legal, for a generator that must
    /// refuse a terrace the report would name. Only that mass and the ground within two tiles of it are walked, so the check
    /// is cheap enough to ask for every candidate edge.</summary>
    internal static bool SlopesHold(GameState state, ContentDb content, TileId tile)
    {
        if (state.CliffLifts(tile) == 0) return true;
        var mass = new HashSet<int> { tile.Value };
        var queue = new Queue<TileId>();
        queue.Enqueue(tile);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in state.Grid.Neighbors(current))
                if (state.CliffLifts(neighbor) != 0 && mass.Add(neighbor.Value)) queue.Enqueue(neighbor);
        }
        bool open = false;
        var ramps = new List<(TileId High, int Direction)>();
        foreach (int value in mass)
        {
            var current = new TileId(value);
            for (int direction = 0; direction < 6; direction++)
            {
                if (state.Grid.Neighbor(current, direction) is not { } next || next.Value < current.Value) continue;
                if (!Step(state, content, current, next, direction, out var high, out int highDirection)) continue;
                if (Present(state, current, direction) || Present(state, next, (direction + 3) % 6)) continue;
                ramps.Add((high, highDirection));
                if (!mass.Contains(next.Value)) open = true;
            }
        }
        // The mass's own walls are its perimeter; a bare edge off the mass is the slope out of it.
        foreach (int value in mass)
        {
            var current = new TileId(value);
            for (int direction = 0; direction < 6; direction++)
            {
                if (state.Grid.Neighbor(current, direction) is not { } next) continue;
                if (mass.Contains(next.Value) || Present(state, current, direction)) continue;
                open = true;
            }
        }
        if (!open) return false;
        foreach (var (high, direction) in ramps)
            if (!SlopeClears(state, content, mass, high, direction)) return false;
        return true;
    }

    /// <summary>The high side of a land-to-land step, or false when the edge is flat, touches water or is absent.</summary>
    private static bool Step(GameState state, ContentDb content, TileId a, TileId b, int direction,
        out TileId high, out int highDirection)
    {
        high = a;
        highDirection = direction;
        var here = state.Plots[a.Value];
        var there = state.Plots[b.Value];
        if (Geography.Water(content, here) || Geography.Water(content, there)) return false;
        if (here.Height == there.Height) return false;
        if (there.Height > here.Height)
        {
            high = b;
            highDirection = (direction + 3) % 6;
        }
        return true;
    }

    /// <summary>Whether one slope reaches the level below before it meets a wall or another mass's slope.</summary>
    private static bool SlopeClears(GameState state, ContentDb content, HashSet<int> mass, TileId high, int direction) =>
        Blocker(state, content, mass.Contains, high, direction) is null;

    /// <summary>The wall or slope that stops a slope before it reaches the ground below, or null when the slope clears.
    /// Only the ground within two tiles of the slope is walked: nothing further can reach its sweep.</summary>
    private static (int Tile, int Direction)? Blocker(GameState state, ContentDb content, Func<int, bool> sameMass,
        TileId high, int direction)
    {
        if (state.Grid.Neighbor(high, direction) is not { } below) return null;
        var edge = EdgeBetween(state, high, below);
        foreach (var tile in state.Grid.Range(high, 2))
            for (int d = 0; d < 6; d++)
            {
                if (state.Grid.Neighbor(tile, d) is not { } next || next.Value < tile.Value) continue;
                if (!Step(state, content, tile, next, d, out var stepHigh, out int stepDirection)) continue;
                if (stepHigh == high) continue; // an edge of the slope's own tile is not in its path
                bool face = Present(state, tile, d) || Present(state, next, (d + 3) % 6);
                if (!face)
                {
                    if (sameMass(stepHigh.Value)) continue;    // one mass shares its own slopes
                    if (state.CliffLifts(stepHigh) == 0) continue; // hills and peaks are not cliff raised
                }
                if (state.Grid.Neighbor(stepHigh, stepDirection) is not { } stepLow) continue;
                if (!Crosses(edge, EdgeBetween(state, stepHigh, stepLow))) continue;
                return face ? (stepHigh.Value, stepDirection) : WallOf(state, stepHigh);
            }
        return null;
    }

    /// <summary>Whether every raised mass within three tiles of a tile is legal. A new wall can block a slope that belongs
    /// to a mass on the other side of it, so a generator has to look past the ground it just raised.</summary>
    internal static bool SlopesNear(GameState state, ContentDb content, TileId tile)
    {
        foreach (var near in state.Grid.Range(tile, 3))
            if (state.CliffLifts(near) != 0 && !SlopesHold(state, content, near)) return false;
        return true;
    }

    /// <summary>The walls whose removal clears every slope finding, for a generator thinning its own cliffs.</summary>
    internal static List<(TileId Tile, int Direction)> SlopeWalls(GameState state, ContentDb content) =>
        SlopeProblems(state, content).Select(p => (new TileId(p.ClearTile), p.ClearDirection)).Distinct().ToList();

    /// <summary>A cliff edge that keeps a tile raised: clearing it lowers the ground and takes the slope out of the way.</summary>
    private static (int Tile, int Direction) WallOf(GameState state, TileId tile)
    {
        byte lifts = state.CliffLifts(tile);
        for (int direction = 0; direction < 6; direction++)
            if ((lifts & (1 << direction)) != 0) return (tile.Value, direction);
        return (tile.Value, 0);
    }

    private static Dictionary<int, List<TileId>> Masses(GameState state)
    {
        var lifted = state.Grid.Tiles.Where(t => state.CliffLifts(t) != 0).Select(t => t.Value).ToHashSet();
        var masses = new Dictionary<int, List<TileId>>();
        var seen = new HashSet<int>();
        foreach (int start in lifted)
        {
            if (!seen.Add(start)) continue;
            var members = new List<TileId>();
            var queue = new Queue<TileId>();
            queue.Enqueue(new TileId(start));
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                members.Add(tile);
                foreach (var neighbor in state.Grid.Neighbors(tile))
                    if (lifted.Contains(neighbor.Value) && seen.Add(neighbor.Value)) queue.Enqueue(neighbor);
            }
            masses[start] = members;
        }
        return masses;
    }

    internal static List<SlopeProblem> SlopeProblems(GameState state, ContentDb content, int only = int.MinValue)
    {
        var problems = new List<SlopeProblem>();
        var lifted = state.Grid.Tiles.Where(t => state.CliffLifts(t) != 0).Select(t => t.Value).ToHashSet();
        var ramps = new List<(TileId High, TileId Low, int Direction)>();
        var faces = new List<(TileId High, TileId Low, int Direction)>();
        foreach (var tile in state.Grid.Tiles)
            for (int direction = 0; direction < 6; direction++)
            {
                if (state.Grid.Neighbor(tile, direction) is not { } next) continue;
                if (next.Value < tile.Value) continue; // each edge once
                var here = state.Plots[tile.Value];
                var there = state.Plots[next.Value];
                if (Geography.Water(content, here) || Geography.Water(content, there)) continue;
                if (here.Height == there.Height) continue;
                var (high, low) = here.Height > there.Height ? (tile, next) : (next, tile);
                int highDirection = high == tile ? direction : (direction + 3) % 6;
                if (Present(state, tile, direction) || Present(state, next, (direction + 3) % 6))
                    faces.Add((high, low, highDirection));
                else if (lifted.Contains(high.Value)) ramps.Add((high, low, highDirection));
            }

        var massOf = new int[state.Plots.Length];
        Array.Fill(massOf, -1);
        var masses = Masses(state);
        int id = 0;
        foreach (var members in masses.Values)
            foreach (var member in members) massOf[member.Value] = ++id;
        // Give every member of one mass the same id.
        foreach (var members in masses.Values)
        {
            int first = massOf[members[0].Value];
            foreach (var member in members) massOf[member.Value] = first;
        }

        foreach (var (start, members) in masses)
        {
            if (only != int.MinValue && start != only && massOf[members[0].Value] != massOf[only]) continue;
            // Enclosure: every edge leading off the mass is a wall.
            (TileId Tile, int Direction)? open = null;
            (TileId Tile, int Direction)? wall = null;
            foreach (var tile in members)
                for (int direction = 0; direction < 6; direction++)
                {
                    if (state.Grid.Neighbor(tile, direction) is not { } next) continue;
                    if (massOf[next.Value] == massOf[tile.Value]) continue;
                    if (Present(state, tile, direction)) wall ??= (tile, direction);
                    else open ??= (tile, direction);
                }
            if (open is null && wall is { } plug)
            {
                problems.Add(new SlopeProblem(
                    $"{Where(state, plug.Tile)}: raised ground is walled in with no edge to slope out of",
                    plug.Tile.Value, plug.Tile.Value, plug.Direction));
                continue;
            }
        }

        foreach (var (high, low, direction) in ramps)
        {
            if (only != int.MinValue && massOf[high.Value] != massOf[only]) continue;
            int mass = massOf[high.Value];
            string where = $"{Where(state, high)} direction {Names[direction]}";
            if (Blocker(state, content, v => massOf[v] == mass, high, direction) is not { } stop) continue;
            var stopTile = new TileId(stop.Tile);
            string what = Present(state, stopTile, stop.Direction) ? "a cliff face" : "another slope";
            problems.Add(new SlopeProblem($"{where}: the slope meets {what} before it reaches the ground below",
                high.Value, stop.Tile, stop.Direction));
        }
        return problems;
    }

    /// <summary>Whether the ground swept by one slope meets the edge another feature stands on. The sweep is as wide as
    /// the edge it leaves and <see cref="CliffRampRoom"/> long.</summary>
    private static bool Crosses(EdgePoint slope, EdgePoint feature)
    {
        // Distance from the feature's middle to the slope's sweep, in the slope's own frame.
        double forward = (feature.MX - slope.MX) * slope.NX + (feature.MZ - slope.MZ) * slope.NZ;
        double side = (feature.MZ - slope.MZ) * slope.NX - (feature.MX - slope.MX) * slope.NZ;
        return forward > -HalfEdge && forward < CliffRampRoom && Math.Abs(side) < HalfEdge + HalfEdge;
    }

    /// <summary>One open sea edge on a coastal tile, one open edge on a tile its cliffs walled in.</summary>
    private static void ReportAccess(GameState state, ContentDb content, TileId tile, string what, List<string> problems)
    {
        if (Geography.Water(content, state.Plots[tile.Value])) return;
        var sea = SeaDirections(state, content, tile).ToArray();
        if (sea.Length > 0 && sea.All(d => Present(state, tile, d)))
            problems.Add($"{Where(state, tile)}: coastal {what} has no open sea edge");
        var blocked = Enumerable.Range(0, 6).Where(d => Present(state, tile, d)).ToArray();
        if (blocked.Length == state.Grid.Neighbors(tile).Count())
            problems.Add($"{Where(state, tile)}: {what} has no open edge");
    }

    /// <summary>Whether every cliff this tile carries still stands on a legal step. A raise can flatten a step or turn
    /// it around, so a generator that raises a tile must not undo the cliffs the tile already carries.</summary>
    internal static bool StepsHold(GameState state, ContentDb content, TileId tile)
    {
        var plot = state.Plots[tile.Value];
        for (int direction = 0; direction < 6; direction++)
        {
            if (!Present(state, tile, direction) || state.Grid.Neighbor(tile, direction) is not { } next) continue;
            var other = state.Plots[next.Value];
            bool water = Geography.Water(content, plot), otherWater = Geography.Water(content, other);
            if (water && otherWater) return false;
            if (water || otherWater)
            {
                if (water ? plot.Height > other.Height : other.Height > plot.Height) return false;
                continue;
            }
            if (plot.Height == other.Height) return false;
            var high = plot.Height > other.Height ? plot : other;
            int step = Math.Abs(plot.Height - other.Height);
            if (high.Kind == PlotKind.Peak ? step > 2 : step != 1) return false;
        }
        return true;
    }

    /// <summary>A tile whose terrain changed keeps the invariant itself, instead of leaving it to a pass: water drops to
    /// its depth and loses its cliffs, and the neighbours the change was holding up settle with it.</summary>
    internal static void AfterTerrainChange(GameState state, ContentDb content, TileId tile)
    {
        if (Geography.Water(content, state.Plots[tile.Value]))
            for (int direction = 0; direction < 6; direction++) state.SetCliff(tile, direction, false);
        Settle(state, content, tile);
        foreach (var neighbor in state.Grid.Neighbors(tile)) Settle(state, content, neighbor);
    }

    /// <summary>Put one plot back to the height its ground implies, keeping a flat raised while a land cliff drops from it.</summary>
    private static void Settle(GameState state, ContentDb content, TileId tile)
    {
        var plot = state.Plots[tile.Value];
        sbyte implied = Geography.HeightOf(content, plot);
        plot.Height = implied == Levels.Flat && Supported(state, content, tile) ? Levels.Hills : implied;
    }

    /// <summary>A flat may stay raised while a terrace drops from it to lower land; a shoreline is not a terrace, so a
    /// cliff down to water never justifies a raise.</summary>
    private static bool Supported(GameState state, ContentDb content, TileId tile)
    {
        for (int direction = 0; direction < 6; direction++)
        {
            if (!Present(state, tile, direction) || state.Grid.Neighbor(tile, direction) is not { } next) continue;
            if (Geography.Water(content, state.Plots[next.Value])) continue;
            if (state.Plots[next.Value].Height < Levels.Hills) return true;
        }
        return false;
    }

    /// <summary>Guarantee a way out of a land tile: one open sea edge for a coastal tile, and at least one open edge for a
    /// tile that cliffs had walled in.</summary>
    internal static void EnsureAccess(GameState state, ContentDb content, TileId tile)
    {
        if (tile.Value < 0 || tile.Value >= state.Plots.Length) return;
        if (Geography.Water(content, state.Plots[tile.Value])) return;
        var sea = Enumerable.Range(0, 6)
            .Where(d => state.Grid.Neighbor(tile, d) is { } next && Sea(content, state.Plots[next.Value])).ToArray();
        if (sea.Length > 0 && sea.All(d => Present(state, tile, d)))
        {
            state.SetCliff(tile, sea[0], false);
            return;
        }
        var blocked = Enumerable.Range(0, 6).Where(d => Present(state, tile, d)).ToArray();
        if (blocked.Length < state.Grid.Neighbors(tile).Count()) return;
        int open = blocked.FirstOrDefault(d => state.Grid.Neighbor(tile, d) is { } next && !Geography.Water(content, state.Plots[next.Value]), blocked[0]);
        state.SetCliff(tile, open, false);
    }

    internal static bool Sea(ContentDb content, Plot plot) => Geography.Water(content, plot) && !plot.IsLake;

    internal static IEnumerable<int> SeaDirections(GameState state, ContentDb content, TileId land) =>
        Enumerable.Range(0, 6).Where(d => state.Grid.Neighbor(land, d) is { } next && Sea(content, state.Plots[next.Value]));

    /// <summary>Enforce walkability and spacing in every land area.</summary>
    internal static bool PruneAllAreas(GameState state, ContentDb content)
    {
        var areas = new Dictionary<int, List<TileId>>();
        foreach (var tile in state.Grid.Tiles)
        {
            if (Geography.Water(content, state.Plots[tile.Value])) continue;
            int area = state.Plots[tile.Value].AreaId;
            if (!areas.TryGetValue(area, out var tiles)) areas[area] = tiles = new List<TileId>();
            tiles.Add(tile);
        }
        bool changed = false;
        foreach (int area in areas.Keys.OrderBy(id => id)) changed |= PruneArea(state, content, areas[area]);
        return changed;
    }

    /// <summary>Keep the area walkable with its cliffs in place, then drop cliffs that stand too close. Opening a cliff only
    /// adds a passable edge and dropping one only removes a cliff, so neither pass undoes the other.</summary>
    internal static bool PruneArea(GameState state, ContentDb content, TileId member) =>
        PruneArea(state, content, LandTiles(state, content, state.Plots[member.Value].AreaId));

    private static List<TileId> LandTiles(GameState state, ContentDb content, int area) =>
        state.Grid.Tiles.Where(t => !Geography.Water(content, state.Plots[t.Value]) && state.Plots[t.Value].AreaId == area).ToList();

    private static bool PruneArea(GameState state, ContentDb content, List<TileId> tiles)
    {
        if (tiles.Count == 0) return false;
        bool changed = false;
        var reached = new bool[state.Plots.Length];
        int count = Connect(state, content, tiles[0], reached);
        while (count < tiles.Count)
        {
            bool opened = false;
            foreach (var tile in tiles)
            {
                if (!reached[tile.Value]) continue;
                for (int direction = 0; direction < 6 && !opened; direction++)
                {
                    if (!Present(state, tile, direction)) continue;
                    if (state.Grid.Neighbor(tile, direction) is not { } next || reached[next.Value]) continue;
                    if (Geography.Water(content, state.Plots[next.Value]) || state.Plots[next.Value].AreaId != state.Plots[tile.Value].AreaId) continue;
                    state.SetCliff(tile, direction, false);
                    // Opening a path can leave the raise the cliff stood on with nothing to hold it up.
                    Settle(state, content, tile);
                    Settle(state, content, next);
                    changed = true;
                    count += Connect(state, content, next, reached);
                    opened = true;
                }
                if (opened) break;
            }
            if (!opened) break; // The area id disagrees with the map; nothing here can reconnect it.
        }
        var accepted = new CliffSpacing();
        foreach (var tile in tiles)
            for (int direction = 0; direction < 6; direction++)
            {
                if (!Present(state, tile, direction) || !Lifted(state, tile, direction)) continue;
                if (state.Grid.Neighbor(tile, direction) is not { } below || Geography.Water(content, state.Plots[below.Value])) continue;
                if (!accepted.Fits(state, tile, direction))
                {
                    state.SetCliff(tile, direction, false);
                    Settle(state, content, tile);
                    Settle(state, content, below);
                    changed = true;
                }
                else accepted.Add(state, tile, direction);
            }
        return changed;
    }

    /// <summary>Mark every land tile of the area reachable from the start without crossing a cliff; return how many tiles
    /// that added.</summary>
    private static int Connect(GameState state, ContentDb content, TileId start, bool[] reached)
    {
        if (reached[start.Value] || Geography.Water(content, state.Plots[start.Value])) return 0;
        int added = 0;
        var queue = new Queue<TileId>();
        reached[start.Value] = true;
        queue.Enqueue(start);
        added++;
        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            for (int direction = 0; direction < 6; direction++)
            {
                if (Present(state, tile, direction)) continue;
                if (state.Grid.Neighbor(tile, direction) is not { } next || reached[next.Value]) continue;
                if (Geography.Water(content, state.Plots[next.Value])) continue;
                reached[next.Value] = true;
                queue.Enqueue(next);
                added++;
            }
        }
        return added;
    }

    /// <summary>Whether a cliff may stand beside every accepted one. Two separate cliffs must leave two tiles of raised
    /// ground between them, so each reads on its own; facing each other across a single low tile is not enough, because
    /// the two slopes then meet in the gap. Neighbouring raised tiles are one raised mass, so a plateau or a long bluff
    /// is allowed: cliffs whose raised sides are joined through raised tiles are faces of one bluff, however long the
    /// rim runs.</summary>
    internal sealed class CliffSpacing
    {
        // Every accepted cliff is filed under the tiles its raised side can crowd, because a cliff is only ever compared
        // with the ones standing within HighGap of its own raised side. Scanning the whole accepted list made thinning
        // quadratic in the number of cliffs, which dominated world generation.
        private readonly Dictionary<int, List<(TileId High, int Direction)>> _near = new();
        // Walk marks for the raised-mass search, so a ridge is followed without allocating a set per comparison.
        private readonly Dictionary<int, int> _seen = new();
        private int _walk;

        internal void Add(GameState state, TileId high, int direction)
        {
            foreach (var tile in state.Grid.Range(high, HighGap - 1)) File(tile, high, direction);
        }

        private void File(TileId tile, TileId high, int direction)
        {
            if (!_near.TryGetValue(tile.Value, out var nearby)) _near[tile.Value] = nearby = new();
            nearby.Add((high, direction));
        }

        internal bool Fits(GameState state, TileId high, int direction)
        {
            if (state.Grid.Neighbor(high, direction) is not { } low) return false;
            if (!_near.TryGetValue(high.Value, out var nearby)) return true;
            foreach (var (otherHigh, _) in nearby)
            {
                if (SameMass(state, high, otherHigh)) continue; // Faces of one bluff, not two cliffs.
                int highDistance = state.Grid.Distance(high, otherHigh);
                if (highDistance <= 1) continue; // One raised mass, not two cliffs.
                if (highDistance >= HighGap) continue;
                return false;
            }
            return true;
        }

        /// <summary>Whether raised ground joins the two tiles, read from the map's own lifts so a generator and a checker
        /// visiting the same map in different orders get the same answer.</summary>
        private bool SameMass(GameState state, TileId a, TileId b)
        {
            if (a == b || state.CliffLifts(a) == 0 || state.CliffLifts(b) == 0) return false;
            int walk = ++_walk;
            var queue = new Queue<TileId>();
            queue.Enqueue(a);
            _seen[a.Value] = walk;
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                foreach (var neighbor in state.Grid.Neighbors(tile))
                {
                    if (neighbor == b) return true;
                    if (_seen.TryGetValue(neighbor.Value, out int mark) && mark == walk) continue;
                    if (state.CliffLifts(neighbor) == 0) continue;
                    _seen[neighbor.Value] = walk;
                    queue.Enqueue(neighbor);
                }
            }
            return false;
        }
    }
}

/// <summary>Generation tuning lives here. Rank smooth spatial noise with a strong relief bonus, then fill a 20% sea-edge
/// budget. Neighbouring high-ranked coasts form runs, not independent edge rolls. Land-to-land cliffs (terraces) are
/// ranked the same way, lifted on their higher side, and thinned until they leave every area walkable and every pair of
/// terraces far enough apart.</summary>
internal static class CliffGenerator
{
    internal const int TargetPercent = 20;
    internal const int NoiseScale = 6;
    internal const int NoiseOctaves = 2;
    internal const int ReliefBonus = 400; // Noise range: 0..999; Hills and Peak receive the same bonus.
    internal const int SmallIslandTiles = 4;
    internal const int SmallIslandBeachPercent = 50;

    /// <summary>Share of the candidate land-to-land edges that may become cliffs before thinning.</summary>
    internal const int TerracePercent = 8;
    internal const int TerraceNoiseScale = 9;
    internal const int TerraceNoiseOctaves = 2;
    /// <summary>Score bonus for a candidate edge that also carries a river, so some rivers run along the foot of a cliff and
    /// pour off its brink as waterfalls.</summary>
    internal const int RiverFallBonus = 70;

    internal static void Generate(HexGrid grid, Plot[] plots, DeterministicRng rng, ContentDb content)
    {
        var state = new GameState { Map = grid.Spec, Plots = plots };
        foreach (var plot in plots) plot.CliffEdges = 0;
        Geography.AssignHeights(content, plots);
        // Keep existing feature, resource and start RNG streams unchanged.
        var (a, b, c, d) = rng.State;
        var cliffRng = DeterministicRng.Restore(a, b, c, d);
        var noise = FractalNoise.Generate(grid, cliffRng, NoiseScale, NoiseOctaves);
        var coasts = grid.Tiles.Where(t => !Geography.Water(content, plots[t.Value]))
            .Select(t => (Tile: t, Edges: CliffRules.SeaDirections(state, content, t).ToArray()))
            .Where(c => c.Edges.Length > 0).ToArray();
        var sizes = plots.Where(p => !Geography.Water(content, p)).GroupBy(p => p.AreaId).ToDictionary(g => g.Key, g => g.Count());
        var budgets = coasts.GroupBy(c => plots[c.Tile.Value].AreaId).ToDictionary(g => g.Key, g =>
        {
            int edges = g.Sum(c => c.Edges.Length);
            return sizes[g.Key] <= SmallIslandTiles ? edges * (100 - SmallIslandBeachPercent) / 100 : edges - 1;
        });
        int remaining = coasts.Sum(c => c.Edges.Length) * TargetPercent / 100;
        foreach (var coast in coasts.OrderByDescending(c => noise[c.Tile.Value] + (plots[c.Tile.Value].Kind == PlotKind.Flat ? 0 : ReliefBonus))
            .ThenBy(c => c.Tile.Value))
        {
            var plot = plots[coast.Tile.Value];
            if (RiverMouth(plot, coast.Edges)) continue;
            foreach (int direction in coast.Edges)
            {
                if (remaining == 0) break;
                if (budgets[plot.AreaId] == 0) break;
                state.SetCliff(coast.Tile, direction, true);
                budgets[plot.AreaId]--; remaining--;
            }
            if (remaining == 0) break;
        }
        Terraces(state, content, cliffRng);
        CliffRules.PruneAllAreas(state, content);
        Legalize(state, content);
    }

    /// <summary>Thin the cliffs the slope control refuses. Walkability is not re-checked: every pass clears walls, and an
    /// open edge never closes, so an area that had a way in keeps it.</summary>
    private static void Legalize(GameState state, ContentDb content)
    {
        for (int pass = 0; pass < 8; pass++)
        {
            bool thinned = false;
            var walls = CliffRules.SlopeWalls(state, content);
            foreach (var (tile, direction) in walls)
            {
                state.SetCliff(tile, direction, false);
                CliffRules.AfterTerrainChange(state, content, tile);
                thinned = true;
            }
            if (!thinned) return;
        }
    }

    // A river reaches the sea at a corner of one of the plot's sea edges, its mouth: a river edge next to a sea edge
    // (or, in a hand-made scene, on it). Keep the whole land plot open, so the mouth is a beach rather than a cliff.
    private static bool RiverMouth(Plot plot, int[] seaEdges) => seaEdges.Any(d =>
        (plot.RiverEdges & ((1 << ((d + 5) % 6)) | (1 << d) | (1 << ((d + 1) % 6)))) != 0);

    /// <summary>Choose land-to-land cliffs by the same noise ranking as the coast, lift the higher side, and thin them until
    /// the cliffs stand far enough apart and every area stays walkable.</summary>
    private static void Terraces(GameState state, ContentDb content, DeterministicRng rng)
    {
        var grid = state.Grid;
        var plots = state.Plots;
        var noise = FractalNoise.Generate(grid, rng, TerraceNoiseScale, TerraceNoiseOctaves);
        var candidates = new List<(TileId Tile, TileId Next, int Direction, int Score)>();
        foreach (var tile in grid.Tiles)
        {
            var plot = plots[tile.Value];
            if (Geography.Water(content, plot)) continue;
            for (int direction = 0; direction < 6; direction++)
            {
                if (grid.Neighbor(tile, direction) is not { } next || next.Value <= tile.Value) continue;
                var other = plots[next.Value];
                if (Geography.Water(content, other) || other.AreaId != plot.AreaId) continue;
                // A river edge may also be a cliff: the river then runs along its foot, and falls where its course leaves the
                // upper ground.
                bool river = (plot.RiverEdges & (1 << direction)) != 0 || (other.RiverEdges & (1 << ((direction + 3) % 6))) != 0;
                candidates.Add((tile, next, direction, noise[tile.Value] + noise[next.Value]
                    + 120 * Math.Max(ReliefRank(plot.Kind), ReliefRank(other.Kind)) + (river ? RiverFallBonus : 0)));
            }
        }
        int remaining = candidates.Count * TerracePercent / 100;
        var accepted = new CliffRules.CliffSpacing();
        foreach (var (tile, next, direction, _) in candidates
            .OrderByDescending(c => c.Score).ThenBy(c => c.Tile.Value).ThenBy(c => c.Direction))
        {
            if (remaining == 0) break;
            // An earlier terrace may have raised ground under this edge, so the step and its high side are decided against
            // the heights as they stand now rather than the ones the edge was scored with.
            bool liftTile = Higher(plots[tile.Value].Height, plots[next.Value].Height,
                noise[tile.Value], noise[next.Value], tile.Value, next.Value);
            var high = liftTile ? tile : next;
            var low = liftTile ? next : tile;
            int highDirection = liftTile ? direction : (direction + 3) % 6;
            if (!TerraceLegal(plots[high.Value], plots[low.Value])) continue;
            if (!accepted.Fits(state, high, highDirection)) continue;
            // Standing the cliff raises the high side, which must leave the cliffs that tile already carries legal.
            sbyte before = plots[high.Value].Height;
            plots[high.Value].Height = (sbyte)Math.Max(before, Math.Min(plots[low.Value].Height + 1, Levels.Peak));
            if (!CliffRules.StepsHold(state, content, high)) { plots[high.Value].Height = before; continue; }
            state.SetCliff(high, highDirection, true);
            // A terrace whose raised ground is walled in, or whose slope runs into a wall, is not worth keeping.
            if (!CliffRules.SlopesNear(state, content, high))
            {
                state.SetCliff(high, highDirection, false);
                plots[high.Value].Height = before;
                continue;
            }
            accepted.Add(state, high, highDirection);
            remaining--;
        }
    }

    /// <summary>Whether an edge may carry a terrace. A peak may stand one or two tiers above any land and is never the low
    /// side; otherwise the heights must step by exactly one tier, which an edge between two flats reaches by lifting one of
    /// them to hills. Two hills, two raised flats or two peaks share a height and carry nothing.</summary>
    private static bool TerraceLegal(Plot high, Plot low)
    {
        if (low.Kind == PlotKind.Peak) return false;
        if (high.Kind == PlotKind.Peak) return low.Height is Levels.Flat or Levels.Hills;
        return high.Height - low.Height == 1
            || (high.Kind == PlotKind.Flat && high.Height == Levels.Flat && low.Height == Levels.Flat);
    }

    private static bool Higher(sbyte height, sbyte otherHeight, int noise, int otherNoise, int id, int otherId)
    {
        if (height != otherHeight) return height > otherHeight;
        if (noise != otherNoise) return noise > otherNoise;
        return id < otherId;
    }

    private static int ReliefRank(PlotKind kind) => kind switch { PlotKind.Peak => 2, PlotKind.Hills => 1, _ => 0 };
}
