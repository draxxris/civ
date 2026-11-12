using Ariel.Content;
using Ariel.Game;

namespace Ariel.Rules.World;

/// <summary>Shared world pipeline with selectable land silhouettes and separate relief and moisture fields.
/// The default Continental layout retains polar fractal land and the configured water percentage.</summary>
internal sealed class ContinentsGenerator(string id = "world:continents", string name = "Continental",
    WorldLayout layout = WorldLayout.Continental) : IMapGenerator
{
    public string Id => id;
    public string Name => name;
    public void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content) => Generate(spec, plots, rng, content, 2);
    public void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content, int players)
    {
        using var trace = Ariel.Modding.StartupTrace.Phase("world generation");
        var grid = new HexGrid(spec);
        if (plots.Length != grid.Count) throw new ArgumentException("Plot count does not match the grid");
        var tuning = Geography.Tuning(content);
        using (Ariel.Modding.StartupTrace.Phase("world terrain and rivers"))
        {
            var (elevation, land) = BuildLand(grid, rng, tuning, layout);
            var moisture = FractalNoise.Generate(grid, rng, tuning.NoiseScale / 2, tuning.NoiseOctaves);
            var relief = FractalNoise.Generate(grid, rng, tuning.NoiseScale / 4, tuning.NoiseOctaves);
            int hills = Percentile(relief, layout == WorldLayout.GreatPlains ? 94 : 100 - tuning.HillsPercent);
            int peaks = layout == WorldLayout.GreatPlains ? int.MaxValue : Percentile(relief, 100 - tuning.PeaksPercent);
            foreach (var tile in grid.Tiles)
                SetTerrain(grid, plots[tile.Value], tile, land[tile.Value], moisture[tile.Value], relief[tile.Value], hills, peaks, tuning);
            AddCoasts(grid, plots, content);
            Geography.AssignAreas(grid, plots, content);
            Geography.AssignHeights(content, plots);
            RiverGenerator.Generate(grid, plots, elevation, rng, content);
            CliffGenerator.Generate(grid, plots, rng, content);
        }
        using (Ariel.Modding.StartupTrace.Phase("world features"))
            Placement.AddFeatures(grid, plots, rng, content);
        using (Ariel.Modding.StartupTrace.Phase("world bonuses"))
            Placement.AddBonuses(grid, plots, rng, content, players);
        using (Ariel.Modding.StartupTrace.Phase("world goodies"))
            Placement.AddGoodies(grid, plots, rng, content);
    }
    public bool[] PreviewLand(MapSpec spec, ulong seed, ContentDb content) =>
        BuildLand(new HexGrid(spec), new DeterministicRng(seed), Geography.Tuning(content), layout).Land;
    /// <summary>The land step of generation: the elevation field, its high-latitude penalty and the layout silhouette.
    /// It draws exactly what <see cref="Generate"/> draws first, so a preview of a seed is that seed's coastline.</summary>
    private static (int[] Elevation, bool[] Land) BuildLand(HexGrid grid, DeterministicRng rng,
        Ariel.Content.Schema.HexOverridesDef tuning, WorldLayout layout)
    {
        var elevation = FractalNoise.Generate(grid, rng, tuning.NoiseScale, tuning.NoiseOctaves);
        foreach (var tile in grid.Tiles) elevation[tile.Value] -= Geography.Latitude(grid, tile) * tuning.PolarPenalty / 90;
        return (elevation, LandLayouts.Create(grid, elevation, tuning.WaterPercent, layout));
    }
    private static int Percentile(int[] values, int percent) => values.Order().ElementAt(Math.Min(values.Length - 1, values.Length * percent / 100));
    private static void SetTerrain(HexGrid grid, Plot plot, TileId tile, bool land, int moisture, int relief,
        int hills, int peaks, Ariel.Content.Schema.HexOverridesDef tuning)
    {
        plot.Kind = PlotKind.Flat;
        plot.Terrain = land ? Climate(Geography.Latitude(grid, tile), moisture, tuning) : "world:ocean";
        if (!land) return;
        if (relief >= peaks)
        {
            // Keep peak relief for capability/yield rules, but mountains have no underlying biome.
            plot.Kind = PlotKind.Peak;
            plot.Terrain = "world:peak";
        }
        else if (relief >= hills) plot.Kind = PlotKind.Hills;
    }
    private static string Climate(int latitude, int moisture, Ariel.Content.Schema.HexOverridesDef tuning)
    {
        if (latitude >= tuning.SnowLatitude) return "world:snow";
        if (latitude >= tuning.TundraLatitude) return "world:tundra";
        if (latitude >= tuning.DesertMinLatitude && latitude <= tuning.DesertMaxLatitude && moisture < tuning.DesertMoisture) return "world:desert";
        if (moisture < tuning.PlainsMoisture) return "world:plains";
        return moisture >= tuning.MarshMoisture ? "world:marsh" : "world:grass";
    }
    private static void AddCoasts(HexGrid grid, Plot[] plots, ContentDb content)
    {
        foreach (var tile in grid.Tiles)
            if (Geography.Water(content, plots[tile.Value]) && grid.Neighbors(tile).Any(n => !Geography.Water(content, plots[n.Value])))
                plots[tile.Value].Terrain = "world:coast";
    }
}

