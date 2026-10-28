namespace Ariel.Client.Terrain;

/// <summary>Colour grade for scatter foliage textures. The source packs ship pure yellow, lime and red leaf atlases that
/// glow against the textured ground. Each vivid pixel moves toward a muted olive-to-moss ramp picked by its luminance,
/// keeping a desaturated trace of its own hue so species still differ. Pale or grey pixels (snowy needles, stems) keep
/// their colour, and alpha is never touched, so cutout edges stay exactly where they were.</summary>
public static class FoliageGrade
{
    // sRGB ramp stops by luminance: deep moss shadow, muted mid green, dusty sage highlight.
    private static readonly (double R, double G, double B)[] Ramp =
        [(0.13, 0.17, 0.08), (0.32, 0.41, 0.19), (0.57, 0.64, 0.37)];
    private const double OwnHue = 0.3;
    private const double OwnSaturation = 0.45;

    /// <summary>Grade tightly packed sRGB RGBA8 pixels in place.</summary>
    public static void Apply(Span<byte> rgba)
    {
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            double r = rgba[i] / 255.0, g = rgba[i + 1] / 255.0, b = rgba[i + 2] / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double saturation = max > 0 ? (max - min) / max : 0;
            double amount = SmoothStep(0.12, 0.45, saturation);
            if (amount <= 0) continue;
            double luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            var target = RampAt(luma);
            rgba[i] = Byte(Grade(r, luma, target.R, amount));
            rgba[i + 1] = Byte(Grade(g, luma, target.G, amount));
            rgba[i + 2] = Byte(Grade(b, luma, target.B, amount));
        }
    }

    private static double Grade(double channel, double luma, double ramp, double amount)
    {
        double own = luma + (channel - luma) * OwnSaturation;
        double graded = ramp + (own - ramp) * OwnHue;
        return channel + (graded - channel) * amount;
    }

    private static (double R, double G, double B) RampAt(double luma)
    {
        double t = Math.Clamp(luma, 0, 1) * (Ramp.Length - 1);
        int i = Math.Min((int)t, Ramp.Length - 2);
        double f = t - i;
        var (a, b) = (Ramp[i], Ramp[i + 1]);
        return (a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f);
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static byte Byte(double v) => (byte)Math.Clamp((int)Math.Round(v * 255), 0, 255);
}
