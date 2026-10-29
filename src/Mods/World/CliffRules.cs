using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.World;

/// <summary>Cliff invariants and deterministic access rules. A cliff stands on the edge between two on-map plots with at
/// least one land side, is mirrored on both plots, and names one side as lifted (water is never lifted). Only clears
/// existing cliffs and reconnects an area a cliff had severed.</summary>
internal sealed class CliffRules : IMapSanitizer
{
    /// <summary>The distance beyond a cliff tile over which the client eases neighbouring ground up to the lift, in world
    /// units. The generator's spacing rule is derived from it, so the two must agree; a world-generation test ties them.</summary>
    internal const double SpacingRamp = 1.6;

    /// <summary>Minimum distance in tiles between the lifted tiles of two cliffs that face the same way. The lift covers
    /// the lifted tile and then ramps down, so the ground between two cliffs must return to its normal height before the
    /// next one starts: the lift and ramp in tiles, rounded up, plus a tile of margin and the lifted tile at the far end.</summary>
    internal static int SameFacingGap => (int)Math.Ceiling((1 + SpacingRamp) / Math.Sqrt(3)) + 2;

    /// <summary>Minimum distance between two cliffs whose raised sides face each other: their lifts lean apart, so only the
    /// valley width matters.</summary>
    internal static int FacingGap => Math.Max(2, SameFacingGap - 1);

    private static bool Present(GameState state, TileId tile, int direction) =>
        (state.Plots[tile.Value].CliffEdges & (1 << direction)) != 0;

    private static bool Lifted(GameState state, TileId tile, int direction) =>
        (state.Plots[tile.Value].CliffLiftEdges & (1 << direction)) != 0;

    public void Sanitize(Simulation sim)
    {
        SanitizeAll(sim.State, sim.Content);
        foreach (var player in sim.State.Players)
            if (player.StartingPlot is { } start) EnsureAccess(sim.State, sim.Content, start);
        foreach (var city in sim.State.Cities)
            EnsureAccess(sim.State, sim.Content, sim.State.Grid.TileAt(city.Position));
    }

    internal static void SanitizeAll(GameState state, ContentDb content)
    {
        foreach (var tile in state.Grid.Tiles) ClearInvalidEdges(state, content, tile);
        PruneAllAreas(state, content);
    }

    // Inspect every edge of the changed tile and its neighbours, including stale off-map bits.
    internal static void SanitizeTile(GameState state, ContentDb content, TileId tile)
    {
        ClearInvalidEdges(state, content, tile);
        foreach (var neighbor in state.Grid.Neighbors(tile)) ClearInvalidEdges(state, content, neighbor);
        PruneArea(state, content, tile);
    }

