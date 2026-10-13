using Godot;

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
/// Draws the map: terrain, relief, features, rivers, borders, routes and icons as one baked mesh (rebuilt when the
/// simulation changes), plus a cheap token layer for cities, units and the city-screen overlay.
/// </summary>
internal sealed partial class MapRenderer : Node3D
{
    public const float YRelief = 0.012f, YLines = 0.024f, YIcons = 0.036f, YOverlay = 0.048f, YTokens = 0.06f, YPath = 0.075f;

    private readonly GameSession _session;
    private readonly IconCache _icons;
    private readonly IconAtlas _atlas;
    private readonly WrapMesh _land, _landIcons, _overlay, _tokens, _tokenIcons, _path;
    private readonly MeshInstance3D _hover;
    private readonly StandardMaterial3D _opaque, _alpha, _landIconMaterial, _tokenIconMaterial, _tokenMaterial, _pathMaterial, _overlayMaterial;
    private readonly List<(Label3D Label, Vector3 Base)> _labels = new();
    private readonly List<Label3D> _labelPool = new();
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
        Period = (float)session.WrapPeriod;
        _opaque = Material(null, false, 0);
        _alpha = Material(null, true, 1);
        _overlayMaterial = Material(null, true, 1);
        _landIconMaterial = Material(_atlas.Texture, true, 2);
        _tokenMaterial = Material(null, true, 3);
        _tokenIconMaterial = Material(_atlas.Texture, true, 4);
        _pathMaterial = Material(null, true, 5);
        _land = Layer();
        _overlay = Layer();
        _landIcons = Layer();
        _tokens = Layer();
        _tokenIcons = Layer();
        _path = Layer();
        _hover = new MeshInstance3D { Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_hover);
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

    public static Vector3 WorldOf(Hex hex, float y = 0)
    {
        var (x, z) = hex.ToPixel(1);
        return new Vector3((float)x, y, (float)z);
    }

    /// <summary>Rebuild whatever the session changed since the last call.</summary>
    public void Refresh()
    {
        bool world = _worldRevision != _session.WorldRevision;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (world) { BuildLand(); _worldRevision = _session.WorldRevision; }
        long land = clock.ElapsedMilliseconds;
        if (!world && _revision == _session.Revision) return;
        BuildDynamic();
        _revision = _session.Revision;
        if (LogTimings) GD.Print($"map refresh: land {land} ms, tokens {clock.ElapsedMilliseconds - land} ms");
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

    public void ShowHover(Hex? hex, float focusX)
    {
        if (hex is not Hex h) { _hover.Visible = false; return; }
        if (_hover.Mesh == null)
        {
            var b = new MeshBuilder();
            for (int k = 0; k < 6; k++) b.Line(MeshBuilder.Corner(k, 0.97f), MeshBuilder.Corner(k + 1, 0.97f), 0.07f, new Color(1, 1, 1, 0.9f));
            _hover.Mesh = b.Build(_alpha);
        }
        var pos = WorldOf(h, YOverlay);
        if (Period > 0) pos.X += Mathf.Round((focusX - pos.X) / Period) * Period;
        _hover.Position = pos;
        _hover.Visible = true;
    }
}
