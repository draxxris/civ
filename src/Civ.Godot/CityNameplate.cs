using Godot;

namespace Civ.Client;

/// <summary>
/// A city's floating nameplate: the population in an owner-coloured medallion, the name, a food bar toward the next
/// citizen, a production bar toward the current build, and that build's icon on the right. Faces the camera, and keeps
/// the camera's orientation so every part sits in screen space.
/// </summary>
internal sealed partial class CityNameplate : Node3D
{
    private const float HalfWidth = 0.66f, HalfHeight = 0.19f, Chamfer = 0.06f, Edge = 0.026f, Rim = 0.018f;
    private const float BarLeft = -0.4f, BarRight = 0.3f, BarHalf = 0.019f;
    private static readonly Color Ink = new(0.06f, 0.05f, 0.05f, 0.9f), Trough = new(0.1f, 0.09f, 0.09f, 0.95f);
    private static readonly Color Parchment = new(0.2f, 0.17f, 0.13f, 0.88f);
    private static readonly Color Food = new(0.45f, 0.85f, 0.3f), Hammer = new(0.95f, 0.6f, 0.18f);

    private static readonly StandardMaterial3D Flat = MakeFlat();

    /// <summary>Where the plate stands before any wrap-around shift.</summary>
    public Vector3 Anchor { get; }

    public CityNameplate(CityPlate plate, Color owner, bool selected, Texture2D? icon, Vector3 anchor)
    {
        Anchor = anchor;
        Position = anchor;
        AddChild(new MeshInstance3D
        {
            Name = "Plate", Mesh = Build(plate, owner, selected), MaterialOverride = Flat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        AddChild(Text(plate.Population.ToString(), 64, new Vector3(-HalfWidth + 0.03f, 0, 0.001f), 0.0026f, 13, true));
        AddChild(Text(plate.Name, 56, new Vector3(0.03f, 0.065f, 0.001f), 0.0027f, 13, true));
        if (plate.ProductionId != null && icon != null)
            AddChild(new Sprite3D
            {
                Texture = icon, PixelSize = 0.27f / Math.Max(icon.GetWidth(), 1), Position = new Vector3(HalfWidth + 0.02f, 0, 0.001f),
                Shaded = false, NoDepthTest = true, RenderPriority = 12, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            });
    }

    public override void _Process(double delta)
    {
        if (GetViewport()?.GetCamera3D() is { } camera) GlobalBasis = camera.GlobalBasis;
    }

    private static Label3D Text(string text, int size, Vector3 at, float pixel, int priority, bool outline) => new()
    {
        Text = text, FontSize = size, PixelSize = pixel, Position = at, NoDepthTest = true, RenderPriority = priority,
        OutlineSize = outline ? 12 : 0, OutlineModulate = new Color(0, 0, 0, 0.9f), Modulate = Colors.White,
    };

    private static Mesh Build(CityPlate plate, Color owner, bool selected)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var shape = Plate(HalfWidth, HalfHeight);
        var rim = selected ? new Color(1f, 0.92f, 0.45f) : UnitActor.GoldLight;
        UnitActor.Band(st, UnitActor.Offset(shape, Edge), UnitActor.Offset(shape, Rim), _ => Ink);
        UnitActor.Band(st, UnitActor.Offset(shape, Rim), shape, y => rim.Lerp(UnitActor.GoldDark, Mathf.InverseLerp(HalfHeight, -HalfHeight, y)));
        UnitActor.Fan(st, shape, y => Parchment.Lerp(Parchment.Darkened(0.4f), Mathf.InverseLerp(HalfHeight, -HalfHeight, y)));
        // A thin owner-coloured strip under the top edge ties the plate to its civilization.
        UnitActor.Fan(st, Rect(-HalfWidth + 0.2f, HalfWidth - 0.1f, HalfHeight - 0.045f, HalfHeight - 0.015f), _ => owner with { A = 0.95f });

        Medallion(st, owner, new Vector2(-HalfWidth, 0));
        Bar(st, -0.07f, plate.Growth, Food);
        Bar(st, -0.125f, plate.Production ?? 0f, Hammer);
        if (plate.ProductionId != null) IconFrame(st, new Vector2(HalfWidth + 0.02f, 0));
        return st.Commit();
    }

    private static void Medallion(SurfaceTool st, Color owner, Vector2 at)
    {
        var outer = Circle(at, 0.215f);

        UnitActor.Fan(st, outer, _ => Ink);
        var gold = Circle(at, 0.195f);
        UnitActor.Fan(st, gold, y => UnitActor.GoldLight.Lerp(UnitActor.GoldDark, Mathf.InverseLerp(at.Y + 0.2f, at.Y - 0.2f, y)));
        var fill = Circle(at, 0.165f);
        UnitActor.Fan(st, fill, y => owner.Lightened(0.3f).Lerp(owner.Darkened(0.5f), Mathf.InverseLerp(at.Y + 0.165f, at.Y - 0.165f, y)) with { A = 0.98f });
    }

    private static void IconFrame(SurfaceTool st, Vector2 at)
    {
        const float r = 0.17f;
        UnitActor.Fan(st, Rect(at.X - r - 0.03f, at.X + r + 0.03f, at.Y - r - 0.03f, at.Y + r + 0.03f), _ => Ink);
        UnitActor.Fan(st, Rect(at.X - r - 0.015f, at.X + r + 0.015f, at.Y - r - 0.015f, at.Y + r + 0.015f), y => UnitActor.GoldLight.Lerp(UnitActor.GoldDark, Mathf.InverseLerp(at.Y + r, at.Y - r, y)));
        UnitActor.Fan(st, Rect(at.X - r, at.X + r, at.Y - r, at.Y + r), _ => Trough);
    }

    private static void Bar(SurfaceTool st, float y, float fraction, Color colour)
    {
        UnitActor.Fan(st, Rect(BarLeft - 0.012f, BarRight + 0.012f, y - BarHalf - 0.012f, y + BarHalf + 0.012f), _ => Ink);
        UnitActor.Fan(st, Rect(BarLeft, BarRight, y - BarHalf, y + BarHalf), _ => Trough);
        if (fraction <= 0) return;
        float right = BarLeft + (BarRight - BarLeft) * Math.Clamp(fraction, 0f, 1f);
        UnitActor.Fan(st, Rect(BarLeft, right, y - BarHalf, y + BarHalf),
            v => colour.Lightened(0.2f).Lerp(colour.Darkened(0.35f), Mathf.InverseLerp(y + BarHalf, y - BarHalf, v)));
    }

    private static Vector2[] Rect(float left, float right, float bottom, float top) =>
        [new(left, bottom), new(right, bottom), new(right, top), new(left, top)];

    private static Vector2[] Circle(Vector2 at, float r)
    {
        var points = new Vector2[20];
        for (int i = 0; i < points.Length; i++)
        {
            float a = Mathf.Tau * i / points.Length;
            points[i] = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return points;
    }

    /// <summary>A wide box with chamfered corners. Counter-clockwise, convex.</summary>
    private static Vector2[] Plate(float w, float h) =>
    [
        new(-w + Chamfer, -h), new(w - Chamfer, -h), new(w, -h + Chamfer),
        new(w, h - Chamfer), new(w - Chamfer, h), new(-w + Chamfer, h), new(-w, h - Chamfer), new(-w, -h + Chamfer),
    ];

    private static StandardMaterial3D MakeFlat()
    {
        var m = (StandardMaterial3D)UnitActor.OverlayMaterial.Duplicate();
        m.BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled;
        m.RenderPriority = 11;
        return m;
    }
}