    private static void ClearInvalidEdges(GameState state, ContentDb content, TileId tile)
    {
        var plot = state.Plots[tile.Value];
        plot.CliffEdges &= 63;
        plot.CliffLiftEdges &= 63;
        for (int direction = 0; direction < 6; direction++)
        {
            if (state.Grid.Neighbor(tile, direction) is not { } next) { state.SetCliff(tile, direction, false); continue; }
            int mirror = (direction + 3) % 6;
            bool present = Present(state, tile, direction), mirrored = Present(state, next, mirror);
            // Malformed one-sided cliffs are removed, not invented on the missing side.
            if (!present || !mirrored) { state.SetCliff(tile, direction, false); continue; }
            bool water = Geography.Water(content, plot), otherWater = Geography.Water(content, state.Plots[next.Value]);
            if (water && otherWater) { state.SetCliff(tile, direction, false); continue; }
            bool lifted = Lifted(state, tile, direction), otherLifted = Lifted(state, next, mirror);
            if (water != otherWater)
            {
                // Water never stands above land; older saves carry no lifted side at all.
                if (water) state.SetCliff(next, mirror, true);
                else state.SetCliff(tile, direction, true);
                continue;
            }
            // Land on both sides: exactly one side must be lifted, and a cliff with no lifted side cannot say which.
            if (lifted == otherLifted) state.SetCliff(tile, direction, false);
        }
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
    internal static void PruneAllAreas(GameState state, ContentDb content)
    {
        var areas = new Dictionary<int, List<TileId>>();
        foreach (var tile in state.Grid.Tiles)
        {
            if (Geography.Water(content, state.Plots[tile.Value])) continue;
            int area = state.Plots[tile.Value].AreaId;
            if (!areas.TryGetValue(area, out var tiles)) areas[area] = tiles = new List<TileId>();
            tiles.Add(tile);
        }
        foreach (int area in areas.Keys.OrderBy(id => id)) PruneArea(state, content, areas[area]);
    }

    /// <summary>Keep the area walkable with its cliffs in place, then drop cliffs that stand too close in line. Opening a
    /// cliff only adds a passable edge and dropping one only removes a cliff, so neither pass undoes the other.</summary>
    internal static void PruneArea(GameState state, ContentDb content, TileId member) =>
        PruneArea(state, content, LandTiles(state, content, state.Plots[member.Value].AreaId));

    private static List<TileId> LandTiles(GameState state, ContentDb content, int area) =>
        state.Grid.Tiles.Where(t => !Geography.Water(content, state.Plots[t.Value]) && state.Plots[t.Value].AreaId == area).ToList();

    private static void PruneArea(GameState state, ContentDb content, List<TileId> tiles)
    {
        if (tiles.Count == 0) return;
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
                    count += Connect(state, content, next, reached);
                    opened = true;
                }
                if (opened) break;
            }
            if (!opened) break; // The area id disagrees with the map; nothing here can reconnect it.
        }
        var accepted = new List<(TileId Lifted, int Direction)>();
        foreach (var tile in tiles)
            for (int direction = 0; direction < 6; direction++)
            {
                if (!Present(state, tile, direction) || !Lifted(state, tile, direction)) continue;
                if (state.Grid.Neighbor(tile, direction) is not { } below || Geography.Water(content, state.Plots[below.Value])) continue;
                if (!SpacingOk(state, tile, direction, accepted)) state.SetCliff(tile, direction, false);
                else accepted.Add((tile, direction));
            }
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

    /// <summary>Whether a cliff may stand beside every accepted one. Two cliffs in line must leave the ground between them
    /// enough tiles to fall back to its normal height; two facing each other lift opposite ground and may stand closer.
    /// Neighbouring lifted tiles are one raised mass, so a plateau of any shape is allowed.</summary>
    private static bool SpacingOk(GameState state, TileId lifted, int direction, List<(TileId Lifted, int Direction)> accepted)
    {
        foreach (var (other, otherDirection) in accepted)
        {
            int distance = state.Grid.Distance(lifted, other);
            if (distance <= 1) continue;
            int steps = Math.Abs(direction - otherDirection);
            steps = Math.Min(steps, 6 - steps);
            int gap = steps <= 1 ? SameFacingGap : steps == 3 ? FacingGap : 0;
            if (gap > 0 && distance < gap) return false;
        }
        return true;
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
        foreach (var plot in plots) { plot.CliffEdges = 0; plot.CliffLiftEdges = 0; }
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
        var candidates = new List<(TileId Lifted, TileId Below, int Direction, int Score)>();
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
                bool liftTile = Higher(plot, other, noise[tile.Value], noise[next.Value], tile.Value, next.Value);
                var lifted = liftTile ? tile : next;
                var below = liftTile ? next : tile;
                int liftedDirection = liftTile ? direction : (direction + 3) % 6;
                candidates.Add((lifted, below, liftedDirection,
                    noise[tile.Value] + noise[next.Value] + 120 * ReliefRank(plots[lifted.Value].Kind) + (river ? RiverFallBonus : 0)));
            }
        }
        int remaining = candidates.Count * TerracePercent / 100;
        var accepted = new List<(TileId Lifted, int Direction)>();
        foreach (var (lifted, below, direction, _) in candidates
            .OrderByDescending(c => c.Score).ThenBy(c => c.Lifted.Value).ThenBy(c => c.Direction))
        {
            if (remaining == 0) break;
            if (!SpacingOk(state, lifted, direction, accepted)) continue;
            state.SetCliff(lifted, direction, true);
            accepted.Add((lifted, direction));
            remaining--;
        }
    }

    private static bool Higher(Plot a, Plot b, int noiseA, int noiseB, int idA, int idB)
    {
        int rankA = ReliefRank(a.Kind), rankB = ReliefRank(b.Kind);
        if (rankA != rankB) return rankA > rankB;
        if (noiseA != noiseB) return noiseA > noiseB;
        return idA < idB;
    }

    private static int ReliefRank(PlotKind kind) => kind switch { PlotKind.Peak => 2, PlotKind.Hills => 1, _ => 0 };

    private static bool SpacingOk(GameState state, TileId lifted, int direction, List<(TileId Lifted, int Direction)> accepted)
    {
        foreach (var (other, otherDirection) in accepted)
        {
            int distance = state.Grid.Distance(lifted, other);
            if (distance <= 1) continue;
            int steps = Math.Abs(direction - otherDirection);
            steps = Math.Min(steps, 6 - steps);
            int gap = steps <= 1 ? CliffRules.SameFacingGap : steps == 3 ? CliffRules.FacingGap : 0;
            if (gap > 0 && distance < gap) return false;
        }
        return true;
    }
}
