using Civ.Content;
using Civ.Game;

namespace Civ.Mods.World;

/// <summary>Continents.py / FractalWorld: polar fractal land, 75% water, separate relief and moisture fields.
/// Integer value-noise replaces the native fractal primitive; climate and adjacency use hex topology.</summary>
internal sealed class ContinentsGenerator : IMapGenerator
{
    public string Id => "world:continents";
    public void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content) => Generate(spec, plots, rng, content, 2);
    public void Generate(MapSpec spec, Plot[] plots, DeterministicRng rng, ContentDb content, int players)
    {
        using var trace = Civ.Modding.StartupTrace.Phase("world generation");
        var grid = new HexGrid(spec);
        if (plots.Length != grid.Count) throw new ArgumentException("Plot count does not match the grid");
        var tuning = Geography.Tuning(content);
        using (Civ.Modding.StartupTrace.Phase("world terrain and rivers"))
        {
            var elevation = FractalNoise.Generate(grid, rng, tuning.NoiseScale, tuning.NoiseOctaves);
            var moisture = FractalNoise.Generate(grid, rng, tuning.NoiseScale / 2, tuning.NoiseOctaves);
            var relief = FractalNoise.Generate(grid, rng, tuning.NoiseScale / 4, tuning.NoiseOctaves);
            foreach (var tile in grid.Tiles) elevation[tile.Value] -= Geography.Latitude(grid, tile) * tuning.PolarPenalty / 90;
            var land = grid.Tiles.OrderByDescending(t => elevation[t.Value]).ThenBy(t => t.Value)
                .Take(grid.Count * (100 - tuning.WaterPercent) / 100).Select(t => t.Value).ToHashSet();
            int hills = Percentile(relief, 100 - tuning.HillsPercent);
            int peaks = Percentile(relief, 100 - tuning.PeaksPercent);
            foreach (var tile in grid.Tiles)
                SetTerrain(grid, plots[tile.Value], tile, land.Contains(tile.Value), moisture[tile.Value], relief[tile.Value], hills, peaks, tuning);
            AddCoasts(grid, plots, content);
            Geography.AssignAreas(grid, plots, content);
            RiverGenerator.Generate(grid, plots, elevation, rng, content);
        }
        using (Civ.Modding.StartupTrace.Phase("world features"))
            Placement.AddFeatures(grid, plots, rng, content);
        using (Civ.Modding.StartupTrace.Phase("world bonuses"))
            Placement.AddBonuses(grid, plots, rng, content, players);
        using (Civ.Modding.StartupTrace.Phase("world goodies"))
            Placement.AddGoodies(grid, plots, rng, content);
    }
    private static int Percentile(int[] values, int percent) => values.Order().ElementAt(Math.Min(values.Length - 1, values.Length * percent / 100));
    private static void SetTerrain(HexGrid grid, Plot plot, TileId tile, bool land, int moisture, int relief,
        int hills, int peaks, Civ.Content.Schema.HexOverridesDef tuning)
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
    private static string Climate(int latitude, int moisture, Civ.Content.Schema.HexOverridesDef tuning)
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

/// <summary>CvMapGenerator::doRiver idea: drain high ground to water. Priority-flood fills depressions
/// in a temporary drainage surface. Parent order breaks level ties, so flow cannot form a cycle.</summary>
internal static class RiverGenerator
{
    internal static void Generate(HexGrid grid, Plot[] plots, int[] elevation, DeterministicRng rng, ContentDb content)
    {
        var parent = Drainage(grid, plots, elevation, content);
        var state = new GameState { Map = grid.Spec, Plots = plots };
        foreach (var tile in grid.Tiles)
        {
            if (Geography.Water(content, plots[tile.Value]) || plots[tile.Value].Kind == PlotKind.Flat) continue;
            if (rng.Next(Geography.Tuning(content).RiverSourceOneIn) != 0) continue;
            Draw(state, tile, parent, content);
        }
    }
    private static int[] Drainage(HexGrid grid, Plot[] plots, int[] elevation, ContentDb content)
    {
        var parent = Enumerable.Repeat(-1, grid.Count).ToArray();
        var visited = new bool[grid.Count];
        var queue = new PriorityQueue<TileId, (int Height, int Tile)>();
        foreach (var tile in grid.Tiles)
        {
            if (!Geography.Water(content, plots[tile.Value])) continue;
            visited[tile.Value] = true;
            queue.Enqueue(tile, (elevation[tile.Value], tile.Value));
        }
        while (queue.TryDequeue(out var tile, out var priority))
            foreach (var neighbor in grid.Neighbors(tile))
            {
                if (visited[neighbor.Value]) continue;
                visited[neighbor.Value] = true;
                parent[neighbor.Value] = tile.Value;
                queue.Enqueue(neighbor, (Math.Max(priority.Height, elevation[neighbor.Value]), neighbor.Value));
            }
        return parent;
    }
    private static void Draw(GameState state, TileId source, int[] parent, ContentDb content)
    {
        var tile = source;
        while (!Geography.Water(content, state.Plots[tile.Value]) && parent[tile.Value] >= 0)
        {
            var next = new TileId(parent[tile.Value]);
            int direction = Enumerable.Range(0, 6).First(d => state.Grid.Neighbor(tile, d) == next);
            state.SetRiver(tile, direction, true);
            tile = next;
        }
    }
}
