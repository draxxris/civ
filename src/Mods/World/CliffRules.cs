using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.World;

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
        foreach (var city in sim.State.Cities)
            ReportAccess(sim.State, sim.Content, sim.State.Grid.TileAt(city.Position), "city", problems);
        return problems;
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

    /// <summary>Whether a cliff may stand beside every accepted one. Two separate cliffs must leave enough ground between
    /// them that each reads on its own: two tiles of raised ground, or one tile of low ground that both drop onto.
    /// Neighbouring raised tiles are one raised mass, so a plateau or a long bluff is allowed: cliffs whose raised sides
    /// are joined through raised tiles are faces of one bluff, not two features, however long the rim runs.</summary>
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
            foreach (var (otherHigh, otherDirection) in nearby)
            {
                if (SameMass(state, high, otherHigh)) continue; // Faces of one bluff, not two cliffs.
                int highDistance = state.Grid.Distance(high, otherHigh);
                if (highDistance <= 1) continue; // One raised mass, not two cliffs.
                if (highDistance >= HighGap) continue;
                // Two cliffs that drop onto the same low tile face each other across one tile of low ground.
                if (highDistance == 2 && state.Grid.Neighbor(otherHigh, otherDirection) == low) continue;
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
    /// <summary>Score bonus for a candidate edge that also carries a river, so some rivers cross a cliff as waterfalls.</summary>
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
                if (RiverEndpoint(plot, direction)) continue;
                state.SetCliff(coast.Tile, direction, true);
                budgets[plot.AreaId]--; remaining--;
            }
            if (remaining == 0) break;
        }
        Terraces(state, content, cliffRng);
        CliffRules.PruneAllAreas(state, content);
    }

    // A drainage river shared with sea marks a mouth; keep its whole land plot open.
    private static bool RiverMouth(Plot plot, int[] seaEdges) => seaEdges.Any(d => (plot.RiverEdges & (1 << d)) != 0);
    private static bool RiverEndpoint(Plot plot, int direction) =>
        (plot.RiverEdges & ((1 << ((direction + 5) % 6)) | (1 << direction) | (1 << ((direction + 1) % 6)))) != 0;

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
                // A river edge may also be a cliff: the river then pours over the face as a waterfall.
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
