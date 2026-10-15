using Civ.Game;
using Godot;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    /// <summary>Feature marks, routes and resource/improvement icons. The ground itself, relief, rivers and borders are the
    /// <see cref="TerrainRenderer"/>'s job.</summary>
    private void BuildLand()
    {
        var sim = _session.Sim;
        var marks = new MeshBuilder { Ground = GroundAt };
        var icons = new MeshBuilder { DefaultUv = _atlas.WhiteUv, Ground = GroundAt };
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var plot = sim.State.Plots[tile.Value];
            var sight = _session.SightOf(plot);
            if (sight == Sight.Hidden) continue;
            var centre = PositionOf(sim.State.Grid.HexOf(tile));
            float fog = sight == Sight.Visible ? 1f : 0.55f;
            DrawFeature(marks, centre, tile.Value, plot, fog);
            DrawRoute(marks, tile, centre, plot, fog);
            DrawResources(icons, centre, plot, fog);
        }
        _land.Mesh = marks.Build(_opaque);
        _landIcons.Mesh = icons.Build(_landIconMaterial);
        _atlas.Flush();
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

    private void DrawFeature(MeshBuilder b, Vector3 centre, int tile, Plot plot, float fog)
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

    private const float RoadVergeWidth = 0.12f, RoadTrackWidth = 0.065f, RoadTrackLift = 0.006f;
    /// <summary>Largest sideways bow of a road link, and of its S-shaped second harmonic, in world units.</summary>
    private const float RoadBow = 0.17f, RoadWiggle = 0.08f;
    /// <summary>Ribbon segments per half link (tile centre to shared edge).</summary>
    private const int RoadSteps = 8;

    /// <summary>Each road link between two tile centres is one gently swerving curve: a bow plus a weaker S-bend, both
    /// zero at the centres. The bend is hashed from the unordered tile pair, so each tile draws its own half (in its own
    /// fog) and the halves meet exactly at the shared edge with the same tangent.</summary>
    private void DrawRoute(MeshBuilder b, TileId tile, Vector3 centre, Plot plot, float fog)
    {
        if (plot.Route == null) return;
        var verge = Ui.ToColor(MapStyle.Road.Scale(fog));
        var track = Ui.ToColor(MapStyle.RoadTrack.Scale(fog));
        var grid = _session.Sim.State.Grid;
        var hex = grid.HexOf(tile);
        for (int d = 0; d < 6; d++)
        {
            if (grid.Neighbor(tile, d) is not TileId n || n == tile || _session.Sim.State.Plots[n.Value].Route == null) continue;
            // Unwrapped neighbour centre, so a link across the wrap seam stays short.
            var other = PositionOf(hex.Neighbor(d));
            bool first = tile.Value < n.Value;
            var link = new RoadLink(first ? centre : other, first ? other : centre,
                Signed(Hash(Math.Min(tile.Value, n.Value), 2 * Math.Max(tile.Value, n.Value) + 1)) * RoadBow,
                Signed(Hash(Math.Min(tile.Value, n.Value), 2 * Math.Max(tile.Value, n.Value) + 2)) * RoadWiggle);
            float t0 = first ? 0f : 0.5f;
            DrawRoadHalf(b, link, t0, RoadVergeWidth, YLines, verge);
            DrawRoadHalf(b, link, t0, RoadTrackWidth, YLines + RoadTrackLift, track);
        }
        b.Disc(centre + Up(YLines), RoadVergeWidth / 2, verge, 10);
        b.Disc(centre + Up(YLines + RoadTrackLift), RoadTrackWidth / 2, track, 10);
    }

    private static float Signed(int hash) => hash % 2001 / 1000f - 1f;

    /// <summary>A road curve from <c>From</c> (t = 0) to <c>To</c> (t = 1) with a sideways offset
    /// <c>Bow·sin(πt) + Wiggle·sin(2πt)</c>.</summary>
    private readonly record struct RoadLink(Vector3 From, Vector3 To, float Bow, float Wiggle)
    {
        private Vector3 Normal => new Vector3(-(To.Z - From.Z), 0, To.X - From.X).Normalized();

        public Vector3 At(float t) =>
            From.Lerp(To, t) + Normal * (Bow * Mathf.Sin(Mathf.Pi * t) + Wiggle * Mathf.Sin(Mathf.Tau * t));

        public Vector3 Tangent(float t) =>
            To - From + Normal * (Bow * Mathf.Pi * Mathf.Cos(Mathf.Pi * t) + Wiggle * Mathf.Tau * Mathf.Cos(Mathf.Tau * t));
    }

    /// <summary>A draped ribbon along half a link, from t0 to t0 + ½, with analytic sides so the two halves join cleanly.</summary>
    private static void DrawRoadHalf(MeshBuilder b, RoadLink link, float t0, float width, float lift, Color colour)
    {
        Vector3 prevPoint = default, prevSide = default;
        for (int i = 0; i <= RoadSteps; i++)
        {
            float t = t0 + 0.5f * i / RoadSteps;
            var tangent = link.Tangent(t);
            var side = new Vector3(-tangent.Z, 0, tangent.X).Normalized() * (width / 2);
            var point = link.At(t) + Up(lift);
            if (i > 0) b.Quad(prevPoint - prevSide, prevPoint + prevSide, point + side, point - side, colour);
            prevPoint = point;
            prevSide = side;
        }
    }

    private void DrawResources(MeshBuilder icons, Vector3 centre, Plot plot, float fog)
    {
        var tint = new Color(fog, fog, fog);
        if (plot.Bonus is { } bonus && _session.BonusVisible(bonus)) DrawIcon(icons, bonus, centre + new Vector3(-0.34f, YIcons, -0.26f), 0.56f, tint, new Color(0.95f, 0.8f, 0.2f));
        if (plot.Improvement is { } improvement) DrawIcon(icons, improvement, centre + new Vector3(0.34f, YIcons, 0.26f), 0.56f, tint, new Color(0.7f, 0.55f, 0.4f));
    }

    private void DrawIcon(MeshBuilder icons, string id, Vector3 at, float size, Color tint, Color fallback)
    {
        if (_atlas.UvOf(id) is Rect2 uv) icons.Icon(at, size, uv, tint);
        else icons.Disc(at, size * 0.28f, fallback * tint, 10);
    }
}
