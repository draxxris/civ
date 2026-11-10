using System.Globalization;
using System.Text.Json;
using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.MeshPlot;

/// <summary>Write the meshes the game builds for one region of a capture scenario as JSON: the terrain lattice, the river
/// strips, the waterfall water, the river courses and the falls, with the hex tiles for orientation. The world is set up
/// exactly as <c>mise run shot -- --scenario</c> sets it up (a scene-sized world, the same seed), so the triangles are the
/// ones on screen. tools/shots/mesh_plot.py draws the file; <c>mise run shot:mesh</c> runs both.</summary>
public static class Program
{
    private const string Usage = """
        usage: Ariel.MeshPlot <scenario.json> --out <file.json> [--focus COL,ROW] [--fall] [--radius TILES] [--seed N]

          --focus COL,ROW  tile at the centre, in the scenario's own coordinates (the poses' focusTile); the scenario's
                           focus by default
          --fall           centre on the brink of the waterfall nearest that tile instead of on the tile
          --radius TILES   half the width of the square region, in tiles (default 1.5)
          --seed N         game seed, as `mise run shot -- --seed` (default 1, the capture default)
        """;

    private static readonly double Sqrt3 = Math.Sqrt(3);

    public static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(Usage);
            return 0;
        }
        if (!TryParse(args, out var options, out string? error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return 2;
        }
        try
        {
            Write(options);
            return 0;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or JsonException)
        {
            Console.Error.WriteLine($"{options.Scenario}: {e.Message}");
            return 1;
        }
    }

    private static void Write(Options options)
    {
        var catalog = new GameCatalog(Discover());
        var scene = DebugScenario.Frame(options.Scenario)
            ?? throw new InvalidDataException("the scenario names no tile, so it has no world of its own to draw");
        // As Main.DebugGame does for a capture: the scene's own world, nobody else in it.
        var game = catalog.DefaultOptions() with
        {
            Seed = options.Seed, Opponents = 0, MapOverride = new MapSpec(scene.Width, scene.Height),
        };
        var session = GameSession.Start(catalog, game);
        var focus = DebugScenario.Apply(session, options.Scenario, sceneWorld: true);
        // As TerrainRenderer does.
        var palette = new TerrainPalette(catalog);
        var snapshot = TerrainSnapshot.From(session, palette.LayerOf, palette.FeatureGroundOf, palette.IsCrater);
        ulong seed = session.Sim.Settings.Seed;
        var field = new TerrainField(snapshot, unchecked((int)(seed ^ (seed >> 32))));
        var grid = snapshot.Grid;

        Hex centre;
        if (options.Focus is var (col, row))
        {
            if (!grid.TryTile(col + scene.ShiftCol, row + scene.ShiftRow, out var id))
                throw new ArgumentException($"--focus {col},{row} is off this {scene.Width} x {scene.Height} scene");
            centre = grid.HexOf(id);
        }
        else centre = focus ?? throw new ArgumentException("the scenario has no focus of its own; pass --focus");
        var (cx, cz) = centre.ToPixel(1);
        if (options.Fall)
        {
            double tx = cx, tz = cz;
            var nearest = field.Rivers.Falls.MinBy(f => (f.X - tx) * (f.X - tx) + (f.Z - tz) * (f.Z - tz))
                ?? throw new ArgumentException("--fall: the scenario has no waterfall");
            var ledge = field.LedgeOf(nearest);
            (cx, cz) = (ledge.X, ledge.Z);
        }
        double reach = options.Radius * Sqrt3;
        var region = new Region(cx - reach, cz - reach, cx + reach, cz + reach);

        var chunks = new TerrainChunks(snapshot.Grid.Spec);
        var resolutions = chunks.Resolutions(snapshot);
        var near = chunks.Chunks.Where(c => c.X0 < region.X1 && c.X1 > region.X0 && c.Z0 < region.Z1 && c.Z1 > region.Z0).ToArray();
        int ResolutionAt(double x, double z) => chunks.ResolutionAt(x, z, resolutions);

        var terrain = new MeshWriter(region);
        var river = new MeshWriter(region);
        var falls = new MeshWriter(region);
        foreach (var chunk in near)
        {
            var ground = TerrainMesher.Build(chunk, field, chunks.Resolution(chunk, resolutions));
            terrain.Add(ground.Positions, ground.Indices, _ => []);
            if (RiverMesher.Build(chunk, field, ResolutionAt) is { } water)
                // Across (±1 at the waterline, beyond it the wet bank) and fade.
                river.Add(water.Positions, water.Indices, i => [water.Lanes[2 * i], water.Info[2 * i]]);
            if (WaterfallMesher.Build(chunk, field, ResolutionAt) is { } fall)
                // Part (0 lip, 1 sheet, 2 pool, 3 veil), foam and fade.
                falls.Add(fall.WaterPositions, fall.WaterIndices, i => [fall.WaterInfo[4 * i], fall.WaterInfo[4 * i + 2], fall.WaterInfo[4 * i + 3]]);
        }

        var tiles = new List<object>();
        for (int i = 0; i < grid.Spec.Width * grid.Spec.Height; i++)
        {
            var hex = grid.HexOf(new TileId(i));
            var (x, z) = hex.ToPixel(1);
            if (x < region.X0 - Sqrt3 || x > region.X1 + Sqrt3 || z < region.Z0 - 2 || z > region.Z1 + 2) continue;
            var (c, r) = grid.OffsetOf(new TileId(i));
            tiles.Add(new { col = c - scene.ShiftCol, row = r - scene.ShiftRow, x = Round(x), z = Round(z), height = snapshot.Heights[i] });
        }

        var courses = field.Rivers.Curves
            .Where(c => Enumerable.Range(0, c.Count).Any(i => region.Holds(c.X[i], c.Z[i])))
            .Select(c => new
            {
                points = Enumerable.Range(0, c.Count).Select(i => new[] { Round(c.X[i]), Round(c.Z[i]) }).ToArray(),
                falls = c.Fall != null, feeds = c.Feeds != null,
            }).ToArray();
        var lips = field.Rivers.Falls.Where(f => region.Holds(f.X, f.Z)).Select(f =>
        {
            var ledge = field.LedgeOf(f);
            // The brink runs across the fall's direction through the ledge's origin, the lip's width wide.
            double ax = -f.DirectionZ, az = f.DirectionX;
            return new
            {
                x = Round(f.X), z = Round(f.Z), dx = Round(f.DirectionX), dz = Round(f.DirectionZ), course = f.Course.ToString(),
                brink = new[]
                {
                    new[] { Round(ledge.X - ax * f.HalfWidth), Round(ledge.Z - az * f.HalfWidth) },
                    new[] { Round(ledge.X + ax * f.HalfWidth), Round(ledge.Z + az * f.HalfWidth) },
                },
            };
        }).ToArray();

        var document = new
        {
            scenario = Path.GetFileName(options.Scenario),
            focus = new { col = options.Focus?.Col, row = options.Focus?.Row },
            seed = options.Seed,
            region = new[] { Round(region.X0), Round(region.Z0), Round(region.X1), Round(region.Z1) },
            tiles, terrain = terrain.Result(), river = river.Result(), falls = falls.Result(), courses, lips,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Out))!);
        File.WriteAllText(options.Out, JsonSerializer.Serialize(document));
    }

    private static double Round(double value) => Math.Round(value, 5);

    /// <summary>The mods staged beside this tool by build/ModPayloads.targets.</summary>
    private static ModSource[] Discover()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "mods");
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"no mods beside the tool at {root}");
        return Directory.GetDirectories(root).Order(StringComparer.Ordinal).Select(ModLoader.FromDirectory).ToArray();
    }

    private sealed record Region(double X0, double Z0, double X1, double Z1)
    {
        public bool Holds(double x, double z) => x >= X0 && x <= X1 && z >= Z0 && z <= Z1;
    }

    /// <summary>Triangles of one mesh with any corner in the region, as flat arrays: positions (x, y, z) and per-vertex
    /// attributes, re-indexed so only the vertices they use are written.</summary>
    private sealed class MeshWriter(Region region)
    {
        private readonly List<double> _positions = [];
        private readonly List<double> _attributes = [];
        private readonly List<int> _indices = [];
        private int _width = -1;

        public void Add(float[] positions, int[] indices, Func<int, float[]> attributes)
        {
            var map = new Dictionary<int, int>();
            for (int t = 0; t < indices.Length; t += 3)
            {
                bool inside = false;
                for (int k = 0; k < 3; k++)
                {
                    int v = indices[t + k];
                    inside |= region.Holds(positions[3 * v], positions[3 * v + 2]);
                }
                if (!inside) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = indices[t + k];
                    if (!map.TryGetValue(v, out int at))
                    {
                        at = _positions.Count / 3;
                        map[v] = at;
                        _positions.AddRange([Round(positions[3 * v]), Round(positions[3 * v + 1]), Round(positions[3 * v + 2])]);
                        var extra = attributes(v);
                        _width = extra.Length;
                        foreach (float a in extra) _attributes.Add(Round(a));
                    }
                    _indices.Add(at);
                }
            }
        }

        public object Result() => new { positions = _positions, attributes = _attributes, stride = Math.Max(_width, 0), indices = _indices };
    }

    private sealed record Options(string Scenario, string Out, (int Col, int Row)? Focus, bool Fall, double Radius, ulong Seed);

    private static bool TryParse(string[] args, out Options options, out string? error)
    {
        string? scenario = null, output = null;
        (int, int)? focus = null;
        double radius = 1.5;
        bool fall = false;
        ulong seed = 1;
        error = null;
        for (int i = 0; i < args.Length && error is null; i++)
        {
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (args[i])
            {
                case "--out": output = Next() ?? (error = "--out needs a file"); break;
                case "--focus":
                    var parts = Next()?.Split(',');
                    if (parts is [var c, var r] && int.TryParse(c, out int col) && int.TryParse(r, out int row)) focus = (col, row);
                    else error = "--focus needs COL,ROW";
                    break;
                case "--fall": fall = true; break;
                case "--radius":
                    if (!double.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out radius) || radius <= 0)
                        error = "--radius needs a positive number of tiles";
                    break;
                case "--seed":
                    if (!ulong.TryParse(Next(), out seed)) error = "--seed needs a whole number";
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal)) error = $"unknown option '{args[i]}'";
                    else if (scenario is null) scenario = args[i];
                    else error = "only one scenario at a time";
                    break;
            }
        }
        if (error is null && scenario is null) error = "no scenario";
        if (error is null && output is null) error = "no --out file";
        options = new(scenario ?? "", output ?? "", focus, fall, radius, seed);
        return error is null;
    }
}
