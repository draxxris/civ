using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    private static readonly Color Ink = new(0.05f, 0.05f, 0.07f, 0.95f);

    private void BuildTokens()
    {
        var shapes = new MeshBuilder();
        var icons = new MeshBuilder { DefaultUv = _atlas.WhiteUv };
        foreach (var city in _session.KnownCities()) DrawCity(shapes, city);
        foreach (var group in _session.VisibleUnits().GroupBy(u => u.Position)) DrawUnitStack(shapes, icons, group.OrderBy(u => u.Id).ToList());
        _tokens.Mesh = shapes.Build(_tokenMaterial);
        _tokenIcons.Mesh = icons.Build(_tokenIconMaterial);
    }

    private void DrawCity(MeshBuilder b, City city)
    {
        var owner = Ui.ToColor(MapStyle.PlayerColor(city.Owner));
        var at = WorldOf(city.Position, YTokens);
        b.Disc(at, 0.62f, Ink, 16);
        b.Disc(at, 0.55f, owner, 16);
        b.Quad(at + new Vector3(-0.3f, 0.002f, -0.02f), at + new Vector3(0.3f, 0.002f, -0.02f), at + new Vector3(0.3f, 0.002f, 0.3f), at + new Vector3(-0.3f, 0.002f, 0.3f), new Color(0.93f, 0.9f, 0.82f));
        b.Tri(at + new Vector3(-0.36f, 0.003f, -0.02f), at + new Vector3(0.36f, 0.003f, -0.02f), at + new Vector3(0, 0.003f, -0.4f), owner.Darkened(0.45f));
        b.Quad(at + new Vector3(-0.07f, 0.004f, 0.14f), at + new Vector3(0.07f, 0.004f, 0.14f), at + new Vector3(0.07f, 0.004f, 0.3f), at + new Vector3(-0.07f, 0.004f, 0.3f), Ink);
        if (_session.City?.Id == city.Id) b.Ring(at, 0.8f, 0.07f, new Color(1f, 0.85f, 0.25f), 24);
        AddLabel($"{city.Name}  {city.Population}", at + new Vector3(0, 0.25f, 0.9f), 24, Colors.White);
    }

    private void DrawUnitStack(MeshBuilder shapes, MeshBuilder icons, List<Unit> stack)
    {
        var selected = stack.FirstOrDefault(u => u.Id == _session.SelectedUnitId);
        var top = selected ?? stack.OrderByDescending(u => _session.DefOf(u).Strength).First();
        bool inCity = _session.Sim.State.CityAt(top.Position) != null;
        var at = WorldOf(top.Position, YTokens + 0.01f) + (inCity ? new Vector3(0.42f, 0, 0.36f) : Vector3.Zero);
        float radius = inCity ? 0.3f : 0.4f;
        var owner = Ui.ToColor(MapStyle.PlayerColor(top.Owner));
        float shade = top.Owner == GameSession.Human && top.MovesLeft <= 0 ? 0.62f : 1f;
        shapes.Disc(at, radius + 0.06f, selected != null ? new Color(1f, 0.9f, 0.3f) : Ink, 16);
        shapes.Disc(at + Up(0.002f), radius, owner * shade, 16);
        if (_atlas.UvOf(top.Type) is Rect2 uv) icons.Icon(at + Up(0.03f), radius * 1.6f, uv, new Color(shade, shade, shade));
        DrawHealth(shapes, top, at + new Vector3(0, 0.004f, radius + 0.16f));
        if (stack.Count > 1) AddLabel($"×{stack.Count}", at + new Vector3(radius, 0.2f, -radius), 22, Colors.White);
    }

    private static void DrawHealth(MeshBuilder b, Unit unit, Vector3 at)
    {
        if (unit.Health >= 100) return;
        float w = 0.64f, frac = Mathf.Clamp(unit.Health / 100f, 0, 1);
        b.Quad(at + new Vector3(-w / 2 - 0.03f, 0, -0.07f), at + new Vector3(w / 2 + 0.03f, 0, -0.07f), at + new Vector3(w / 2 + 0.03f, 0, 0.07f), at + new Vector3(-w / 2 - 0.03f, 0, 0.07f), Ink);
        var fill = frac > 0.5f ? new Color(0.35f, 0.85f, 0.35f) : frac > 0.25f ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.9f, 0.25f, 0.2f);
        b.Quad(at + new Vector3(-w / 2, 0.001f, -0.045f), at + new Vector3(-w / 2 + w * frac, 0.001f, -0.045f), at + new Vector3(-w / 2 + w * frac, 0.001f, 0.045f), at + new Vector3(-w / 2, 0.001f, 0.045f), fill);
    }

    // --- city screen overlay --------------------------------------------------------------------

    private void BuildOverlay()
    {
        var b = new MeshBuilder();
        foreach (var (hex, mark) in _session.TileMarks()) DrawWorkTile(b, hex, mark);
        _overlay.Mesh = b.Build(_overlayMaterial);
    }

    private void DrawWorkTile(MeshBuilder b, Hex hex, TileMark mark)
    {
        if (mark is TileMark.None or TileMark.Selected || _session.SightOf(hex) == Sight.Hidden) return;
        var at = WorldOf(hex, YOverlay);
        var colour = mark switch
        {
            TileMark.Worked => new Color(1f, 0.6f, 0.1f, 0.42f),
            TileMark.Workable => new Color(1f, 1f, 1f, 0.16f),
            _ => new Color(0f, 0f, 0f, 0.5f),
        };
        b.Hex(at, 0.97f, colour, colour);
        if (mark == TileMark.Locked) return;
        var y = _session.YieldsOf(hex);
        var text = string.Join("  ", new[] { (y.Food, "F"), (y.Production, "P"), (y.Commerce, "C") }.Where(p => p.Item1 != 0).Select(p => $"{p.Item1}{p.Item2}"));
        AddLabel(text.Length == 0 ? "-" : text, at + new Vector3(0, 0.25f, 0.55f), 20, mark == TileMark.Worked ? new Color(1f, 0.95f, 0.6f) : Colors.White);
    }

    // --- path preview ------------------------------------------------------------------------------

    /// <summary>Draw the route a right click would take, with the final step marked red for an attack.</summary>
    public void ShowPath(Unit? unit, MovePreview? preview)
    {
        if (unit == null || preview == null) { _path.Mesh = null; return; }
        var b = new MeshBuilder();
        var colour = preview.Attack != null ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(0.4f, 1f, 0.5f, 0.9f);
        var previous = WorldOf(unit.Position, YPath);
        foreach (var step in preview.Path)
        {
            var next = WorldOf(step, YPath);
            if (Period > 0) next.X += Mathf.Round((previous.X - next.X) / Period) * Period;
            b.Line(previous, next, 0.12f, colour);
            b.Disc(next, 0.1f, colour);
            previous = next;
        }
        b.Ring(previous, 0.42f, 0.07f, colour);
        _path.Mesh = b.Build(_pathMaterial);
    }
}
