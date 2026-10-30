using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.World;

/// <summary>Cliff invariants and deterministic access rules. A cliff stands on the edge between two on-map plots, at
/// least one of them land, is mirrored on both plots, and drops from the higher height to the lower one; water is never
/// the high side. Heights follow terrain and relief, so the pass also drops a raise that no cliff still justifies. Only
/// clears what is illegal and reconnects an area a cliff had severed.</summary>
internal sealed class CliffRules : IMapSanitizer
{
    /// <summary>Distance in tiles between the raised sides of two separate cliffs, counting the tiles between them. Two
    /// cliffs need two tiles of raised ground between them so each reads on its own. One tile of low ground between them
    /// is the one exception: two cliffs that drop onto the same low tile face each other across it, which is exactly the
    /// single tile that allowance asks for. Neighbouring raised tiles are one raised mass, so anything closer than two is
    /// the same cliff.</summary>
    internal const int HighGap = 3;

    private static bool Present(GameState state, TileId tile, int direction) =>
        (state.Plots[tile.Value].CliffEdges & (1 << direction)) != 0;

    private static bool Lifted(GameState state, TileId tile, int direction) =>
        (state.CliffLifts(tile) & (1 << direction)) != 0;

    public void Sanitize(Simulation sim)
    {
        using var trace = Ariel.Modding.StartupTrace.Phase("map sanitize");
        SanitizeAll(sim.State, sim.Content);
        foreach (var player in sim.State.Players)
            if (player.StartingPlot is { } start) EnsureAccess(sim.State, sim.Content, start);
        foreach (var city in sim.State.Cities)
            EnsureAccess(sim.State, sim.Content, sim.State.Grid.TileAt(city.Position));
    }

    internal static void SanitizeAll(GameState state, ContentDb content)
    {
        // Clearing an illegal edge can strand a height a cliff had raised, and lowering a height can make another edge
        // illegal, so the repairs repeat until neither has anything left to do. Pruning runs once: it only removes
        // cliffs, which can only uncrowd the map and open paths, so it never needs a second look.
        for (int pass = 0; pass < 4; pass++)
        {
            bool changed = ClearInvalidEdges(state, content);
            changed |= RepairHeights(state, content);
            if (pass == 0) { PruneAllAreas(state, content); continue; }
            if (!changed) return;
        }
    }

    // Inspect every edge of the changed tile and its neighbours, including stale off-map bits.
    internal static void SanitizeTile(GameState state, ContentDb content, TileId tile)
    {
        var neighborhood = new List<TileId> { tile };
        neighborhood.AddRange(state.Grid.Neighbors(tile));
        for (int pass = 0; pass < 4; pass++)
        {
            bool changed = false;
            foreach (var member in neighborhood) changed |= ClearInvalidEdges(state, content, member);
            changed |= RepairHeights(state, content, neighborhood);
            if (pass == 0) { PruneArea(state, content, tile); continue; }
            if (!changed) return;
        }
    }

    private static bool ClearInvalidEdges(GameState state, ContentDb content)
    {
        bool changed = false;
        foreach (var tile in state.Grid.Tiles) changed |= ClearInvalidEdges(state, content, tile);
        return changed;
    }

    private static bool ClearInvalidEdges(GameState state, ContentDb content, TileId tile)
    {
        var plot = state.Plots[tile.Value];
        bool changed = false;
        if (plot.CliffEdges > 63) { plot.CliffEdges &= 63; changed = true; }
        for (int direction = 0; direction < 6; direction++)
        {
            if (!Present(state, tile, direction) || Legal(state, content, tile, direction)) continue;
            state.SetCliff(tile, direction, false);
            changed = true;
        }
        return changed;
    }

    /// <summary>Whether the cliff bit on this edge may stand. The edge must be on the map and carried by both plots, water
    /// may only be the low side, and land to land must step exactly one tier, or two below a peak.</summary>
    private static bool Legal(GameState state, ContentDb content, TileId tile, int direction)
    {
        if (state.Grid.Neighbor(tile, direction) is not { } next) return false;
        // A one-sided cliff is malformed; the missing side is not invented.
        if (!Present(state, next, (direction + 3) % 6)) return false;
        bool water = Geography.Water(content, state.Plots[tile.Value]);
        bool otherWater = Geography.Water(content, state.Plots[next.Value]);
        if (water && otherWater) return false;
        if (water != otherWater) return true; // A shoreline takes any land height, and water is always the low side.
        int height = state.Plots[tile.Value].Height, otherHeight = state.Plots[next.Value].Height;
        if (height == otherHeight) return false;
        var high = height > otherHeight ? state.Plots[tile.Value] : state.Plots[next.Value];
        int step = Math.Abs(height - otherHeight);
        return high.Kind == PlotKind.Peak ? step <= 2 : step == 1;
    }

    /// <summary>Bring every height back to what its terrain, lake flag and relief imply, keeping a flat at hills height
    /// only while a cliff still drops away from it. Water always holds its depth and a peak is never raised.</summary>
    private static bool RepairHeights(GameState state, ContentDb content)
    {
        bool changed = false;
        foreach (var tile in state.Grid.Tiles) changed |= RepairHeight(state, content, tile);
        return changed;
    }

    private static bool RepairHeights(GameState state, ContentDb content, IEnumerable<TileId> tiles)
    {
        bool changed = false;
        foreach (var tile in tiles) changed |= RepairHeight(state, content, tile);
        return changed;
    }

    private static bool RepairHeight(GameState state, ContentDb content, TileId tile)
    {
        var plot = state.Plots[tile.Value];
        sbyte wanted = Geography.HeightOf(content, plot);
        if (wanted == Levels.Flat && Supported(state, content, tile)) wanted = Levels.Hills;
        if (plot.Height == wanted) return false;
        plot.Height = wanted;
        return true;
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
                if (!accepted.Fits(state, tile, direction)) { state.SetCliff(tile, direction, false); changed = true; }
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
    /// Neighbouring raised tiles are one raised mass, so a plateau of any shape is allowed.</summary>
    internal sealed class CliffSpacing
    {
        // Every accepted cliff is filed under the tiles its raised side can crowd, because a cliff is only ever compared
        // with the ones standing within HighGap of its own raised side. Scanning the whole accepted list made thinning
        // quadratic in the number of cliffs, which dominated world generation.
        private readonly Dictionary<int, List<(TileId High, int Direction)>> _near = new();

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
                int highDistance = state.Grid.Distance(high, otherHigh);
                if (highDistance <= 1) continue; // One raised mass, not two cliffs.
                if (highDistance >= HighGap) continue;
                // Two cliffs that drop onto the same low tile face each other across one tile of low ground.
                if (highDistance == 2 && state.Grid.Neighbor(otherHigh, otherDirection) == low) continue;
                return false;
            }
            return true;
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
