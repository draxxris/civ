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

    private void DrawRoute(MeshBuilder b, TileId tile, Vector3 centre, Plot plot, float fog)
    {
        if (plot.Route == null) return;
        var colour = Ui.ToColor(MapStyle.Road.Scale(fog));
        var grid = _session.Sim.State.Grid;
        for (int d = 0; d < 6; d++)
        {
            if (grid.Neighbor(tile, d) is not TileId n || _session.Sim.State.Plots[n.Value].Route == null) continue;
            var to = (MeshBuilder.EdgeCorner(d, 0, 0.87f) + MeshBuilder.EdgeCorner(d, 1, 0.87f)) / 2 * 1f;
            b.Line(centre + Up(YLines), centre + to + Up(YLines), 0.1f, colour);
        }
        b.Disc(centre + Up(YLines), 0.1f, colour, 8);
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
