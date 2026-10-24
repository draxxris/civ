using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    private static readonly Color Ink = new(0.05f, 0.05f, 0.07f, 0.95f);

    private void BuildTokens()
    {
        var shapes = new MeshBuilder { Ground = GroundAt };
        var icons = new MeshBuilder { DefaultUv = _atlas.WhiteUv, Ground = GroundAt };
        foreach (var city in _session.KnownCities()) DrawCity(shapes, city);
        _standing.Clear();
        _selectionAt = null;
        foreach (var group in _session.VisibleUnits().Where(u => !_battleIds.Contains(u.Id)).GroupBy(u => u.Position))
            DrawUnitStack(shapes, icons, group.OrderBy(u => u.Id).ToList());
        SyncActors();
        _tokens.Mesh = shapes.Build(_tokenMaterial);
        _tokenIcons.Mesh = icons.Build(_tokenIconMaterial);
        ShowSelection(_selectionAt?.At, _selectionAt?.Radius ?? 0);
    }

    private (Vector3 At, float Radius)? _selectionAt;

    private void DrawCity(MeshBuilder b, City city)
    {
        var owner = Ui.ToColor(MapStyle.PlayerColor(city.Owner));
        var at = PositionOf(city.Position, YTokens);
        b.Ring(at, 0.62f, 0.05f, owner, 24);
        if (_session.City?.Id == city.Id) b.Ring(at, 0.8f, 0.07f, new Color(1f, 0.85f, 0.25f), 24);
        var info = _session.PlateOf(city);
        var plate = new CityNameplate(info, owner, _session.City?.Id == city.Id, _icons.TextureOf(info.ProductionId),
            WorldOf(city.Position, YTokens) + new Vector3(0, 0.3f, 0.95f));
        AddChild(plate);
        _plates.Add(plate);
    }

    private void DrawUnitStack(MeshBuilder shapes, MeshBuilder icons, List<Unit> stack)
    {
        var selected = stack.FirstOrDefault(u => u.Id == _session.SelectedUnitId);
        var top = selected ?? stack.OrderByDescending(u => _session.DefOf(u).Strength).First();
        var city = _session.Sim.State.CityAt(top.Position);
        bool inCity = city != null;
        var offset = _improvements.CityRing(city) != null ? new Vector3(0.25f, 0, 0.2f) : new Vector3(0.42f, 0, 0.36f);
        var at = PositionOf(top.Position, YTokens + 0.01f) + (inCity ? offset : Vector3.Zero);
        float radius = inCity ? 0.3f : 0.4f;
        var owner = Ui.ToColor(MapStyle.PlayerColor(top.Owner));
        float shade = top.Owner == GameSession.Human && top.MovesLeft <= 0 ? 0.62f : 1f;
        if (_models.Has(top.Type))
        {
            // The 3D model carries its own banner and health bar; the flat layer only adds selection and the stack count.
            var anchor = at;
            anchor.Y = GroundAt(anchor.X, anchor.Z) + 0.015f;
            _standing.Add((top, anchor, StackHealth(stack)));
            radius = 0.4f;
            if (selected != null) _selectionAt = (at, 0.44f);
        }
        else
        {
            shapes.Disc(at, radius + 0.06f, Ink, 16);
            if (selected != null) _selectionAt = (at, radius + 0.1f);
            shapes.Disc(at + Up(0.002f), radius, owner * shade, 16);
            if (_atlas.UvOf(top.Type) is Rect2 uv) icons.Icon(at + Up(0.03f), radius * 1.6f, uv, new Color(shade, shade, shade));
            DrawHealth(shapes, stack, at + new Vector3(0, 0.004f, radius + 0.16f));
        }
        if (stack.Count > 1) AddLabel($"×{stack.Count}", at + Up(GroundAt(at.X, at.Z)) + new Vector3(radius, 0.2f, -radius), 22, Colors.White);
    }

    /// <summary>The stack's average health as 0..1, or null while every unit is unhurt.</summary>
    private static float? StackHealth(List<Unit> stack)
    {
        float average = (float)stack.Average(u => u.Health);
        return average >= 100 ? null : average / 100f;
    }

    /// <summary>Flat ground bar for tokens without a 3D model; see <see cref="StackHealth"/>.</summary>
    private static void DrawHealth(MeshBuilder b, List<Unit> stack, Vector3 at)
    {
        if (StackHealth(stack) is not { } frac) return;
        float w = 0.64f;
        b.Quad(at + new Vector3(-w / 2 - 0.03f, 0, -0.07f), at + new Vector3(w / 2 + 0.03f, 0, -0.07f), at + new Vector3(w / 2 + 0.03f, 0, 0.07f), at + new Vector3(-w / 2 - 0.03f, 0, 0.07f), Ink);
        var fill = frac > 0.5f ? new Color(0.35f, 0.85f, 0.35f) : frac > 0.25f ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.9f, 0.25f, 0.2f);
        b.Quad(at + new Vector3(-w / 2, 0.001f, -0.045f), at + new Vector3(-w / 2 + w * frac, 0.001f, -0.045f), at + new Vector3(-w / 2 + w * frac, 0.001f, 0.045f), at + new Vector3(-w / 2, 0.001f, 0.045f), fill);
    }

    // --- city screen overlay --------------------------------------------------------------------

    /// <summary>Work-tile tints go to the terrain shader (<c>tileB.G</c>); the yield labels stay as text.</summary>
    private void BuildOverlay()
    {
        var marks = new List<(Hex, byte)>();
        foreach (var (hex, mark) in _session.TileMarks())
        {
            if (mark is TileMark.None or TileMark.Selected || _session.SightOf(hex) == Sight.Hidden) continue;
            marks.Add((hex, mark switch
            {
                TileMark.Worked => TileDataTexture.MarkWorked,
                TileMark.Workable => TileDataTexture.MarkWorkable,
                _ => TileDataTexture.MarkLocked,
            }));
            if (mark != TileMark.Locked) DrawYields(hex, mark);
        }
        _terrain.SetMarks(marks);
    }

    private void DrawYields(Hex hex, TileMark mark)
    {
        var at = WorldOf(hex, YOverlay);
        var y = _session.YieldsOf(hex);
        var text = string.Join("  ", new[] { (y.Food, "F"), (y.Production, "P"), (y.Commerce, "C") }.Where(p => p.Item1 != 0).Select(p => $"{p.Item1}{p.Item2}"));
        AddLabel(text.Length == 0 ? "-" : text, at + new Vector3(0, 0.25f, 0.55f), 20, mark == TileMark.Worked ? new Color(1f, 0.95f, 0.6f) : Colors.White);
    }
}
