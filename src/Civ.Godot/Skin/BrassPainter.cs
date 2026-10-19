using Godot;

namespace Civ.Client;

/// <summary>Sides of a plate. An open side runs off the texture with no rim and no corners (a tab's foot).</summary>
[Flags]
internal enum Edges { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }

/// <summary>
/// How a rim is shaded. <see cref="Polished"/>: a smooth bright bevel with a dark lip either side, as on the
/// reference's buttons and tabs. <see cref="Dull"/>: worn, painted-over brass with flecks and chips (check boxes).
/// </summary>
internal enum RimFinish { Polished, Dull }

/// <summary>
/// One painted-brass plate: a rounded panel of scratched paint inside a worn brass rim.
/// Every widget skin is a plate with different colours, rim widths and lighting.
/// </summary>
internal sealed record Plate
{
    public required Color Fill { get; init; }
    /// <summary>Brass rim width on each closed side; 0 paints the fill alone.</summary>
    public float Rim { get; init; } = 1.3f;
    /// <summary>Rim width on the top side when it differs (a tab rail); negative means <see cref="Rim"/>.</summary>
    public float TopRim { get; init; } = -1f;
    /// <summary>Brass brightness: below 1 for dull, worn brass.</summary>
    public float Brass { get; init; } = 1f;
    public RimFinish Finish { get; init; } = RimFinish.Polished;
    public float Radius { get; init; } = 4f;
    /// <summary>Diagonal cut across each closed corner, in pixels (tabs); 0 for plain rounded corners.</summary>
    public float Chamfer { get; init; }
    /// <summary>Which sides' corners get the <see cref="Chamfer"/> cut: both by default, the right only for a tab.</summary>
    public Edges ChamferSides { get; init; } = Edges.Left | Edges.Right;
    /// <summary>Transparent space around the plate, so it sits inside another plate's rim.</summary>
    public float Pad { get; init; }
    public Edges Open { get; init; }
    /// <summary>1 lights the plate from the top left (raised); -1 flips the light (pressed in).</summary>
    public float Light { get; init; } = 1f;
    /// <summary>Recessed look: shadow under the top-left rim, light along the bottom right.</summary>
    public float Well { get; init; }
    /// <summary>Inner highlight under the lit rim.</summary>
    public float Glow { get; init; }
    /// <summary>Soft blotches in the painted base. Style boxes keep it low: their overlay paints the clouds.</summary>
    public float Mottle { get; init; } = 0.02f;
    /// <summary>Strength of the brushwork and open-field scrapes in the overlay: 1 for raised paint, lower for wells.</summary>
    public float Strokes { get; init; } = 1f;
    /// <summary>Paint scraped off along the rim, down to the bronze: 0 for none, 1 for a well-handled edge.</summary>
    public float Wear { get; init; } = 1f;
    /// <summary>Dark hairline outside the rim.</summary>
    public bool Outline { get; init; } = true;
    /// <summary>
    /// Strength of the soft drop shadow cast down and to the right, as under every raised plate in the reference.
    /// It is painted into a margin outside the plate, which style boxes draw as an expand margin; icons have none.
    /// </summary>
    public float Shadow { get; init; } = 1f;
    /// <summary>
    /// Brightens the fill inside <see cref="FieldInset"/> (left, top, right, bottom, from the plate's rect), where a
    /// style box's paint overlay darkens it again. Outside it, next to the rim, the fill keeps its own colour.
    /// </summary>
    public float FieldLift { get; init; } = 1f;
    public Vector4 FieldInset { get; init; }
    /// <summary>How far, in pixels, rim shading reaches into the fill.</summary>
    public float Falloff { get; init; } = 4f;
    /// <summary>Width of a bevel on a rimless plate's own edge, lit from the top left; 0 for none.</summary>
    public float Bevel { get; init; }
    public int Seed { get; init; } = 1;
    /// <summary>
    /// Texture pixels per unit. Every size on a plate is in units (game pixels at 1x); a larger scale paints the
    /// same plate with more detail, for high-DPI screens and for comparing against the full-size reference.
    /// </summary>
    public int Scale { get; init; } = 1;

    public float TopRimWidth => TopRim < 0 ? Rim : TopRim;
}

