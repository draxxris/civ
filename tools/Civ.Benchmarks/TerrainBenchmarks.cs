using System.Diagnostics;
using Civ.Client;
using Civ.Client.Terrain;
using Civ.Game;
using Civ.Mods.Tests;

/// <summary>CPU terrain workloads. Run in Release with --terrain; generation, content and GPU upload are excluded.</summary>
internal static class TerrainBenchmarks
{
    public static void Run(GameCatalog catalog)
    {
        Console.WriteLine($"Terrain CPU benchmarks; {Environment.ProcessorCount} logical CPUs; k={TerrainMesher.DefaultResolution} "
            + $"(peaks {TerrainMesher.PeakResolution}); seed=42.");
        Console.WriteLine("One warmup, median of five samples (min/max also printed); revealed generated maps; no timing assertions.");
        foreach (string size in new[] { "standard", "huge", "gigantic" })
        {
            var session = new GameSession(GameFactory.CreateAi(catalog.Content, seed: 42, world: $"foundation:worldsize_{size}"));
            foreach (var plot in session.Sim.State.Plots) plot.Revealed.Add(session.Me.TeamId);
            var palette = new TerrainPalette(catalog);
            var snapshot = TerrainSnapshot.From(session, palette.LayerOf, palette.FeatureGroundOf);
            var field = new TerrainField(snapshot, 42);
            var chunks = new TerrainChunks(snapshot.Grid.Spec);
            var planner = new ScatterPlanner(catalog);
            var all = chunks.Chunks.ToArray();
            var meshes = new TerrainMeshData[all.Length];
            var scatter = new IReadOnlyList<ScatterInstance>[all.Length];
            void Mesh(bool parallel, TerrainChunk[] work)
            {
                var resolutions = chunks.Resolutions(snapshot);
                if (parallel) Parallel.ForEach(work, c => meshes[c.Id] = TerrainMesher.Build(c, field, chunks.Resolution(c, resolutions)));
                else foreach (var c in work) meshes[c.Id] = TerrainMesher.Build(c, field, chunks.Resolution(c, resolutions));
            }
            void Scatter(TerrainChunk[] work)
            {
                foreach (var c in work) scatter[c.Id] = planner.Plan(c, snapshot, field, palette);
            }
            Console.WriteLine($"\n{size}: {snapshot.Width}x{snapshot.Height}; {all.Length} chunks");
            Measure("full mesh sequential", () => Mesh(false, all));
            Measure("full mesh parallel", () => Mesh(true, all));
            Measure("full scatter sequential", () => Scatter(all));
            Measure("full mesh parallel + scatter sequential", () => { Mesh(true, all); Scatter(all); });
            Console.WriteLine($"vertices={meshes.Sum(m => m.VertexCount)}, triangles={meshes.Sum(m => m.Indices.Length / 3)}, "
                + $"fine chunks={chunks.Resolutions(snapshot).Count(k => k == TerrainMesher.PeakResolution)}, instances={scatter.Sum(s => s.Count)}");
            // Change an interior tile, then rebuild four genuinely invalidated chunks using the shared field/warp.
            var previous = chunks.Signatures(snapshot);
            int col = snapshot.Width / 2 / TerrainChunks.TileColumns * TerrainChunks.TileColumns;
            int row = (snapshot.Height / 2 + TerrainChunks.NorthMarginUnits) / TerrainChunks.TileRows * TerrainChunks.TileRows - TerrainChunks.NorthMarginUnits;
            int tile = snapshot.Grid.TileAt(col, row).Value;
            snapshot.Feature[tile] = snapshot.Feature[tile] == "world:forest" ? null : "world:forest";
            var dirty = TerrainChunks.DirtyChunks(previous, chunks.Signatures(snapshot)).Take(4).Select(i => all[i]).ToArray();
            if (dirty.Length != 4) throw new InvalidOperationException("The edit must invalidate at least four chunks");
            Measure("4 dirty mesh sequential", () => Mesh(false, dirty));
            Measure("4 dirty mesh parallel", () => Mesh(true, dirty));
            Measure("4 dirty scatter sequential", () => Scatter(dirty));
            Measure("4 dirty mesh parallel + scatter sequential", () => { Mesh(true, dirty); Scatter(dirty); });
            GC.KeepAlive(meshes);
            GC.KeepAlive(scatter);
        }
    }

    private static void Measure(string name, Action action)
    {
        action();
        var samples = new double[5];
        for (int i = 0; i < samples.Length; i++)
        {
            var clock = Stopwatch.StartNew();
            action();
            samples[i] = clock.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        Console.WriteLine($"{name}: {samples[2]:F2} ms (min {samples[0]:F2}, max {samples[^1]:F2})");
    }
}
