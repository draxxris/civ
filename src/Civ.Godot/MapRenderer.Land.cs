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
            DrawResources(icons, centre, plot, fog);
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

    private void DrawResources(MeshBuilder icons, Vector3 centre, RevealedPlotSnapshot plot, float fog)
    {
        var tint = new Color(fog, fog, fog);
        if (plot.Bonus is { } bonus && _session.BonusVisible(bonus)) DrawIcon(icons, bonus, centre + new Vector3(-0.34f, YIcons, -0.26f), 0.56f, tint, new Color(0.95f, 0.8f, 0.2f));
        if (plot.Improvement is { } improvement && !_improvements.Has(improvement)) DrawIcon(icons, improvement, centre + new Vector3(0.34f, YIcons, 0.26f), 0.56f, tint, new Color(0.7f, 0.55f, 0.4f));
    }

    private void DrawIcon(MeshBuilder icons, string id, Vector3 at, float size, Color tint, Color fallback)
    {
        if (_atlas.UvOf(id) is Rect2 uv) icons.Icon(at, size, uv, tint);
        else icons.Disc(at, size * 0.28f, fallback * tint, 10);
    }
}