/// <summary>
/// A shape painted over an icon in <paramref name="Colour"/> with a dark outline (check marks, arrows, rules).
/// With a <paramref name="Highlight"/>, the edges that face the light get that colour, like a cast bevel.
/// <paramref name="Shade"/>, when set, gives the colour at each point instead (a glossy bead, a soft vignette);
/// its alpha blends it over the plate.
/// </summary>
internal readonly record struct Glyph(Func<float, float, float> Distance, Color Colour, Color? Highlight = null,
    Func<float, float, Color>? Shade = null, bool Outline = true);

/// <summary>
/// Paints plates pixel by pixel. A tiled plate is a 9-slice texture: fixed corners and a middle one noise period
/// wide and tall, so the middle repeats without seams. Each side samples different noise, so no two edges or
/// corners match: the rim reads as painted and worn by hand rather than mirrored.
/// Colours are measured from docs/references/ui.png scaled to game size (about 1 / 3.3).
/// </summary>
internal static class BrassPainter
{
    public const int PeriodX = 192, PeriodY = 32;
    /// <summary>Width of the margin that holds the drop shadow, on every side.</summary>
    public const int ShadowPad = 6;
    private const int Supersample = 3;
    /// <summary>
    /// Width of the dark outline under the bottom edge, in units; elsewhere it is thinner. Polished plates follow
    /// the reference's fine 1–2 pixel outline; dull ones (check boxes) keep their heavier one.
    /// </summary>
    public static float OutlineWidth(Plate p) => p.Finish == RimFinish.Dull ? 2f : 1f;

    private static float OutlineBase(Plate p) => p.Finish == RimFinish.Dull ? 1.2f : 0.6f;
    /// <summary>The shadow is the plate's shape moved down and right by this much, then blurred.</summary>
    private static readonly Vector2 ShadowOffset = new(0.8f, 1.6f);

    /// <summary>Light direction (towards the light): up and a little left.</summary>
    private static readonly Vector2 Light = new Vector2(-0.4f, -1f).Normalized();
    private static readonly Color ShadowColour = new(0.06f, 0.055f, 0.045f);
    /// <summary>The pale, greyish catch-light on the inner half of the top rim.</summary>
    private static readonly Color PaleEdge = Color.Color8(128, 126, 118);
    /// <summary>The cool, pale catch-light on a bevelled paint edge, as on the reference's scroll thumb.</summary>
    private static readonly Color BevelLight = Color.Color8(186, 198, 204);

    /// <summary>
    /// Muted, painted brass. The reference rim averages 95–125 on lit sides and about 70 underneath;
    /// only scattered flecks reach the top of the ramp.
    /// </summary>
    private static readonly (float At, Color Colour)[] BrassRamp =
    [
        (0f, Color.Color8(28, 23, 18)), (0.35f, Color.Color8(72, 61, 48)), (0.55f, Color.Color8(108, 90, 68)),
        (0.7f, Color.Color8(132, 111, 85)), (0.85f, Color.Color8(178, 154, 120)), (1f, Color.Color8(226, 206, 170)),
    ];

    /// <summary>The 9-slice margin: shadow margin, outline, rim and corner, and the reach of the rim's shading.</summary>
    public static int MarginOf(Plate plate) =>
        (int)MathF.Ceiling(Inset(plate) + MathF.Max(MathF.Max(plate.Radius, plate.Chamfer), MathF.Max(plate.Rim, plate.TopRimWidth)) + plate.Falloff);

    /// <summary>A 9-slice texture with <see cref="MarginOf"/> margins and a <see cref="PeriodX"/> x <see cref="PeriodY"/> middle.</summary>
    public static Image PaintTiled(Plate plate)
    {
        var margin = MarginOf(plate);
        var canvas = new Canvas(plate, margin + PeriodX + margin, margin + PeriodY + margin, margin, margin, plate.Scale);
        return canvas.Paint([]);
    }

    /// <summary>
    /// A fixed-size icon, <paramref name="width"/> x <paramref name="height"/> units at <paramref name="scale"/>
    /// pixels per unit: an optional plate filling it, with glyphs (in units) painted on top in order.
    /// </summary>
    public static Image PaintIcon(int width, int height, int scale, Plate? plate, params Glyph[] glyphs)
    {
        // Icons never tile, so side shading may reach anywhere: the fade margin is far beyond any icon.
        var canvas = new Canvas(plate == null ? null : plate with { Shadow = 0f, Scale = scale }, width, height, 0, 10_000, scale);
        return canvas.Paint(glyphs);
    }

