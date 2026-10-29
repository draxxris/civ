using System.Diagnostics;
using Ariel.Client.Terrain;
using Ariel.Game;
using Godot;

namespace Ariel.Client;

/// <summary>
/// The 3D terrain: one lit mesh, one sea/lake water mesh and one river surface per chunk (three wrap copies on a wrapping
/// map), shaded from the tile-data textures. A world change rebuilds only the chunks whose dirty signature changed; sight, overlay marks and
/// hover only re-upload <c>tileB</c>.
/// </summary>
internal sealed partial class TerrainRenderer : Node3D
{
    private readonly GameSession _session;
    private readonly TerrainChunks _chunks;
    private readonly TileDataTexture _data;
    private readonly TerrainTextures _textures;
    private readonly ShaderMaterial _terrainMaterial, _waterMaterial, _riverMaterial;
    private readonly MeshInstance3D[][] _ground, _water, _rivers;
    private readonly HazeRenderer _haze;
    private readonly float[] _copies;
    private readonly byte[] _marks;
    private readonly int _seed;
    private readonly ScatterPlanner _scatterPlanner;
    private readonly ScatterRenderer _scatter;
    private readonly Func<string, bool> _clearsTile;
    private readonly Func<string, bool> _hasModel;
    private ulong[]? _signatures;
    private int _worldRevision = -1;
    private int _hover = -1;

    /// <summary>Terrain shader debug view for screenshot runs (see <c>debug_mode</c> in terrain.gdshader); 0 is normal.</summary>
    public static int DebugMode { get; set; }
    /// <summary>Height tuning for this run (command-line overrides); the defaults in normal play.</summary>
    public static TerrainTuning Tuning { get; set; } = new();
    /// <summary>Scatter density multiplier for this run; 1 in normal play.</summary>
    public static double ScatterDensityScale { get; set; } = 1;

    /// <summary>Switch the terrain shader's debug view without rebuilding anything.</summary>
    /// <summary>Draw the mesh lattice (debug view 7): the compatibility renderer has no wireframe mode.</summary>
    public void SetWireframe(bool on) => SetDebugMode(on ? 7 : 0);

    public void SetDebugMode(int mode) => _terrainMaterial.SetShaderParameter("debug_mode", mode);

    /// <param name="clearsTile">True for an improvement whose 3D model fills its whole tile, so no scatter
    /// props grow through it; other improvements only keep the tile centre clear.</param>
    /// <param name="hasModel">True for a content id (here, a resource) that ships a 3D model.</param>
    public TerrainRenderer(GameSession session, GameCatalog catalog, Func<string, bool> clearsTile, Func<string, bool> hasModel)
    {
        _session = session;
        _clearsTile = clearsTile;
        _hasModel = hasModel;
        var map = session.Sim.State.Map;
        Palette = new TerrainPalette(catalog);
        _textures = new TerrainTextures(Palette);
        var tuning = Tuning;
        _chunks = new TerrainChunks(map, tuning);
        _data = new TileDataTexture(map.Width, map.Height);
        _marks = new byte[map.Width * map.Height];
        ulong seed = session.Sim.Settings.Seed;
        _seed = unchecked((int)(seed ^ (seed >> 32)));
        Snapshot = new TerrainSnapshot(map);
        Field = new TerrainField(Snapshot, _seed, tuning);
        _copies = map.Wrap ? [-(float)Snapshot.Period, 0f, (float)Snapshot.Period] : [0f];

        _scatterPlanner = new ScatterPlanner(catalog) { DensityScale = ScatterDensityScale };
        _scatter = new ScatterRenderer(catalog, _copies);
        AddChild(_scatter);

        var warp = TileDataTexture.WarpTexture(Field.Warp);
        var palette = TileDataTexture.PaletteTexture(Palette, WaterTintOf);
        _terrainMaterial = Material("res://shaders/terrain.gdshader", warp, palette);
        _waterMaterial = Material("res://shaders/water.gdshader", warp, palette);
        _riverMaterial = Material("res://shaders/river.gdshader", warp, palette);
        RoadMaterial = Material("res://shaders/road.gdshader", warp, palette);
        // After the rivers, so a road crossing one stays on top.
        RoadMaterial.RenderPriority = 1;
        RouteStructureMaterial = Material("res://shaders/route_structure.gdshader", warp, palette);
        _ground = _chunks.Chunks.Select(_ => Instances()).ToArray();
        _water = _chunks.Chunks.Select(_ => Instances()).ToArray();
        _rivers = _chunks.Chunks.Select(_ => Instances()).ToArray();
        _haze = new HazeRenderer(_chunks.Chunks, _copies, Snapshot.Period);
        AddChild(_haze);
    }