/// <summary>Rivers run along tile edges from corner to corner, down the seams between tiles. A priority flood from every
/// coast corner over the edges between two land tiles gives each corner one downstream neighbour, so the flow cannot form
/// a cycle; a source on a hill or peak follows it to the sea, a lake, or a river already drawn. A river never runs along
/// a shore: it ends at the first corner that touches water, its mouth. A river is kept only when it is at least
/// <see cref="MinEdges"/> edges long and leaves every tile it touches with one unbroken run of at most
/// <see cref="MaxTileEdges"/> river edges, so no river is a stub, wraps around one tile, or splits a tile between two
/// separate runs; a source whose whole course breaks that is moved down the course until the rest fits.</summary>
internal static class RiverGenerator
{
    /// <summary>Fewest edges a river, or a tributary up to where it joins another, may have.</summary>
    internal const int MinEdges = 4;
    /// <summary>Most river edges one tile may carry.</summary>
    internal const int MaxTileEdges = 3;
    // How much higher a corner stands for each hill or peak at it, in elevation-noise units (0..999), so rivers run off
    // the mountains rather than along them.
    private const int HillRise = 150, PeakRise = 300;

    /// <summary>The drainage: for every corner the next corner downstream and the edge to it (tile * 6 + direction), or
    /// -1, and how many edges it lies from its mouth (0 at a mouth, -1 where no water drains it).</summary>
    private sealed record Flow(int[] Down, int[] Edge, int[] Reach);

    internal static void Generate(HexGrid grid, Plot[] plots, int[] elevation, DeterministicRng rng, ContentDb content)
    {
        var corners = new HexCorners(grid);
        var land = plots.Select(p => !Geography.Water(content, p)).ToArray();
        var flow = Drain(grid, corners, plots, land, elevation);
        var state = new GameState { Map = grid.Spec, Plots = plots };
        // Edges of drawn river above each corner along its longest branch, or -1 off the rivers.
        var upstream = Enumerable.Repeat(-1, corners.Count).ToArray();
        var course = new List<int>();
        var masks = new Dictionary<int, int>();
        int oneIn = Geography.Tuning(content).RiverSourceOneIn;
        foreach (var tile in grid.Tiles)
        {
            if (!land[tile.Value] || plots[tile.Value].Kind == PlotKind.Flat) continue;
            if (rng.Next(oneIn) != 0) continue;
            Draw(state, flow, Source(corners, flow, tile), upstream, course, masks);
        }
    }

    private static Flow Drain(HexGrid grid, HexCorners corners, Plot[] plots, bool[] land, int[] elevation)
    {
        var down = Enumerable.Repeat(-1, corners.Count).ToArray();
        var edge = Enumerable.Repeat(-1, corners.Count).ToArray();
        var reach = Enumerable.Repeat(-1, corners.Count).ToArray();
        var queue = new PriorityQueue<int, (int Level, int Corner)>();
        Span<CornerEdge> edges = stackalloc CornerEdge[3];
        for (int corner = 0; corner < corners.Count; corner++)
        {
            if (!corners.IsCorner(corner) || !Wet(corners, land, corner) || !Drains(corners, grid, land, corner)) continue;
            reach[corner] = 0;
            queue.Enqueue(corner, (Height(corners, plots, land, elevation, corner), corner));
        }
        while (queue.TryDequeue(out int corner, out var priority))
        {
            int count = corners.Edges(corner, edges);
            for (int i = 0; i < count; i++)
            {
                var e = edges[i];
                if (reach[e.Other] >= 0 || !Dry(grid, land, e)) continue;
                reach[e.Other] = reach[corner] + 1;
                down[e.Other] = corner;
                edge[e.Other] = e.Tile.Value * 6 + e.Direction;
                queue.Enqueue(e.Other, (Math.Max(priority.Level, Height(corners, plots, land, elevation, e.Other)), e.Other));
            }
        }
        return new(down, edge, reach);
    }

    /// <summary>Whether water stands at a corner.</summary>
    private static bool Wet(HexCorners corners, bool[] land, int corner)
    {
        Span<TileId> tiles = stackalloc TileId[3];
        int count = corners.Tiles(corner, tiles);
        for (int i = 0; i < count; i++)
            if (!land[tiles[i].Value]) return true;
        return false;
    }