    /// <summary>The space between the plate's outer edge and its rect: the shadow margin when it has a shadow.</summary>
    public static int ShadowMarginOf(Plate plate) => plate.Shadow > 0f ? ShadowPad : 0;

    private static float Inset(Plate plate) => ShadowMarginOf(plate) + plate.Pad + (plate.Outline ? OutlineWidth(plate) : 0f);

    /// <summary>
    /// Geometry and shading for one texture, <paramref name="width"/> x <paramref name="height"/> units painted at
    /// <paramref name="scale"/> pixels per unit. All shading works in units. <paramref name="origin"/> is where
    /// noise coordinates start (the first middle unit); <paramref name="fade"/> is how far in from each texture edge
    /// side shading may reach, so it never leaks into the tiled middle.
    /// </summary>
    private sealed class Canvas(Plate? plate, int width, int height, int origin, int fade, int scale)
    {
        private readonly PeriodicNoise _noise = new(PeriodX, PeriodY, plate?.Seed ?? 1);
        private readonly (float X0, float Y0, float X1, float Y1) _box = plate == null ? default : Box(plate, width, height);

        public Image Paint(Glyph[] glyphs)
        {
            int pixelsWide = width * scale, pixelsHigh = height * scale;
            var bytes = new byte[pixelsWide * pixelsHigh * 4];
            Parallel.For(0, pixelsHigh, y =>
            {
                for (var x = 0; x < pixelsWide; x++)
                {
                    var c = SamplePixel(x, y, glyphs);
                    var i = (y * pixelsWide + x) * 4;
                    bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.R, 0f, 1f) * 255f);
                    bytes[i + 1] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.G, 0f, 1f) * 255f);
                    bytes[i + 2] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.B, 0f, 1f) * 255f);
                    bytes[i + 3] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.A, 0f, 1f) * 255f);
                }
            });
            return Image.CreateFromData(pixelsWide, pixelsHigh, false, Image.Format.Rgba8, bytes);
        }

        /// <summary>
        /// Near an edge (outline, rim, glyph), the average of Supersample² samples in premultiplied alpha, so edges
        /// are anti-aliased. Elsewhere one sample is enough.
        /// </summary>
        private Color SamplePixel(int px, int py, Glyph[] glyphs)
        {
            float cx = (px + 0.5f) / scale, cy = (py + 0.5f) / scale;
            if (!NearEdge(cx, cy, glyphs)) return Sample(cx, cy, glyphs);
            var sum = new Color(0, 0, 0, 0);
            for (var sy = 0; sy < Supersample; sy++)
            {
                for (var sx = 0; sx < Supersample; sx++)
                {
                    var c = Sample((px + (sx + 0.5f) / Supersample) / scale, (py + (sy + 0.5f) / Supersample) / scale, glyphs);
                    sum += new Color(c.R * c.A, c.G * c.A, c.B * c.A, c.A);
                }
            }
            sum /= Supersample * Supersample;
            return sum.A <= 0f ? new Color(0, 0, 0, 0) : new Color(sum.R / sum.A, sum.G / sum.A, sum.B / sum.A, sum.A);
        }

        private Color Sample(float x, float y, Glyph[] glyphs)
        {
            var c = plate == null ? new Color(0, 0, 0, 0) : Shade(plate, x, y);
            foreach (var glyph in glyphs) c = OverGlyph(c, glyph, x, y);
            return c;
        }

        /// <summary>Whether the pixel at (x, y) is within reach of the plate outline, the rim or a glyph outline.</summary>
        private bool NearEdge(float x, float y, Glyph[] glyphs)
        {
            foreach (var glyph in glyphs)
                if (glyph.Distance(x, y) is > -2f and < 2.6f) return true;
            if (plate == null) return false;
            var d = Distance(plate, x, y);
            var rim = 1.4f * MathF.Max(plate.Rim, plate.TopRimWidth);
            return d > -(rim + 1.5f) && d < OutlineWidth(plate) + 1f;
        }

        private Color Shade(Plate p, float x, float y)
        {
            float u = x - origin, v = y - origin;
            var normal = Normal(p, x, y);
            // The rim is handled brass, not machined: small dents knock its outer edge in, and the paint creeps over
            // its inner edge in places, so neither edge runs perfectly straight.
            // Check boxes (dull rims) and rimless plates keep their exact shape.
            var handled = p.Finish == RimFinish.Polished && p.Rim > 0f;
            var d = Distance(p, x, y) + (handled ? Dent(u, v) : 0f);
            if (d >= 0f) return Outside(p, d, normal, x, y);
            var rim = RimWidth(p, normal);
            if (handled) rim += 0.22f * _noise.Sample(u + 5.3f, v + 2.1f, 4, 4, 13) + 0.1f * _noise.Sample(u, v, 1, 1, 14);
            if (-d < rim) return BrassAt(p, u, v, -d / rim, normal);
            return FillAt(p, x, y, u, v, -d - rim);
        }

        /// <summary>
        /// A dark hairline all round, over a soft drop shadow. Measured on the reference at full size: under the
        /// bottom and right the backdrop darkens to about half and recovers over 15–20 px (5–6 px at game size);
        /// above and to the left the shadow is only a few pixels.
        /// </summary>
        private Color Outside(Plate p, float d, Vector2 normal, float x, float y)
        {
            var shadow = new Color(ShadowColour, 0f);
            if (p.Shadow > 0f)
            {
                var s = Distance(p, x - ShadowOffset.X, y - ShadowOffset.Y);
                shadow.A = p.Shadow * 0.62f * Mathf.Exp(-Mathf.Max(s, 0f) / 1.7f);
            }
            if (!p.Outline) return shadow;
            var width = OutlineBase(p) + (OutlineWidth(p) - OutlineBase(p)) * Mathf.Max(normal.Y, 0f);
            var alpha = Mathf.Clamp(1.2f - d / width, 0f, 1f) * 0.95f;
            return Over(shadow, new Color(ShadowColour, alpha));
        }

        /// <summary>Rim width by side; corners blend the widths of the two sides they join.</summary>
        private static float RimWidth(Plate p, Vector2 n)
        {
            var vertical = n.Y < 0 ? p.TopRimWidth : p.Rim;
            return n.X * n.X * p.Rim + n.Y * n.Y * vertical;
        }

        private Color BrassAt(Plate p, float u, float v, float t, Vector2 n) =>
            p.Finish == RimFinish.Dull ? DullBrassAt(p, u, v, t, n) : PolishedBrassAt(p, u, v, t, n);

        /// <summary>
        /// Brass at <paramref name="t"/> across the rim (0 outer edge, 1 inner edge), as a smooth bevel. Measured
        /// across the reference rim at full size, peak brightness: top 173, left 144, right 100, bottom 93; the outer
        /// and inner lips are about 60–70% of the peak. Along the rim it varies only slowly, with faint dull spots,
        /// so at game size it reads as one continuous polished line.
        /// </summary>
        private Color PolishedBrassAt(Plate p, float u, float v, float t, Vector2 n)
        {
            // The rim is a bevel sloping outwards, so each side faces away from the plate: measured peaks on the
            // reference are about 135 on top, 169 on the left, 108 on the right and only 50 (dark bronze) at the
            // bottom. Across the rim (t from 0 outside to 1 inside) it is brightest a little inside the middle.
            float lx = n.X * p.Light, ly = n.Y * p.Light;
            var peak = lx * lx * (lx < 0 ? 0.9f : 0.66f) + ly * ly * (ly < 0 ? 0.84f : 0.36f);
            var bevel = 0.72f + 0.28f * Mathf.Exp(-Mathf.Pow((t - 0.58f) / 0.3f, 2f));
            // Along the rim the brass is uneven: brighter rubbed lengths, duller tarnished ones, and fine streaks.
            var drift = 0.07f * _noise.Sample(u, v, 16, 16, 6) + 0.05f * _noise.Sample(u + 3.3f, v + 1.1f, 4, 4, 5)
                + 0.03f * _noise.Sample(u, v, 1, 1, 15);
            var dull = 0.14f * Mathf.SmoothStep(0.3f, 0.8f, _noise.Sample(u + 7.1f, v + 2.9f, 8, 8, 7));
            var value = (peak * bevel + drift - dull) * p.Brass;
            // Pits and nicks: sparse dark spots where two lattices of different sizes both peak.
            var pit = Mathf.Min(_noise.Sample(u, v, 2, 2, 16), _noise.Sample(u + 0.7f, v + 1.3f, 1, 1, 17));
            value = Mathf.Lerp(value, 0.22f, 0.7f * Mathf.SmoothStep(0.45f, 0.75f, pit));
            // Grime packed into the rim in patches, darkest towards the paint.
            var grime = Mathf.SmoothStep(0.4f, 0.75f, _noise.Sample(u + 1.9f, v + 6.7f, 4, 4, 18)) * Mathf.SmoothStep(0.2f, 1f, t);
            return Ramp(value).Lerp(Color.Color8(46, 36, 26), 0.55f * grime);
        }

        /// <summary>
        /// How far a dent knocks the plate's outer edge in at (u, v), in units: zero along most of the edge, up to
        /// about half a unit at sparse, short dents.
        /// </summary>
        private float Dent(float u, float v)
        {
            var dent = Mathf.Min(_noise.Sample(u, v, 4, 4, 19), _noise.Sample(u + 1.1f, v + 0.3f, 2, 2, 20));
            return 0.55f * Mathf.SmoothStep(0.35f, 0.7f, dent);
        }

        /// <summary>
        /// Brass at <paramref name="t"/> across the rim (0 outer edge, 1 inner edge): flat painted bronze, brighter on
        /// sides facing the light, with a pale catch-light along the inner half of the top, streaks along the edge,
        /// bright flecks where the paint is polished and dark chips where it has worn through.
        /// </summary>
        private Color DullBrassAt(Plate p, float u, float v, float t, Vector2 n)
        {
            var lit = p.Light * n.Dot(Light);
            var streak = n.Y * n.Y * _noise.Sample(u, v, 16, 2, 3) + n.X * n.X * _noise.Sample(u, v, 2, 16, 4);
            var grain = _noise.Sample(u, v, 1, 5);
            var swell = _noise.Sample(u, v, 32, 16, 6);
            // Measured per side: left brightest, top outer half a little dimmer (its inner half is the catch-light),
            // right dimmer still, bottom darkest.
            var value = 0.52f + 0.16f * lit - 0.12f * Mathf.Max(-n.Y * p.Light, 0f) + 0.08f * Mathf.Max(-n.X * p.Light, 0f)
                + 0.08f * streak + 0.08f * grain + 0.07f * swell;
            // Polished flecks: two lattices of different sizes must both peak, so they land sparse and off-grid.
            var fleck = Mathf.Min(_noise.Sample(u, v, 4, 2, 11), _noise.Sample(u + 1.7f, v + 0.6f, 3, 3, 12));
            value += 0.25f * Mathf.SmoothStep(0.35f, 0.7f, fleck);
            value = Mathf.Lerp(value, 0.2f, 0.75f * Mathf.SmoothStep(0.5f, 0.8f, _noise.Sample(u, v, 4, 2, 7))); // chips
            var colour = Ramp(value * p.Brass);
            var catchLight = Mathf.Pow(Mathf.Max(-n.Y * p.Light, 0f), 2f) * Mathf.SmoothStep(0.35f, 0.85f, t);
            return colour.Lerp(PaleEdge * p.Brass, 0.7f * catchLight);
        }

        /// <summary>
        /// Base paint: softly mottled, a dark groove under the lit rims, lit or shadowed per side. Brushwork and scrapes
        /// down to the bronze come from the <see cref="ScrapedPaint"/> overlay, drawn at 1:1 with a phase per control.
        /// </summary>
        private Color FillAt(Plate p, float x, float y, float u, float v, float depth)
        {
            var mottle = 0.6f * _noise.Sample(u, v, 32, 16, 8) + 0.4f * _noise.Sample(u, v, 8, 8, 9);
            var c = p.Fill * (1f + p.Mottle * mottle);
            if (p.FieldLift != 1f && InField(p, x, y)) c *= p.FieldLift;
            var light = p.Light * SideLight(p, x, y);
            float toLight = Mathf.Max(light, 0f), away = Mathf.Max(-light, 0f);
            c += p.Fill * (p.Glow * toLight);
            c *= 1f - p.Well * toLight - 0.2f * p.Glow * away;
            c += p.Fill * (0.6f * p.Well * away);
            var n = Normal(p, x, y);
            if (p.Bevel > 0f) c = Bevelled(p, c, n, depth);
            else if (p.Finish == RimFinish.Polished && p.Rim > 0f) c = EdgeOfPaint(p, c, n, depth, u, v);
            else
            {
                // Dull rims (check boxes): only the rim's cast shadow, under the sides that face the light.
                float up = Mathf.Max(-n.Y * p.Light, 0f), left = Mathf.Max(-n.X * p.Light, 0f);
                c *= 1f - 0.75f * (up * up + 0.25f * left * left) * Mathf.Exp(-depth / 0.45f);
            }
            c.A = 1f;
            return c;
        }

        /// <summary>
        /// The edge of the paint against a polished rim, measured on the reference at full size (about 3.3 pixels
        /// per unit). On every side the paint ends in a raised lip that catches the light, about 0.6 units wide,
        /// then a dark groove, about 0.6 units, at roughly half the paint's brightness. The lip shows mainly on top,
        /// a pale grey catch-light, faint on the left and bottom and absent on the right.
        /// </summary>
        private Color EdgeOfPaint(Plate p, Color c, Vector2 n, float depth, float u, float v)
        {
            const float lip = 0.6f, groove = 0.65f;
            float lx = n.X * p.Light, ly = n.Y * p.Light;
            // Worn off in places along its length, so it never reads as a second frame.
            var worn = Mathf.Clamp(0.55f + 0.6f * _noise.Sample(u + 9.1f, v + 4.3f, 8, 8, 10), 0f, 1f);
            var lift = 1f + worn * (lx * lx * (lx < 0 ? 0.15f : 0f) + ly * ly * (ly < 0 ? 0.6f : 0.12f));
            var lipColour = c * lift;
            var grey = (lipColour.R + lipColour.G + lipColour.B) / 3f;
            lipColour = lipColour.Lerp(new Color(grey, grey, grey * 0.96f), 0.6f * Mathf.Max(-ly, 0f));
            var inLip = Mathf.SmoothStep(lip + 0.15f, lip - 0.15f, depth);
            var inGroove = Mathf.SmoothStep(lip - 0.1f, lip + 0.15f, depth) * Mathf.SmoothStep(lip + groove + 0.25f, lip + groove - 0.1f, depth);
            return c.Lerp(lipColour, inLip) * (1f - 0.47f * inGroove);
        }

        /// <summary>
        /// A rimless plate's own bevelled edge (the scroll thumb): within <see cref="Plate.Bevel"/> of the edge the
        /// paint turns towards the light, so the top and left edges catch a pale highlight and the bottom and right
        /// fall into shadow, strongest at the very edge.
        /// </summary>
        private static Color Bevelled(Plate p, Color c, Vector2 n, float depth)
        {
            // Measured across the reference thumb: a dark lip at the very edge, then on the lit side a pale band about
            // two-thirds of a unit wide; the shadowed side darkens to about 0.8 over a wider slope.
            var lip = Mathf.SmoothStep(0.7f, 0.15f, depth);
            var facing = n.Dot(Light) * p.Light;
            if (facing > 0f)
            {
                var band = Mathf.SmoothStep(p.Bevel, p.Bevel * 0.45f, depth) * (1f - lip);
                c = c.Lerp(BevelLight, Mathf.Min(1f, 1.1f * facing) * band);
            }
            else c *= 1f - 0.4f * -facing * Mathf.SmoothStep(p.Bevel * 2f, 0.3f, depth);
            return c * (1f - 0.5f * lip);
        }

        /// <summary>Whether (x, y) is inside the overlay's field; insets count from the rect, inside the shadow margin.</summary>
        private bool InField(Plate p, float x, float y)
        {
            // Open sides have no shadow margin in the style box: the texture's edge is the rect's edge there.
            var s = ShadowMarginOf(p);
            float Margin(Edges side) => p.Open.HasFlag(side) ? 0f : s;
            var f = p.FieldInset;
            return x >= Margin(Edges.Left) + f.X && y >= Margin(Edges.Top) + f.Y
                && x <= width - Margin(Edges.Right) - f.Z && y <= height - Margin(Edges.Bottom) - f.W;
        }

        /// <summary>
        /// How much the nearby sides face the light: positive under the top and left rims, negative under the bottom
        /// and right. Each side fades out before the tiled middle, so tiles join cleanly.
        /// </summary>
        private float SideLight(Plate p, float x, float y)
        {
            var reach = p.Falloff * 0.5f;
            var (x0, y0, x1, y1) = _box;
            float Side(float into, float distanceToTextureEdge) =>
                Mathf.Exp(-Mathf.Max(into, 0f) / reach) * Mathf.SmoothStep(fade, fade - 2f, distanceToTextureEdge);
            var top = Side(y - y0 - p.TopRimWidth, y);
            var left = Side(x - x0 - p.Rim, x);
            var bottom = Side(y1 - p.Rim - y, height - y);
            var right = Side(x1 - p.Rim - x, width - x);
            return -Light.Y * top - Light.X * left + 0.5f * (Light.Y * bottom + Light.X * right);
        }

        /// <summary>The plate box; open sides extend well past the texture so no rim or corner shows there.</summary>
        private static (float X0, float Y0, float X1, float Y1) Box(Plate p, int width, int height)
        {
            var inset = Inset(p);
            var beyond = 2f * p.Radius + p.Rim + 16f;
            return (
                p.Open.HasFlag(Edges.Left) ? -beyond : inset,
                p.Open.HasFlag(Edges.Top) ? -beyond : inset,
                p.Open.HasFlag(Edges.Right) ? width + beyond : width - inset,
                p.Open.HasFlag(Edges.Bottom) ? height + beyond : height - inset);
        }

        /// <summary>Signed distance (negative inside) to the rounded, optionally chamfered plate box.</summary>
        private float Distance(Plate p, float x, float y)
        {
            var (x0, y0, x1, y1) = _box;
            float hx = (x1 - x0) / 2f, hy = (y1 - y0) / 2f;
            var r = Mathf.Min(p.Radius, Mathf.Min(hx, hy));
            float ax = Mathf.Abs(x - (x0 + hx)), ay = Mathf.Abs(y - (y0 + hy));
            float qx = ax - hx + r, qy = ay - hy + r;
            var rounded = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).Length() + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            if (p.Chamfer <= 0f) return rounded;
            var right = x >= x0 + hx;
            if (!p.ChamferSides.HasFlag(right ? Edges.Right : Edges.Left)) return rounded;
            var cut = (ax + ay - (hx + hy - p.Chamfer)) / Mathf.Sqrt2;
            return Mathf.Max(rounded, cut);
        }

        private Vector2 Normal(Plate p, float x, float y)
        {
            const float e = 0.25f;
            var n = new Vector2(Distance(p, x + e, y) - Distance(p, x - e, y), Distance(p, x, y + e) - Distance(p, x, y - e));
            return n.LengthSquared() < 1e-12f ? Vector2.Zero : n.Normalized();
        }

        /// <summary>Glyph in its colour with a one-pixel dark outline; edges facing the light take the highlight.</summary>
        private static Color OverGlyph(Color under, Glyph glyph, float x, float y)
        {
            var d = glyph.Distance(x, y);
            if (d >= 1.1f) return under;
            if (d >= 0f) return glyph.Outline ? Over(under, new Color(ShadowColour, 0.85f * (1f - d / 1.1f))) : under;
            var colour = glyph.Shade?.Invoke(x, y) ?? glyph.Colour;
            if (glyph.Highlight is { } highlight && d > -1.6f)
            {
                const float e = 0.25f;
                var n = new Vector2(glyph.Distance(x + e, y) - glyph.Distance(x - e, y), glyph.Distance(x, y + e) - glyph.Distance(x, y - e));
                var facing = n.LengthSquared() < 1e-12f ? 0f : n.Normalized().Dot(Light);
                colour = colour.Lerp(highlight, Mathf.Clamp(facing * 1.4f, 0f, 1f) * Mathf.Clamp(1.6f + d, 0f, 1f));
            }
            return Over(under, colour);
        }
    }

    private static Color Over(Color under, Color top)
    {
        var a = top.A + under.A * (1f - top.A);
        if (a <= 0f) return new Color(0, 0, 0, 0);
        var c = (top * top.A + under * under.A * (1f - top.A)) / a;
        c.A = a;
        return c;
    }

    private static Color Ramp(float v)
    {
        v = Mathf.Clamp(v, 0f, 1f);
        for (var i = 1; i < BrassRamp.Length; i++)
        {
            if (v > BrassRamp[i].At) continue;
            var (a, b) = (BrassRamp[i - 1], BrassRamp[i]);
            return a.Colour.Lerp(b.Colour, (v - a.At) / (b.At - a.At));
        }
        return BrassRamp[^1].Colour;
    }
}