    /// <summary>Draped route ribbons (road.gdshader): lit, fogged and blended with the ground like the terrain.</summary>
    public ShaderMaterial RoadMaterial { get; }
    /// <summary>Route decks, supports and masts (route_structure.gdshader), fogged per pixel.</summary>
    public ShaderMaterial RouteStructureMaterial { get; }
    /// <summary>Material and scatter lookups for the loaded content.</summary>
    public TerrainPalette Palette { get; }
    /// <summary>The terrain inputs as the local team knows them, as of the last world refresh.</summary>
    public TerrainSnapshot Snapshot { get; private set; }
    /// <summary>The height field the chunks were built from.</summary>
    public TerrainField Field { get; private set; }
    /// <summary>Per tile, the <see cref="BonusSpot"/> where its resource model stands, or -1 for none, as of the last
    /// world refresh. The map seats the models there and the scatter keeps the spot clear.</summary>
    public int[] BonusSpots { get; private set; } = [];

    /// <summary>Refresh perceived terrain and scatter only after a world revision, before seating map layers.</summary>
    public void RefreshWorld()
    {
        if (_worldRevision == _session.WorldRevision) return;
        RebuildWorld();
        _scatter.UpdateVisibility(Snapshot);
        _haze.Rebuild(Field, Palette.FeatureGrounds);
        _worldRevision = _session.WorldRevision;
    }

    /// <summary>Upload sight and overlay data once, after the map writes its current marks.</summary>
    public void RefreshOverlay() => _data.WriteTileB(Snapshot, _marks, _hover);

    /// <summary>Shared surface controls: uniforms only, with no terrain or sight rebuild.</summary>
    public void SetMapOverlays(MapOverlays overlays)
    {
        foreach (var material in new[] { _terrainMaterial, _waterMaterial, _riverMaterial })
        {
            material.SetShaderParameter("show_grid", overlays.Grid);
            material.SetShaderParameter("show_borders", overlays.CulturalBorders);
        }
    }

    /// <summary>Set the work-tile overlay marks; uploaded by the next <see cref="RefreshOverlay"/>.</summary>
    public void SetMarks(IEnumerable<(Hex Hex, byte Mark)> marks)
    {
        Array.Clear(_marks);
        var grid = _session.Sim.State.Grid;
        foreach (var (hex, mark) in marks)
            if (grid.TryTile(hex, out var tile)) _marks[tile.Value] = mark;
    }

    /// <summary>Outline one tile (or none) without rebuilding any mesh.</summary>
    public void SetHover(Hex? hex)
    {
        int tile = hex is Hex h && _session.Sim.State.Grid.TryTile(h, out var id) ? id.Value : -1;
        if (tile == _hover) return;
        _hover = tile;
        _data.WriteTileB(Snapshot, _marks, _hover);
    }

