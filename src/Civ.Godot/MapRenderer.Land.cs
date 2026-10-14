using Civ.Game;
using Godot;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    private static readonly Color Black = new(0.02f, 0.02f, 0.03f);

    private void BuildLand()
    {
        var sim = _session.Sim;
        var terrain = new MeshBuilder();
        var icons = new MeshBuilder { DefaultUv = _atlas.WhiteUv };
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var hex = sim.State.Grid.HexOf(tile);
            var plot = sim.State.Plots[tile.Value];
            var sight = _session.SightOf(plot);
            var centre = WorldOf(hex);
            if (sight == Sight.Hidden) { terrain.Hex(centre, 1.01f, Black, Black); continue; }
            float fog = sight == Sight.Visible ? 1f : 0.55f;
            DrawTerrain(terrain, centre, plot, fog);
            DrawRelief(terrain, centre, tile.Value, plot, fog);
            DrawFeature(terrain, centre, tile.Value, plot, fog);
            DrawEdges(terrain, tile, hex, plot, fog);
            DrawRoute(terrain, tile, centre, plot, fog);
            DrawResources(icons, centre, plot, fog);
        }
        _land.Mesh = terrain.Build(_opaque);
        _landIcons.Mesh = icons.Build(_landIconMaterial);
        _atlas.Flush();
    }

    private Color TerrainTone(Plot plot, float fog)
    {
        var colour = MapStyle.TerrainColor(_session.Sim.Content, plot.Terrain);
        if (plot.Feature is { } f && MapStyle.FeatureOf(_session.Sim.Content, f) is { } style) colour = colour.Lerp(style.Tint, style.TintStrength);
        if (plot.Owner is int owner) colour = colour.Lerp(MapStyle.PlayerColor(owner), 0.14f);
        return Ui.ToColor(colour.Scale(fog));
    }

    private void DrawTerrain(MeshBuilder b, Vector3 centre, Plot plot, float fog)
    {
        var tone = TerrainTone(plot, fog);
        b.Hex(centre, 1.01f, tone.Lightened(0.06f), tone.Darkened(0.07f));
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

    private void DrawRelief(MeshBuilder b, Vector3 centre, int tile, Plot plot, float fog)
    {
        if (plot.Kind == PlotKind.Flat) return;
        var ground = MapStyle.TerrainColor(_session.Sim.Content, plot.Terrain).Scale(fog);
        if (plot.Kind == PlotKind.Hills)
        {
            Mark(b, centre + new Vector3(-0.22f, 0, 0.18f), 0.62f, 0.42f, Ui.ToColor(ground.Scale(0.68f)), Ui.ToColor(ground.Scale(0.9f)));
            Mark(b, centre + new Vector3(0.24f, 0, 0.26f), 0.5f, 0.34f, Ui.ToColor(ground.Scale(0.62f)), Ui.ToColor(ground.Scale(0.85f)));
            return;
        }
        var rock = new Color(0.46f, 0.46f, 0.5f) * fog;
        Mark(b, centre + new Vector3(0, 0, 0.3f), 0.95f, 0.85f, rock.Darkened(0.2f), rock.Lightened(0.1f));
        Mark(b, centre + new Vector3(0, 0, -0.38f), 0.26f, 0.2f, new Color(1, 1, 1) * fog, new Color(0.9f, 0.93f, 1f) * fog);
    }

    private void DrawFeature(MeshBuilder b, Vector3 centre, int tile, Plot plot, float fog)
    {
        if (plot.Feature is not { } id || MapStyle.FeatureOf(_session.Sim.Content, id) is not { } style || style.Marks == 0) return;
        var body = Ui.ToColor(style.Tint.Scale(0.8f * fog));
        var light = Ui.ToColor(style.Tint.Lerp(Rgb.Hex(0xffffff), 0.25f).Scale(0.85f * fog));
        for (int i = 0; i < style.Marks; i++)
            Mark(b, centre + Scatter(tile, i, 0.62f) + new Vector3(0, 0, 0.12f), 0.24f, 0.32f, body, light);
    }

    private void DrawEdges(MeshBuilder b, TileId tile, Hex hex, Plot plot, float fog)
    {
        var grid = _session.Sim.State.Grid;
        var centre = WorldOf(hex);
        for (int d = 0; d < 6; d++)
        {
            if ((plot.RiverEdges & (1 << d)) != 0)
                b.Line(centre + MeshBuilder.EdgeCorner(d, 0, 1f) + Up(YLines), centre + MeshBuilder.EdgeCorner(d, 1, 1f) + Up(YLines), 0.14f, Ui.ToColor(MapStyle.River.Scale(fog)));
            if (plot.Owner is not int owner) continue;
            var neighbour = grid.Neighbor(tile, d);
            if (neighbour is TileId n && _session.Sim.State.Plots[n.Value].Owner != owner)
                b.Line(centre + MeshBuilder.EdgeCorner(d, 0, 0.93f) + Up(YLines + 0.004f), centre + MeshBuilder.EdgeCorner(d, 1, 0.93f) + Up(YLines + 0.004f), 0.1f, Ui.ToColor(MapStyle.PlayerColor(owner).Scale(fog)));
        }
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
