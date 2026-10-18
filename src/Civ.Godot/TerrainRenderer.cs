using Civ.Client.Terrain;
using Civ.Game;
using Godot;

namespace Civ.Client;

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
    private readonly float[] _copies;
    private readonly byte[] _marks;
    private readonly int _seed;
    private readonly ScatterPlanner _scatterPlanner;
    private readonly ScatterRenderer _scatter;
    private readonly Func<string, bool> _clearsTile;
    private readonly Func<City, string?> _cityRing;
    private ulong[]? _signatures;
    private int _snapshotRevision = -1;
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
    public TerrainRenderer(GameSession session, GameCatalog catalog, Func<string, bool> clearsTile, Func<City, string?> cityRing)
    {
        _session = session;
        _clearsTile = clearsTile;
        _cityRing = cityRing;
        var map = session.Sim.State.Map;
        Palette = new TerrainPalette(catalog);
        _textures = new TerrainTextures(Palette);
        _chunks = new TerrainChunks(map, Tuning);
        _data = new TileDataTexture(map.Width, map.Height);
        _marks = new byte[map.Width * map.Height];
        ulong seed = session.Sim.Settings.Seed;
        _seed = unchecked((int)(seed ^ (seed >> 32)));
        Snapshot = new TerrainSnapshot(map);
        Field = new TerrainField(Snapshot, _seed, Tuning);
        _copies = map.Wrap ? [-(float)Snapshot.Period, 0f, (float)Snapshot.Period] : [0f];

        _scatterPlanner = new ScatterPlanner(catalog) { DensityScale = ScatterDensityScale };
        _scatter = new ScatterRenderer(catalog, _copies);
        AddChild(_scatter);

        var warp = TileDataTexture.WarpTexture(Field.Warp);
        var palette = TileDataTexture.PaletteTexture(Palette, WaterTintOf);
        _terrainMaterial = Material("res://shaders/terrain.gdshader", warp, palette);
        _waterMaterial = Material("res://shaders/water.gdshader", warp, palette);
        _riverMaterial = Material("res://shaders/river.gdshader", warp, palette);
        _ground = _chunks.Chunks.Select(_ => Instances()).ToArray();
        _water = _chunks.Chunks.Select(_ => Instances()).ToArray();
        _rivers = _chunks.Chunks.Select(_ => Instances()).ToArray();
    }

    /// <summary>Material and scatter lookups for the loaded content.</summary>
    public TerrainPalette Palette { get; }
    /// <summary>The terrain inputs as the local team knows them, as of the last world refresh.</summary>
    public TerrainSnapshot Snapshot { get; private set; }
    /// <summary>The height field the chunks were built from.</summary>
    public TerrainField Field { get; private set; }

    /// <summary>Rebuild dirty chunks and <c>tileA</c> after a world change; re-upload <c>tileB</c> after any change.</summary>
    public void Refresh(bool worldChanged, bool overlayChanged)
    {
        if (worldChanged || (overlayChanged && _snapshotRevision != _session.Revision)) RebuildWorld();
        if (worldChanged || overlayChanged)
        {
            _data.WriteTileB(Snapshot, _marks, _hover);
            _scatter.UpdateVisibility(Snapshot);
        }
    }

    /// <summary>Set the work-tile overlay marks; uploaded by the next <see cref="Refresh"/>.</summary>
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
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Snapshot = TerrainSnapshot.From(_session, Palette.LayerOf, Palette.FeatureGroundOf);
        Field = Field.With(Snapshot);
        var extras = ScatterClearings();
        var signatures = _chunks.Signatures(Snapshot, extras);
        var dirty = TerrainChunks.DirtyChunks(_signatures, signatures);
        var meshes = new TerrainMeshData[dirty.Count];
        var rivers = new RiverMeshData?[dirty.Count];
        var field = Field;
        Parallel.For(0, dirty.Count, i =>
        {
            meshes[i] = TerrainMesher.Build(_chunks.Chunks[dirty[i]], field);
            rivers[i] = RiverMesher.Build(_chunks.Chunks[dirty[i]], field);
        });
        long built = clock.ElapsedMilliseconds;
        for (int i = 0; i < dirty.Count; i++)
        {
            var ground = GroundMesh(meshes[i]);
            var water = WaterMesh(meshes[i]);
            RenderStats.TerrainTriangles[dirty[i]] = ground.SurfaceGetArrayIndexLen(0) / 3;
            foreach (var copy in _ground[dirty[i]]) copy.Mesh = ground;
            foreach (var copy in _water[dirty[i]]) copy.Mesh = water;
            var river = RiverMesh(rivers[i]);
            foreach (var copy in _rivers[dirty[i]]) copy.Mesh = river;
            var props = _scatterPlanner.Plan(_chunks.Chunks[dirty[i]], Snapshot, Field, Palette, extras);
            _scatter.Rebuild(dirty[i], props, Snapshot);
        }
        _signatures = signatures;
        _snapshotRevision = _session.Revision;
        _data.WriteTileA(Snapshot);
        if (MapRenderer.LogTimings) GD.Print($"terrain: {dirty.Count}/{_chunks.Chunks.Count} chunks, mesh {built} ms, upload {clock.ElapsedMilliseconds - built} ms");
    }

    private int[] ScatterClearings()
    {
        var state = _session.Sim.State;
        var extras = new int[state.Plots.Length];
        for (int i = 0; i < extras.Length; i++)
            if (Snapshot.Sight[i] != Sight.Hidden && state.Plots[i].Improvement is { } improvement)
                extras[i] = _clearsTile(improvement) ? ScatterPlanner.ClearTile : ScatterPlanner.ClearCentre;
        foreach (var city in _session.KnownCities())
        {
            int tile = state.Grid.TileAt(city.Position).Value;
            if (Snapshot.Sight[tile] == Sight.Hidden) continue;
            extras[tile] |= _cityRing(city) is { } ring && _clearsTile(ring)
                ? ScatterPlanner.ClearTile : ScatterPlanner.ClearCentre;
        }
        foreach (var unit in _session.VisibleUnits())
            extras[state.Grid.TileAt(unit.Position).Value] |= ScatterPlanner.ClearCentre;
        return extras;
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

    private ArrayMesh GroundMesh(TerrainMeshData data)
    {
        int n = data.VertexCount;
        var vertices = new Vector3[n];
        var normals = new Vector3[n];
        for (int v = 0; v < n; v++)
        {
            vertices[v] = new Vector3(data.Positions[v * 3], data.Positions[v * 3 + 1], data.Positions[v * 3 + 2]);
            normals[v] = new Vector3(data.Normals[v * 3], data.Normals[v * 3 + 1], data.Normals[v * 3 + 2]);
        }
        return Build(vertices, normals, null, FrontFaces(data.Indices, _ => true), _terrainMaterial)!;
    }

    /// <summary>A flat surface at y = 0 over every lattice triangle with a vertex below sea level that a water tile shapes;
    /// UV2.x holds the terrain depth (-HeightAt) for the shader's colour and shore foam. River troughs that dip below 0 on
    /// land get no water, so they do not fill into pools.</summary>
    private ArrayMesh? WaterMesh(TerrainMeshData data)
    {
        int n = data.VertexCount;
        var vertices = new Vector3[n];
        var depths = new Vector2[n];
        var normals = new Vector3[n];
        for (int v = 0; v < n; v++)
        {
            vertices[v] = new Vector3(data.Positions[v * 3], 0, data.Positions[v * 3 + 2]);
            depths[v] = new Vector2(-data.Positions[v * 3 + 1], 0);
            normals[v] = Vector3.Up;
        }
        var wet = new bool[n];
        for (int v = 0; v < n; v++) wet[v] = data.Positions[v * 3 + 1] < 0 && Field.NearWater(data.Positions[v * 3], data.Positions[v * 3 + 2]);
        var indices = FrontFaces(data.Indices, t => wet[data.Indices[t]] || wet[data.Indices[t + 1]] || wet[data.Indices[t + 2]]);
        return indices.Length == 0 ? null : Build(vertices, normals, depths, indices, _waterMaterial);
    }


    /// <summary>The chunk's river water and wet banks: lanes in UV, downstream flow in UV2, fade and white water in COLOR.</summary>
    private ArrayMesh? RiverMesh(RiverMeshData? data)
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
            info[v] = new Color(data.Info[v * 2], data.Info[v * 2 + 1], 0);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = lanes;
        arrays[(int)Mesh.ArrayType.TexUV2] = flow;
        arrays[(int)Mesh.ArrayType.Color] = info;
        arrays[(int)Mesh.ArrayType.Index] = FrontFaces(data.Indices, _ => true);
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, _riverMaterial);
        return mesh;
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

    private static ArrayMesh? Build(Vector3[] vertices, Vector3[] normals, Vector2[]? uv2, int[] indices, Material material)
    {
        if (indices.Length == 0) return null;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        if (uv2 != null) arrays[(int)Mesh.ArrayType.TexUV2] = uv2;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }
}