    private void RebuildWorld()
    {
        bool logTimings = MapRenderer.LogTimings;
        long totalStart = logTimings ? Stopwatch.GetTimestamp() : 0;
        Snapshot = TerrainSnapshot.From(_session, Palette.LayerOf, Palette.FeatureGroundOf, Palette.IsCrater);
        Field = Field.With(Snapshot);
        BonusSpots = ResourceSpots();
        var extras = ScatterClearings();
        var signatures = _chunks.Signatures(Snapshot, extras);
        var dirty = TerrainChunks.DirtyChunks(_signatures, signatures);
        var prepared = new PreparedChunk?[dirty.Count];
        var field = Field;
        var resolutions = _chunks.Resolutions(Snapshot);
        double inputsMs = logTimings ? Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds : 0;
        var meshMs = logTimings ? new double[dirty.Count] : null;
        var prepareMs = logTimings ? new double[dirty.Count] : null;
        Parallel.For(0, dirty.Count, i =>
        {
            long start = logTimings ? Stopwatch.GetTimestamp() : 0;
            var chunk = _chunks.Chunks[dirty[i]];
            var mesh = TerrainMesher.Build(chunk, field, _chunks.Resolution(chunk, resolutions));
            var river = RiverMesher.Build(chunk, field, (x, z) => _chunks.ResolutionAt(x, z, resolutions));
            if (logTimings) meshMs![i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            start = logTimings ? Stopwatch.GetTimestamp() : 0;
            prepared[i] = new(PrepareGround(mesh, field), PrepareWater(mesh, field), PrepareRiver(river));
            if (logTimings) prepareMs![i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        });
        double built = logTimings ? Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds : 0;
        double assignMs = 0, planMs = 0, scatterMs = 0;
        var uploads = logTimings ? new UploadTimings() : null;
        for (int i = 0; i < dirty.Count; i++)
        {
            // The worker join publishes complete packets. Consume them in dirty-chunk order, then release the arrays.
            var data = prepared[i]!;
            prepared[i] = null;
            var ground = Build(data.Ground, _terrainMaterial, uploads)!;
            var water = Build(data.Water, _waterMaterial, uploads);
            long start = logTimings ? Stopwatch.GetTimestamp() : 0;
            RenderStats.TerrainTriangles[dirty[i]] = ground.SurfaceGetArrayIndexLen(0) / 3;
            foreach (var copy in _ground[dirty[i]]) copy.Mesh = ground;
            foreach (var copy in _water[dirty[i]]) copy.Mesh = water;
            if (logTimings) assignMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var river = Build(data.River, _riverMaterial, uploads);
            start = logTimings ? Stopwatch.GetTimestamp() : 0;
            foreach (var copy in _rivers[dirty[i]]) copy.Mesh = river;
            if (logTimings) assignMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            start = logTimings ? Stopwatch.GetTimestamp() : 0;
            var props = _scatterPlanner.Plan(_chunks.Chunks[dirty[i]], Snapshot, Field, Palette, extras);
            if (logTimings) planMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            start = logTimings ? Stopwatch.GetTimestamp() : 0;
            _scatter.Rebuild(dirty[i], props, Snapshot);
            if (logTimings) scatterMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        _signatures = signatures;
        long tileStart = logTimings ? Stopwatch.GetTimestamp() : 0;
        _data.WriteTileA(Snapshot);
        double tileMs = logTimings ? Stopwatch.GetElapsedTime(tileStart).TotalMilliseconds : 0;
        if (logTimings)
            GD.Print($"terrain: {dirty.Count}/{_chunks.Chunks.Count} chunks, inputs {inputsMs:F2} ms, workers {built - inputsMs:F2} ms, " +
                $"mesh sum {meshMs!.Sum():F2} ms, prepare sum {prepareMs!.Sum():F2} ms, pack {uploads!.PackMs:F2} ms, surfaces {uploads.SurfaceMs:F2} ms, " +
                $"assign {assignMs:F2} ms, scatter plan {planMs:F2} ms, scatter rebuild {scatterMs:F2} ms, tileA {tileMs:F2} ms, total {Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds:F2} ms");
    }

    private int[] ScatterClearings()
    {
        var state = _session.Sim.State;
        var extras = new int[state.Plots.Length];
        for (int i = 0; i < extras.Length; i++)
            if (_session.PerceivedPlot(new TileId(i))?.Improvement is { } improvement)
                extras[i] = _clearsTile(improvement) ? ScatterPlanner.ClearTile : ScatterPlanner.ClearCentre;
        foreach (var city in _session.KnownCities())
        {
            int tile = state.Grid.TileAt(city.Position).Value;
            if (Snapshot.Sight[tile] == Sight.Hidden) continue;
            // The city's buildings fill its tile.
            extras[tile] |= ScatterPlanner.ClearTile;
        }
        foreach (var unit in _session.VisibleUnits())
            extras[state.Grid.TileAt(unit.Position).Value] |= ScatterPlanner.ClearCentre;
        for (int i = 0; i < extras.Length; i++)
            if (BonusSpots[i] >= 0) extras[i] |= ScatterPlanner.BonusClearing(BonusSpots[i]);
        return extras;
    }

    /// <summary>Choose a spot for every known, revealed resource that ships a model. City tiles get none: the city's
    /// buildings fill the tile, and the resource overlay still shows the icon there.</summary>
    private int[] ResourceSpots()
    {
        var state = _session.Sim.State;
        var spots = new int[state.Plots.Length];
        Array.Fill(spots, -1);
        var cities = _session.KnownCities().Select(c => state.Grid.TileAt(c.Position).Value).ToHashSet();
        for (int i = 0; i < spots.Length; i++)
        {
            if (Snapshot.Sight[i] == Sight.Hidden || cities.Contains(i)) continue;
            if (_session.PerceivedPlot(new TileId(i))?.Bonus is not { } bonus || !_session.BonusVisible(bonus) || !_hasModel(bonus))
                continue;
            var (x, z) = state.Grid.HexOf(new TileId(i)).ToPixel(1);
            bool water = Snapshot.Relief[i] is ReliefClass.DeepWater or ReliefClass.ShallowWater;
            spots[i] = BonusSpot.Choose(Field, x, z, water);
        }
        return spots;
    }

    private MeshInstance3D[] Instances() => _copies.Select(offset =>
    {
        var instance = new MeshInstance3D { Position = new Vector3(offset, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(instance);
        return instance;
    }).ToArray();

    private ShaderMaterial Material(string path, Texture2D warp, Texture2D palette)
    {
        var map = _session.Sim.State.Map;
        var tuning = Field.Tuning;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(path) };
        material.SetShaderParameter("tile_a", _data.TileA);
        material.SetShaderParameter("tile_b", _data.TileB);
        material.SetShaderParameter("warp_grid", warp);
        material.SetShaderParameter("palette", palette);
        material.SetShaderParameter("terrain_textures", _textures.Array);
        material.SetShaderParameter("cliff_layer", Palette.SurfaceLayerOf(TerrainSurface.Cliff));
        material.SetShaderParameter("seabed_layer", Palette.SurfaceLayerOf(TerrainSurface.Seabed));
        material.SetShaderParameter("deep_seabed_layer", Palette.SurfaceLayerOf(TerrainSurface.DeepSeabed));
        material.SetShaderParameter("unexplored_layer", Palette.SurfaceLayerOf(TerrainSurface.Unexplored));
        material.SetShaderParameter("snow_layer", Palette.SurfaceLayerOf(TerrainSurface.Snow));
        material.SetShaderParameter("map_size", new Vector2I(map.Width, map.Height));
        material.SetShaderParameter("wrap_x", map.Wrap);
        material.SetShaderParameter("period", (float)Snapshot.Period);
        material.SetShaderParameter("sharpness", (float)tuning.MaterialSharpness);
        material.SetShaderParameter("warp_amplitude", (float)tuning.WarpAmplitude);
        material.SetShaderParameter("warp_size", new Vector2I(Field.Warp.Columns, Field.Warp.Rows));
        material.SetShaderParameter("warp_step", new Vector2((float)Field.Warp.StepX, (float)Field.Warp.StepZ));
        material.SetShaderParameter("warp_z_min", (float)Field.Warp.ZMin);
        material.SetShaderParameter("debug_mode", DebugMode);
        return material;
    }

    /// <summary>Slimy water terrains tint the water surface green; other layers leave it alone.</summary>
    private static Rgb? WaterTintOf(string layerId) =>
        layerId.Contains("slimy", StringComparison.Ordinal) ? Rgb.Hex(0x5c9a1e) : null;

    // Numeric Vector/Color constructors only assign managed fields. Keep Variant packing and all engine calls in Build.
    // Water preparation only reads the captured field's snapshot and warp grid; it never reads the renderer from a worker.
    private sealed record SurfaceData(Vector3[] Vertices, Vector3[] Normals, Vector2[] Uv2, int[] Indices,
        Vector2[]? Uv = null, Color[]? Colors = null);

    private sealed record PreparedChunk(SurfaceData Ground, SurfaceData? Water, SurfaceData? River);

    private sealed class UploadTimings
    {
        public double PackMs, SurfaceMs;
    }

    /// <summary>The chunk's ground; UV2.x holds the lattice cavity for the shader's valley shading and snow, UV2.y the
    /// crater floor share (<see cref="TerrainField.CraterAt"/>) for the lava glow.</summary>
    private static SurfaceData PrepareGround(TerrainMeshData data, TerrainField field)
    {
        int n = data.VertexCount;
        var vertices = new Vector3[n];
        var normals = new Vector3[n];
        var cavity = new Vector2[n];
        for (int v = 0; v < n; v++)
        {
            vertices[v] = new Vector3(data.Positions[v * 3], data.Positions[v * 3 + 1], data.Positions[v * 3 + 2]);
            normals[v] = new Vector3(data.Normals[v * 3], data.Normals[v * 3 + 1], data.Normals[v * 3 + 2]);
            cavity[v] = new Vector2(data.Cavity[v], (float)field.CraterAt(data.Positions[v * 3], data.Positions[v * 3 + 2]));
        }
        return new(vertices, normals, cavity, FrontFaces(data.Indices, _ => true));
    }

    /// <summary>A flat surface at y = 0 over every lattice triangle with a vertex below sea level that a water tile shapes;
    /// UV2.x holds the terrain depth (-HeightAt) for the shader's colour and shore foam, UV2.y the cliff share
    /// (<see cref="TerrainField.CliffAt"/>) for surf at the foot of cliffs. River troughs that dip below 0 on
    /// land get no water, so they do not fill into pools.</summary>
    private static SurfaceData? PrepareWater(TerrainMeshData data, TerrainField field)
    {
        int n = data.VertexCount;
        var vertices = new Vector3[n];
        var depths = new Vector2[n];
        var normals = new Vector3[n];
        for (int v = 0; v < n; v++)
        {
            vertices[v] = new Vector3(data.Positions[v * 3], 0, data.Positions[v * 3 + 2]);
            depths[v] = new Vector2(-data.Positions[v * 3 + 1], (float)field.CliffAt(data.Positions[v * 3], data.Positions[v * 3 + 2]));
            normals[v] = Vector3.Up;
        }
        var wet = new bool[n];
        for (int v = 0; v < n; v++) wet[v] = data.Positions[v * 3 + 1] < 0 && field.NearWater(data.Positions[v * 3], data.Positions[v * 3 + 2]);
        var indices = FrontFaces(data.Indices, t => wet[data.Indices[t]] || wet[data.Indices[t + 1]] || wet[data.Indices[t + 2]]);
        return indices.Length == 0 ? null : new(vertices, normals, depths, indices);
    }


    /// <summary>The chunk's river water and wet banks: lanes in UV, downstream or downhill flow in UV2, fade, white water and fall share in COLOR.</summary>
    private static SurfaceData? PrepareRiver(RiverMeshData? data)
    {
        if (data == null) return null;
        int n = data.VertexCount;
        var vertices = new Vector3[n];
        var normals = new Vector3[n];
        var lanes = new Vector2[n];
        var flow = new Vector2[n];
        var info = new Color[n];
        for (int v = 0; v < n; v++)
        {
            vertices[v] = new Vector3(data.Positions[v * 3], data.Positions[v * 3 + 1], data.Positions[v * 3 + 2]);
            normals[v] = new Vector3(data.Normals[v * 3], data.Normals[v * 3 + 1], data.Normals[v * 3 + 2]);
            lanes[v] = new Vector2(data.Lanes[v * 2], data.Lanes[v * 2 + 1]);
            flow[v] = new Vector2(data.Flow[v * 2], data.Flow[v * 2 + 1]);
            info[v] = new Color(data.Info[v * 3], data.Info[v * 3 + 1], data.Info[v * 3 + 2]);
        }
        return new(vertices, normals, flow, FrontFaces(data.Indices, _ => true), lanes, info);
    }

    /// <summary>The mesher winds triangles counter-clockwise seen from above; Godot's front faces are clockwise.</summary>
    private static int[] FrontFaces(int[] indices, Func<int, bool> keep)
    {
        var result = new List<int>(indices.Length);
        for (int t = 0; t < indices.Length; t += 3)
        {
            if (!keep(t)) continue;
            result.Add(indices[t]); result.Add(indices[t + 2]); result.Add(indices[t + 1]);
        }
        return result.ToArray();
    }

    private static ArrayMesh? Build(SurfaceData? data, Material material, UploadTimings? timings)
    {
        if (data == null || data.Indices.Length == 0) return null;
        long start = timings != null ? Stopwatch.GetTimestamp() : 0;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Vertices;
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals;
        if (data.Uv != null) arrays[(int)Mesh.ArrayType.TexUV] = data.Uv;
        arrays[(int)Mesh.ArrayType.TexUV2] = data.Uv2;
        if (data.Colors != null) arrays[(int)Mesh.ArrayType.Color] = data.Colors;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        if (timings != null)
        {
            timings.PackMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            start = Stopwatch.GetTimestamp();
        }
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        if (timings != null) timings.SurfaceMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return mesh;
    }
}
