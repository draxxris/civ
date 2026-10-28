using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.World;

/// <summary>Cliff invariants and deterministic beach access. Only clears existing cliffs.</summary>
internal sealed class CliffRules : IMapSanitizer
{
    public void Sanitize(Simulation sim)
    {
        SanitizeAll(sim.State, sim.Content);
        foreach (var player in sim.State.Players)
            if (player.StartingPlot is { } start) EnsureBeach(sim.State, sim.Content, start);
        foreach (var city in sim.State.Cities)
            EnsureBeach(sim.State, sim.Content, sim.State.Grid.TileAt(city.Position));
    }

    internal static void SanitizeAll(GameState state, ContentDb content)
    {
        foreach (var tile in state.Grid.Tiles) ClearInvalidEdges(state, content, tile);
    }

    // Inspect every edge of the changed tile and its neighbours, including stale off-map bits.
    internal static void SanitizeTile(GameState state, ContentDb content, TileId tile)
    {
        ClearInvalidEdges(state, content, tile);
        foreach (var neighbor in state.Grid.Neighbors(tile)) ClearInvalidEdges(state, content, neighbor);
    }

    private static void ClearInvalidEdges(GameState state, ContentDb content, TileId tile)
    {
        state.Plots[tile.Value].CliffEdges &= 63;
        for (int direction = 0; direction < 6; direction++)
        {
            var neighbor = state.Grid.Neighbor(tile, direction);
            if (neighbor is not { } next) { state.SetCliff(tile, direction, false); continue; }
            bool mirrored = (state.Plots[next.Value].CliffEdges & (1 << ((direction + 3) % 6))) != 0;
            bool present = (state.Plots[tile.Value].CliffEdges & (1 << direction)) != 0;
            // Malformed one-sided cliffs are removed, not invented on the missing side.
            if (!present || !mirrored || !LandSeaEdge(content, state.Plots[tile.Value], state.Plots[next.Value]))
                state.SetCliff(tile, direction, false);
        }
    }

    internal static bool LandSeaEdge(ContentDb content, Plot a, Plot b) =>
        !Geography.Water(content, a) && Sea(content, b) || !Geography.Water(content, b) && Sea(content, a);
    internal static bool Sea(ContentDb content, Plot plot) => Geography.Water(content, plot) && !plot.IsLake;

    internal static void EnsureBeach(GameState state, ContentDb content, TileId tile)
    {
        if (tile.Value < 0 || tile.Value >= state.Plots.Length) return;
        if (Geography.Water(content, state.Plots[tile.Value])) return;
        var directions = SeaDirections(state, content, tile).ToArray();
        if (directions.Length == 0 || directions.Any(d => (state.Plots[tile.Value].CliffEdges & (1 << d)) == 0)) return;
        state.SetCliff(tile, directions[0], false);
    }

    internal static IEnumerable<int> SeaDirections(GameState state, ContentDb content, TileId land) =>
        Enumerable.Range(0, 6).Where(d => state.Grid.Neighbor(land, d) is { } next && Sea(content, state.Plots[next.Value]));
}

/// <summary>Generation tuning lives here. Rank smooth spatial noise with a strong relief bonus,
/// then fill a 20% edge budget. Neighbouring high-ranked coast plots form runs, not independent edge rolls.</summary>
internal static class CliffGenerator
{
    internal const int TargetPercent = 20;
    internal const int NoiseScale = 6;
    internal const int NoiseOctaves = 2;
    internal const int ReliefBonus = 400; // Noise range: 0..999; Hills and Peak receive the same bonus.
    internal const int SmallIslandTiles = 4;
    internal const int SmallIslandBeachPercent = 50;

    internal static void Generate(HexGrid grid, Plot[] plots, DeterministicRng rng, ContentDb content)
    {
        var state = new GameState { Map = grid.Spec, Plots = plots };
        foreach (var plot in plots) plot.CliffEdges = 0;
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
                if (remaining == 0) return;
                if (budgets[plot.AreaId] == 0) break;
                if (RiverEndpoint(plot, direction)) continue;
                state.SetCliff(coast.Tile, direction, true);
                budgets[plot.AreaId]--; remaining--;
            }
        }
    }

    // A drainage river shared with sea marks a mouth; keep its whole land plot open.
    private static bool RiverMouth(Plot plot, int[] seaEdges) => seaEdges.Any(d => (plot.RiverEdges & (1 << d)) != 0);
    private static bool RiverEndpoint(Plot plot, int direction) =>
        (plot.RiverEdges & ((1 << ((direction + 5) % 6)) | (1 << direction) | (1 << ((direction + 1) % 6)))) != 0;
}