    /// <summary>Whether a river may reach a corner along an edge between two land tiles.</summary>
    private static bool Drains(HexCorners corners, HexGrid grid, bool[] land, int corner)
    {
        Span<CornerEdge> edges = stackalloc CornerEdge[3];
        int count = corners.Edges(corner, edges);
        for (int i = 0; i < count; i++)
            if (Dry(grid, land, edges[i])) return true;
        return false;
    }

    /// <summary>Whether land lies on both sides of an edge, so a river may run along it.</summary>
    private static bool Dry(HexGrid grid, bool[] land, CornerEdge edge) =>
        land[edge.Tile.Value] && grid.Neighbor(edge.Tile, edge.Direction) is { } other && land[other.Value];

    /// <summary>A corner's height: the mean elevation of the land at it, raised by its hills and peaks.</summary>
    private static int Height(HexCorners corners, Plot[] plots, bool[] land, int[] elevation, int corner)
    {
        Span<TileId> tiles = stackalloc TileId[3];
        int count = corners.Tiles(corner, tiles), sum = 0, dry = 0;
        for (int i = 0; i < count; i++)
        {
            int tile = tiles[i].Value;
            if (!land[tile]) continue;
            sum += elevation[tile] + plots[tile].Kind switch { PlotKind.Peak => PeakRise, PlotKind.Hills => HillRise, _ => 0 };
            dry++;
        }
        return dry == 0 ? 0 : sum / dry;
    }

    /// <summary>The corner of a tile farthest upstream from its mouth, or -1 when water drains none of them.</summary>
    private static int Source(HexCorners corners, Flow flow, TileId tile)
    {
        int best = -1;
        for (int k = 0; k < 6; k++)
        {
            int corner = corners.Of(tile, k);
            if (flow.Reach[corner] > 0 && (best < 0 || flow.Reach[corner] > flow.Reach[best]
                || (flow.Reach[corner] == flow.Reach[best] && corner < best)))
                best = corner;
        }
        return best;
    }

    /// <summary>Draw the course from a source down to its mouth or to the river it joins, starting as far up it as the
    /// rules allow. Every corner on a drawn river has its whole course below it drawn, so a later river stops there. It
    /// may join another at that river's source, which only lengthens it, or where at least <see cref="MinEdges"/> of it
    /// run above the join, so the joining does not turn that river's head into a stub.</summary>
    private static void Draw(GameState state, Flow flow, int source, int[] upstream, List<int> course, Dictionary<int, int> masks)
    {
        course.Clear();
        int end = source;
        for (; end >= 0 && flow.Reach[end] > 0 && upstream[end] < 0; end = flow.Down[end])
            course.Add(end);
        if (end >= 0 && upstream[end] is > 0 and < MinEdges) return;
        for (int start = 0; course.Count - start >= MinEdges; start++)
        {
            if (!Fits(state, flow, course, start, masks)) continue;
            for (int i = start; i < course.Count; i++)
            {
                int edge = flow.Edge[course[i]];
                state.SetRiver(new TileId(edge / 6), edge % 6, true);
                upstream[course[i]] = i - start;
            }
            // The river below the join is now longer above each of its corners.
            for (int length = course.Count - start, corner = end; corner >= 0 && upstream[corner] < length;
                 corner = flow.Down[corner], length++)
                upstream[corner] = length;
            return;
        }
    }

    /// <summary>Whether drawing the course from <paramref name="start"/> leaves every tile it touches with one unbroken
    /// run of at most <see cref="MaxTileEdges"/> river edges.</summary>
    private static bool Fits(GameState state, Flow flow, List<int> course, int start, Dictionary<int, int> masks)
    {
        masks.Clear();
        for (int i = start; i < course.Count; i++)
        {
            int edge = flow.Edge[course[i]], tile = edge / 6, direction = edge % 6;
            var other = state.Grid.Neighbor(new TileId(tile), direction)!.Value.Value;
            masks[tile] = masks.GetValueOrDefault(tile, state.Plots[tile].RiverEdges) | (1 << direction);
            masks[other] = masks.GetValueOrDefault(other, state.Plots[other].RiverEdges) | (1 << ((direction + 3) % 6));
        }
        foreach (int mask in masks.Values)
            if (System.Numerics.BitOperations.PopCount((uint)mask) > MaxTileEdges || Runs(mask) > 1) return false;
        return true;
    }

    /// <summary>Separate runs of set bits around a six-bit edge mask.</summary>
    internal static int Runs(int mask)
    {
        int runs = 0;
        for (int d = 0; d < 6; d++)
            if ((mask & (1 << d)) != 0 && (mask & (1 << ((d + 5) % 6))) == 0) runs++;
        return runs;
    }
}
