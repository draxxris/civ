using Godot;

namespace Civ.Client;

/// <summary>The unit's floating badges: the faction banner above it and the health bar at its feet. Both face the camera.</summary>
internal sealed partial class UnitActor
{
    private const float BannerHeight = 1.12f, BannerHalfWidth = 0.12f, BannerHalfHeight = 0.16f;
    private const float BannerEdge = 0.034f, BannerRim = 0.024f, BannerLine = 0.007f, BannerAlpha = 0.82f;
    private const float BarHalfWidth = 0.25f, BarHalfHeight = 0.03f, BarEdge = 0.022f, BarRim = 0.014f, BarLine = 0.005f;
    private const float BarHeight = -0.04f;
    private static readonly Color BannerInk = new(0.06f, 0.05f, 0.05f, 0.9f);
    private static readonly Color GoldLight = new(1f, 0.86f, 0.42f, 0.92f), GoldDark = new(0.48f, 0.3f, 0.08f, 0.92f);
    private static readonly Color Trough = new(0.08f, 0.07f, 0.07f, 0.72f);
    private static readonly Color Healthy = new(0.36f, 0.86f, 0.3f), Wounded = new(0.98f, 0.78f, 0.2f), Dying = new(0.9f, 0.18f, 0.14f);

    /// <summary>Unshaded, camera-facing, drawn over terrain and models; vertex colours are authored in sRGB.</summary>
    private static readonly StandardMaterial3D OverlayMaterial = new()
    {
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, RenderPriority = 10,
    };

    private readonly MeshInstance3D _banner;
    private readonly MeshInstance3D _health = new()
    {
        Name = "Health", Position = new Vector3(0, BarHeight, 0), Visible = false,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = OverlayMaterial,
    };
    private float _shownHealth = -1;

    /// <summary>Show a health bar for a 0..1 fraction, or hide it with null. Rebuilt only when the value changes.</summary>
    public void SetHealth(float? fraction)
    {
        _health.Visible = fraction != null && _banner.Visible;
        if (fraction is not { } f || Mathf.IsEqualApprox(f, _shownHealth)) return;
        _shownHealth = f;
        _health.Mesh = HealthBar(Mathf.Clamp(f, 0, 1));
    }

    /// <summary>
    /// A slim gilded frame like the banner's, with a dark translucent trough. The fill shades from green through amber to
    /// red as health drops, is lit from above, carries a sheen on its top half and quarter marks along its length.
    /// </summary>
    private static ArrayMesh HealthBar(float fraction)
    {
        var shape = BarOutline(BarHalfWidth, BarHalfHeight);
        float top = BarHalfHeight + BarEdge, bottom = -top, h = BarHalfHeight, left = -BarHalfWidth;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Band(st, Offset(shape, BarEdge), Offset(shape, BarRim), _ => BannerInk);
        Band(st, Offset(shape, BarRim), Offset(shape, BarLine), y => GoldLight.Lerp(GoldDark, Mathf.InverseLerp(top, bottom, y)));
        Band(st, Offset(shape, BarLine), shape, _ => BannerInk with { A = 0.8f });
        Fan(st, shape, _ => Trough);
        if (fraction > 0)
        {
            var colour = fraction > 0.5f ? Wounded.Lerp(Healthy, (fraction - 0.5f) * 2) : Dying.Lerp(Wounded, fraction * 2);
            float right = left + 2 * BarHalfWidth * fraction;
            Fan(st, ClipRight(shape, right), y => colour.Lightened(0.15f).Lerp(colour.Darkened(0.4f), Mathf.InverseLerp(h, -h, y)) with { A = 0.95f });
            Fan(st, ClipRight(BarOutline(BarHalfWidth - 0.012f, h * 0.75f).Select(p => p + new Vector2(0, h * 0.2f)).ToArray(), right - 0.006f), y => new Color(1, 1, 1, 0.22f * Mathf.InverseLerp(h * 0.1f, h * 0.85f, y)));
        }
        for (int i = 1; i < 4; i++)
        {
            float x = left + BarHalfWidth * i / 2f;
            Fan(st, Rect(x - 0.003f, x + 0.003f, -h, h), _ => new Color(0, 0, 0, 0.35f));
        }
        return st.Commit();
    }

    /// <summary>A long box with chamfered ends. Counter-clockwise, convex.</summary>
    private static Vector2[] BarOutline(float w, float h) =>
        [new(-w + h, -h), new(w - h, -h), new(w, 0), new(w - h, h), new(-w + h, h), new(-w, 0)];

