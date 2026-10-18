using Civ.Game;
using Godot;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    /// <summary>Most a settlement layout turns either way from its authored pose, so fronts still face the camera.</summary>
    private const float ImprovementTurn = 0.45f;

    /// <summary>Feature marks, routes, improvement models and resource/improvement icons. The ground itself, relief, rivers
    /// and borders are the <see cref="TerrainRenderer"/>'s job.</summary>
    private void BuildLand()
    {
        var sim = _session.Sim;
        var marks = new MeshBuilder { Ground = GroundAt };
        var icons = new MeshBuilder { DefaultUv = _atlas.WhiteUv, Ground = GroundAt };
        var roads = new RoadMesh(TerrainField);
        var routeParts = new RouteParts(TerrainField);
        var sites = new List<ImprovementSite>();
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var sight = _session.SightOf(sim.State.Plots[tile.Value]);
            if (_session.PerceivedPlot(tile) is not { } plot) continue;
            var centre = PositionOf(sim.State.Grid.HexOf(tile));
            float fog = sight == Sight.Visible ? 1f : 0.55f;
            DrawFeature(marks, centre, tile.Value, plot, fog);
            DrawRoute(roads, routeParts, tile, centre, plot);
            DrawImprovementIcon(icons, centre, plot, fog);
            if (plot.Improvement is { } improvement && _improvements.Has(improvement))
                sites.Add(new ImprovementSite(improvement, centre, ImprovementYaw(improvement, tile.Value, plot.RiverEdges), fog));
        }
        foreach (var city in _session.KnownCities())
        {
            var sight = _session.SightOf(city.Position);
            if (sight == Sight.Hidden || _improvements.CityRing(city) is not { } ring) continue;
            sites.Add(new ImprovementSite(ring, PositionOf(city.Position), 0, sight == Sight.Visible ? 1f : 0.55f));
        }
        _improvements.Rebuild(sites);
        _cities.Rebuild(CitySites());
        _land.Mesh = marks.Build(_opaque);
        _roads.Mesh = roads.Build(_terrain.RoadMaterial);
        _routeParts.Mesh = routeParts.Build(_terrain.RouteStructureMaterial);
        _landIcons.Mesh = icons.Build(_landIconMaterial);
        _atlas.Flush();
    }

    /// <summary>Layout turn of an improvement: a model that faces the river (its +X side) is turned toward it, the rest get
    /// a hashed turn.</summary>
    private float ImprovementYaw(string improvement, int tile, byte riverEdges)
    {
        if (riverEdges != 0 && _improvements.FacesRiver(improvement)) return RiverYaw(riverEdges);
        return Signed(Hash(tile, 7)) * ImprovementTurn;
    }

    /// <summary>Yaw that turns a model's +X side toward the river on a tile. Edge bits follow <c>Hex.Directions</c>. One
    /// edge: that edge. Several: the mean of their directions, so an adjacent pair faces the bend between them. When the
    /// directions cancel (opposite edges, three at 120°) the lowest edge is used.</summary>
    internal static float RiverYaw(byte riverEdges)
    {
        double sumX = 0, sumZ = 0;
        int first = -1;
        for (int d = 0; d < 6; d++)
        {
            if ((riverEdges & (1 << d)) == 0) continue;
            if (first < 0) first = d;
            var (x, z) = Hex.Directions[d].ToPixel(1);
            sumX += x; sumZ += z;
        }
        if (first < 0) return 0;
        if (Math.Abs(sumX) + Math.Abs(sumZ) < 0.01) (sumX, sumZ) = Hex.Directions[first].ToPixel(1);
        // Basis(Up, yaw) turns +X to (cos yaw, 0, -sin yaw).
        return (float)Math.Atan2(-sumZ, sumX);
    }

    private static int Hash(int tile, int salt)
    {
        uint x = (uint)(tile * 73856093 ^ salt * 19349663);
        x ^= x >> 13; x *= 0x5bd1e995; x ^= x >> 15;
        return (int)(x & 0x7fffffff);
    }

    private static Vector3 Scatter(int tile, int index, float radius)
    {
        float a = Hash(tile, index * 2) % 628 / 100f;
        float r = radius * (0.35f + 0.65f * (Hash(tile, index * 2 + 1) % 100) / 100f);
        return new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r * 0.9f);
    }

    private static void Mark(MeshBuilder b, Vector3 at, float width, float height, Color body, Color light)
    {
        var baseLeft = at + new Vector3(-width / 2, YRelief, 0);
        var baseRight = at + new Vector3(width / 2, YRelief, 0);
        var top = at + new Vector3(0, YRelief, -height);
        b.Tri(baseLeft, baseRight, top, body);
        b.Tri(baseLeft, top, at + new Vector3(0, YRelief, 0), light);
    }

    private void DrawFeature(MeshBuilder b, Vector3 centre, int tile, RevealedPlotSnapshot plot, float fog)
    {
        if (plot.Feature is not { } id) return;
        if (_terrain.Palette.ScatterFor(id, plot.Terrain)?.Scatter is not null) return;
        if (MapStyle.FeatureOf(_session.Sim.Content, id) is not { } style || style.Marks == 0) return;
        var body = Ui.ToColor(style.Tint.Scale(0.8f * fog));
        var light = Ui.ToColor(style.Tint.Lerp(Rgb.Hex(0xffffff), 0.25f).Scale(0.85f * fog));
        for (int i = 0; i < style.Marks; i++)
            Mark(b, centre + Scatter(tile, i, 0.62f) + new Vector3(0, 0, 0.12f), 0.24f, 0.32f, body, light);
    }

    private static Vector3 Up(float y) => new(0, y, 0);

    private static float Signed(int hash) => hash % 2001 / 1000f - 1f;

    private void DrawImprovementIcon(MeshBuilder icons, Vector3 centre, RevealedPlotSnapshot plot, float fog)
    {
        var tint = new Color(fog, fog, fog);
        if (plot.Improvement is { } improvement && !_improvements.Has(improvement)) DrawIcon(icons, improvement, centre + new Vector3(0.34f, YIcons, 0.26f), 0.56f, tint, new Color(0.7f, 0.55f, 0.4f));
    }

    private void BuildResources()
    {
        var icons = new MeshBuilder { DefaultUv = _atlas.WhiteUv, Ground = GroundAt };
        foreach (var tile in _session.Sim.State.Grid.Tiles)
        {
            // Do not calculate yields when building resource icons.
            if (_session.PerceivedPlot(tile)?.Bonus is not { } bonus || !_session.BonusVisible(bonus)) continue;
            var centre = PositionOf(_session.Sim.State.Grid.HexOf(tile));
            float fog = _session.SightOf(_session.Sim.State.Plots[tile.Value]) == Sight.Visible ? 1f : 0.55f;
            DrawIcon(icons, bonus, centre + new Vector3(-0.34f, YIcons, -0.26f), 0.56f,
                new Color(fog, fog, fog), new Color(0.95f, 0.8f, 0.2f));
        }
        _resources.Mesh = icons.Build(_landIconMaterial);
        _atlas.Flush();
    }

    // Tiny F/P/C numerals are geometry in one atlas-backed mesh, not one scene node per tile.
    private static readonly IReadOnlyDictionary<char, string> YieldGlyphs = new Dictionary<char, string>
    {
        ['0'] = "111101101101111", ['1'] = "010110010010111", ['2'] = "111001111100111",
        ['3'] = "111001111001111", ['4'] = "101101111001001", ['5'] = "111100111001111",
        ['6'] = "111100111101111", ['7'] = "111001010010010", ['8'] = "111101111101111",
        ['9'] = "111101111001111", ['F'] = "111100110100100", ['P'] = "110101110100100",
        ['C'] = "111100100100111",
    };

    private void BuildYieldBadges()
    {
        var badges = new MeshBuilder { DefaultUv = _atlas.WhiteUv };
        foreach (var tile in _session.Sim.State.Grid.Tiles)
        {
            if (_session.OverlayOf(tile)?.Yields is not { } y) continue;
            var centre = PositionOf(_session.Sim.State.Grid.HexOf(tile), YOverlay);
            DrawYieldBadge(badges, centre + new Vector3(-0.48f, 0, 0.48f), y.Food, 'F', new Color(0.45f, 1f, 0.4f));
            DrawYieldBadge(badges, centre + new Vector3(0, 0, 0.48f), y.Production, 'P', new Color(1f, 0.65f, 0.35f));
            DrawYieldBadge(badges, centre + new Vector3(0.48f, 0, 0.48f), y.Commerce, 'C', new Color(1f, 0.92f, 0.3f));
        }
        _yieldBadges.Mesh = badges.Build(_landIconMaterial);
    }

    private void DrawYieldBadge(MeshBuilder mesh, Vector3 centre, long value, char kind, Color tint)
    {
        if (value <= 0) return;
        string text = value.ToString(System.Globalization.CultureInfo.InvariantCulture) + kind;
        float pixel = Mathf.Min(0.045f, 0.43f / (text.Length * 4));
        float width = (text.Length * 4 - 1) * pixel, height = 5 * pixel;
        float support = float.NegativeInfinity;
        for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
                support = Mathf.Max(support, GroundAt(centre.X + x * width / 2, centre.Z + z * height / 2));
        centre.Y += support;
        var start = centre - new Vector3(width / 2, 0, height / 2);
        mesh.Quad(start, start + new Vector3(width, 0, 0), start + new Vector3(width, 0, height),
            start + new Vector3(0, 0, height), new Color(0.03f, 0.04f, 0.05f, 0.88f));
        for (int c = 0; c < text.Length; c++)
            DrawYieldGlyph(mesh, start + new Vector3(c * 4 * pixel, 0.003f, 0), text[c], pixel, tint);
    }

    private static void DrawYieldGlyph(MeshBuilder mesh, Vector3 start, char glyph, float pixel, Color tint)
    {
        string bits = YieldGlyphs[glyph];
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i] != '1') continue;
            var p = start + new Vector3(i % 3 * pixel, 0, i / 3 * pixel);
            mesh.Quad(p, p + new Vector3(pixel, 0, 0), p + new Vector3(pixel, 0, pixel), p + new Vector3(0, 0, pixel), tint);
        }
    }

    private void DrawIcon(MeshBuilder icons, string id, Vector3 at, float size, Color tint, Color fallback)
    {
        if (_atlas.UvOf(id) is Rect2 uv) icons.Icon(at, size, uv, tint);
        else icons.Disc(at, size * 0.28f, fallback * tint, 10);
    }
}
