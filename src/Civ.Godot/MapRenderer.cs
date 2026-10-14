using Godot;
using Civ.Client.Terrain;

namespace Civ.Client;

/// <summary>Three copies of one mesh, one world-width apart, so the east-west seam of a wrapping map never shows a gap.</summary>
internal sealed partial class WrapMesh : Node3D
{
    private readonly MeshInstance3D[] _copies = new MeshInstance3D[3];

    public WrapMesh(float period)
    {
        for (int i = 0; i < 3; i++)
        {
            _copies[i] = new MeshInstance3D { Position = new Vector3((i - 1) * period, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_copies[i]);
        }
    }

    public Mesh? Mesh
    {
        set { foreach (var copy in _copies) copy.Mesh = value; }
    }
}

/// <summary>
/// Draws the map: the 3D terrain and water (<see cref="TerrainRenderer"/>, which also shades fog, borders, rivers, hover and
/// the city-screen overlay), feature marks, routes and icons as one baked mesh (rebuilt when the simulation changes), plus a
/// cheap token layer for cities and units.
/// </summary>
internal sealed partial class MapRenderer : Node3D
{
    public const float YRelief = 0.012f, YLines = 0.024f, YIcons = 0.036f, YOverlay = 0.048f, YTokens = 0.06f, YPath = 0.075f;

    private readonly GameSession _session;
    private readonly IconCache _icons;
    private readonly IconAtlas _atlas;
    private readonly WrapMesh _land, _landIcons, _tokens, _tokenIcons, _path;
    private readonly TerrainRenderer _terrain;
    private readonly StandardMaterial3D _opaque, _landIconMaterial, _tokenIconMaterial, _tokenMaterial, _pathMaterial;
    private readonly List<(Label3D Label, Vector3 Base)> _labels = new();
    private readonly List<Label3D> _labelPool = new();
    private readonly ModelCache _models;
    private int _worldRevision = -1, _revision = -1;

    /// <summary>Print mesh rebuild times (set by the screenshot options).</summary>
    public static bool LogTimings { get; set; }

    public float Period { get; }
    public float MapDepth => 1.5f * (_session.Sim.State.Map.Height - 1);

    public MapRenderer(GameSession session, IconCache icons)
    {
        _session = session;
        _icons = icons;
        _atlas = new IconAtlas(icons);
        _models = new ModelCache(icons.Catalog);
        Period = (float)session.WrapPeriod;
        _opaque = Material(null, false, 0);
        _landIconMaterial = Material(_atlas.Texture, true, 2);
        _tokenMaterial = Material(null, true, 3);
        _tokenIconMaterial = Material(_atlas.Texture, true, 4);
        _pathMaterial = Material(null, true, 5);
        _terrain = new TerrainRenderer(session, icons.Catalog);
        AddChild(_terrain);
        _land = Layer();
        _landIcons = Layer();
        _tokens = Layer();
        _tokenIcons = Layer();
        _path = Layer();
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40, -135, 0), LightEnergy = 1.0f, ShadowEnabled = false });
    }

    private WrapMesh Layer()
    {
        var layer = new WrapMesh(Period);
        AddChild(layer);
        return layer;
    }

    private static StandardMaterial3D Material(Texture2D? texture, bool transparent, int priority) => new()
    {
        RenderPriority = priority,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = texture,
        Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled, TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
    };

    /// <summary>The same field used by the rendered chunks, including local fog knowledge.</summary>
    public TerrainField TerrainField => _terrain.Field;

    public float GroundAt(float x, float z) => (float)TerrainField.SurfaceHeightAt(x, z);

    /// <summary>A tile centre seated on the visible surface, plus a clearance offset.</summary>
    public Vector3 WorldOf(Hex hex, float yOffset = 0)
    {
        var p = PositionOf(hex, yOffset);
        p.Y += GroundAt(p.X, p.Z);
        return p;
    }

    /// <summary>Flat map coordinates for camera focus and input to a draped mesh.</summary>
    public static Vector3 PositionOf(Hex hex, float y = 0)
    {
        var (x, z) = hex.ToPixel(1);
        return new Vector3((float)x, y, (float)z);
    }

    /// <summary>Rebuild whatever the session changed since the last call.</summary>
    public void Refresh()
    {
        bool world = _worldRevision != _session.WorldRevision;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        bool dynamic = world || _revision != _session.Revision;
        // Heights must be current before seating tokens, marks or a newly queued battle.
        _terrain.Refresh(world, dynamic);
        long terrain = clock.ElapsedMilliseconds;
        if (_battle == null) StartNextBattle();
        dynamic = world || _revision != _session.Revision;
        if (world) { BuildLand(); _worldRevision = _session.WorldRevision; }
        long land = clock.ElapsedMilliseconds - terrain;
        if (dynamic) { BuildDynamic(); _revision = _session.Revision; }
        long tokens = clock.ElapsedMilliseconds - terrain - land;
        // BuildOverlay writes marks after the height refresh.
        if (dynamic) _terrain.Refresh(false, true);
        if (LogTimings && dynamic) GD.Print($"map refresh: land {land} ms, tokens {tokens} ms, terrain {terrain} ms");
    }

    private void BuildDynamic()
    {
        foreach (var (label, _) in _labels) label.Visible = false;
        _labels.Clear();
        BuildTokens();
        BuildOverlay();
        _atlas.Flush();
    }

    /// <summary>Keep each label on the world copy nearest the camera so text never sits a map-width away.</summary>
    public void WrapLabels(float focusX)
    {
        PlaceActors(focusX);
        if (Period <= 0) return;
        foreach (var (label, basePos) in _labels)
            label.Position = basePos + new Vector3(Mathf.Round((focusX - basePos.X) / Period) * Period, 0, 0);
    }

    private Label3D AddLabel(string text, Vector3 position, int size, Color color)
    {
        var label = _labels.Count < _labelPool.Count ? _labelPool[_labels.Count] : NewLabel();
        label.Text = text; label.FontSize = size; label.Modulate = color; label.Visible = true;
        _labels.Add((label, position));
        label.Position = position;
        return label;
    }

    private Label3D NewLabel()
    {
        var label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, PixelSize = 0.0009f, OutlineSize = 10,
            OutlineModulate = new Color(0, 0, 0, 0.9f), FixedSize = true, RenderPriority = 10,
        };
        AddChild(label);
        _labelPool.Add(label);
        return label;
    }

    /// <summary>Outline the hovered tile in the terrain shader.</summary>
    public void ShowHover(Hex? hex) => _terrain.SetHover(hex);
}