    /// <summary>The part of a convex polygon left of <paramref name="x"/>.</summary>
    private static Vector2[] ClipRight(Vector2[] poly, float x)
    {
        var kept = new List<Vector2>();
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
            if (a.X <= x) kept.Add(a);
            if ((a.X <= x) != (b.X <= x)) kept.Add(a.Lerp(b, (x - a.X) / (b.X - a.X)));
        }
        return kept.ToArray();
    }

    private static Vector2[] Rect(float left, float right, float bottom, float top) =>
        [new(left, bottom), new(right, bottom), new(right, top), new(left, top)];

    /// <summary>
    /// A camera-facing shield in the owner's colour, floating above the model and drawn over the terrain: a dark edge, a
    /// gilded rim with a fine inner line, a top-lit gradient fill with a glossy sheen, all slightly see-through.
    /// The layers are rings that do not overlap, so the translucency never shows one layer through another.
    /// </summary>
    private static MeshInstance3D Banner(Color owner)
    {
        var shape = ShieldOutline(BannerHalfWidth, BannerHalfHeight);
        float top = BannerHalfHeight + BannerEdge, bottom = -top;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Band(st, Offset(shape, BannerEdge), Offset(shape, BannerRim), _ => BannerInk);
        Band(st, Offset(shape, BannerRim), Offset(shape, BannerLine),
            y => GoldLight.Lerp(GoldDark, Mathf.InverseLerp(top, bottom, y)));
        Band(st, Offset(shape, BannerLine), shape, _ => BannerInk with { A = 0.8f });
        Fan(st, shape, y => FillAt(owner, y));
        float sheenBottom = BannerHalfHeight * 0.15f, x = BannerHalfWidth * 0.86f, y0 = BannerHalfHeight * 0.9f;
        Vector2[] sheen = [new(-x, y0), new(x, y0), new(x, sheenBottom), new(-x, sheenBottom)];
        Fan(st, sheen, y => new Color(1, 1, 1, 0.3f * Mathf.InverseLerp(sheenBottom, y0, y)));
        return new MeshInstance3D
        {
            Name = "Banner", Mesh = st.Commit(), Position = new Vector3(0, BannerHeight, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = OverlayMaterial,
        };
    }

    /// <summary>Lighter at the top, the true colour in the middle, deep at the point.</summary>
    private static Color FillAt(Color owner, float y)
    {
        float t = Mathf.InverseLerp(BannerHalfHeight, -BannerHalfHeight, y);
        var c = t < 0.45f ? owner.Lightened(0.35f).Lerp(owner, t / 0.45f) : owner.Lerp(owner.Darkened(0.6f), (t - 0.45f) / 0.55f);
        return c with { A = BannerAlpha };
    }

    /// <summary>Flat top, straight sides, then a curved taper to a point. Counter-clockwise, convex.</summary>
    private static Vector2[] ShieldOutline(float w, float h) =>
    [
        new(0, -h), new(w * 0.42f, -h * 0.74f), new(w * 0.78f, -h * 0.46f), new(w, -h * 0.12f),
        new(w, h), new(-w, h), new(-w, -h * 0.12f), new(-w * 0.78f, -h * 0.46f), new(-w * 0.42f, -h * 0.74f),
    ];

    /// <summary>The convex polygon grown outward by <paramref name="d"/>, keeping every edge parallel.</summary>
    private static Vector2[] Offset(Vector2[] poly, float d)
    {
        var result = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            var a = Normal(poly[(i + poly.Length - 1) % poly.Length], poly[i]);
            var b = Normal(poly[i], poly[(i + 1) % poly.Length]);
            result[i] = poly[i] + (a + b) * (d / (1 + a.Dot(b)));
        }
        return result;
    }

    private static Vector2 Normal(Vector2 from, Vector2 to) { var e = (to - from).Normalized(); return new Vector2(e.Y, -e.X); }

    /// <summary>The ring between two matching polygons.</summary>
    private static void Band(SurfaceTool st, Vector2[] outer, Vector2[] inner, Func<float, Color> color)
    {
        for (int i = 0; i < outer.Length; i++)
        {
            int j = (i + 1) % outer.Length;
            Tri(st, color, outer[i], outer[j], inner[j]);
            Tri(st, color, outer[i], inner[j], inner[i]);
        }
    }

    /// <summary>A convex polygon as a fan around its centroid.</summary>
    private static void Fan(SurfaceTool st, Vector2[] poly, Func<float, Color> color)
    {
        var centre = poly.Aggregate(Vector2.Zero, (sum, p) => sum + p) / poly.Length;
        for (int i = 0; i < poly.Length; i++) Tri(st, color, centre, poly[i], poly[(i + 1) % poly.Length]);
    }

    private static void Tri(SurfaceTool st, Func<float, Color> color, params Vector2[] points)
    {
        foreach (var p in points) { st.SetColor(color(p.Y)); st.AddVertex(new Vector3(p.X, p.Y, 0)); }
    }

}
