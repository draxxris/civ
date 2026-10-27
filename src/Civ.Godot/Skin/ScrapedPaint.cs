using Godot;

namespace Civ.Client;

/// <summary>
/// Overlay tiles for one paint finish: <see cref="Field"/> covers the whole fill, and the four bands run along the
/// inside of the rim, where the paint wears most. Band textures have the rim side at their outer edge: the top of
/// <see cref="Top"/>, the bottom of <see cref="Bottom"/>, the left of <see cref="Left"/> and the right of <see cref="Right"/>.
/// </summary>
internal sealed record ScrapeSet(Texture2D Field, Texture2D Top, Texture2D Bottom, Texture2D Left, Texture2D Right, int Scale)
{
    /// <summary>Depth of the top and bottom bands in pixels.</summary>
    public int EdgeDepth => ScrapedPaint.EdgeDepth * Scale;
    /// <summary>Depth of the left and right bands in pixels.</summary>
    public int EndDepth => ScrapedPaint.EndDepth * Scale;
}

/// <summary>
/// Paints the wear on each control's paint, after docs/references/ui.png at full size. The paint itself is matte:
/// soft clouds and mottling with no direction, and fine grain. On top come hairline scratches down to the bronze,
/// mostly just inside the rim, a broken pale line where the paint has rubbed thin against the rim, and dark grime.
/// <para>
/// The reference marks are about one pixel wide at full size, which is a third of a pixel at game size. So the
/// marks are drawn at three times game size (or more at higher scales) and averaged down: they come out as faint, soft traces
/// rather than hard pixel dashes. Paint shading is black at a varying alpha, which darkens without changing the
/// hue, so one overlay serves every paint colour. Everything wraps round the tile edges, so tiles join without seams.
/// </para>
/// </summary>
internal static class ScrapedPaint
{
    public const int FieldWidth = 256, FieldHeight = 128, BandLength = 256;
    /// <summary>How far the top and bottom bands reach in from the rim.</summary>
    public const int EdgeDepth = 6;
    /// <summary>How far the left and right bands reach in: scuffs gather at the ends of a plate, where hands go.</summary>
    public const int EndDepth = 22;

    /// <summary>Average darkening of the field. Plates paint their fill lighter to match, so the mean is the fill.</summary>
    public const float MeanShade = 0.16f;

    /// <summary>Worn bronze in scratches and chips: dull and brown, as in the reference, not bright metal.</summary>
    private static readonly Color Metal = Color.Color8(110, 80, 48);
    /// <summary>Paint rubbed thin over bronze, just inside the rim: a muted warm brown.</summary>
    private static readonly Color Rubbed = Color.Color8(104, 86, 64);
    /// <summary>Dark brown dirt worked into the paint near the rim.</summary>
    private static readonly Color Grime = Color.Color8(52, 40, 28);
    /// <summary>Paint rubbed through to bronze at the ends of a plate: warm, orange-brown.</summary>
    private static readonly Color Worn = Color.Color8(98, 72, 46);

    private enum Side { Top, Bottom, Left, Right }

    /// <summary>
    /// <paramref name="strokes"/> scales the paint texture and the open-field scratches; <paramref name="wear"/>
    /// scales the rim bands. Each side gets its own amount, so no two edges match: the left wears most, as in the
    /// reference, and the right least.
    /// </summary>
    public static ScrapeSet Paint(int seed, float strokes, float wear, int scale = 1)
    {
        // Each layer has its own seed, drawn up front, so painting them in parallel stays repeatable.
        var rng = new Random(seed);
        var seeds = Enumerable.Range(0, 5).Select(_ => rng.Next()).ToArray();
        var layers = new Layer[5];
        Parallel.Invoke(
            () => layers[0] = PaintField(new Random(seeds[0]), strokes, scale),
            () => layers[1] = PaintBand(new Random(seeds[1]), Side.Top, wear * 1.0f, scale),
            () => layers[2] = PaintBand(new Random(seeds[2]), Side.Bottom, wear * 0.9f, scale),
            () => layers[3] = PaintBand(new Random(seeds[3]), Side.Left, wear * 1.15f, scale),
            () => layers[4] = PaintBand(new Random(seeds[4]), Side.Right, wear * 0.6f, scale));
        return new ScrapeSet(Texture(layers[0]), Texture(layers[1]), Texture(layers[2]), Texture(layers[3]), Texture(layers[4]), scale);
    }

    private static Texture2D Texture(Layer layer) => ImageTexture.CreateFromImage(layer.ToImage());

    // ---- Field ------------------------------------------------------------------------------------

    /// <summary>
    /// Matte paint on cast metal, after the reference at full size. Three things make it read as painted metal
    /// rather than flat colour: a fine pitted relief (a height field about one unit across, lit from the top left),
    /// patches where the paint lies a little darker or thinner, with irregular, fairly sharp edges (thresholded,
    /// warped noise), and scuffs of fine brown scratches.
    /// </summary>
    private static Layer PaintField(Random rng, float strength, int scale)
    {
        var layer = new Layer(FieldWidth, FieldHeight, scale);
        int w = layer.Wide, h = layer.High, d = layer.Detail;
        var noise = new PeriodicNoise(w, h, rng.Next());
        var height = new float[w * h];
        Parallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
                height[y * w + x] = 0.7f * noise.Sample(x, y, d, d, 1) + 0.3f * noise.Sample(x + 1.3f * d, y + 0.7f * d, 2 * d, 2 * d, 2);
        });
        Parallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                // Relief lit from the top left: faces turned up and left are lighter.
                var dy = height[Wrap(y + 1, h) * w + x] - height[Wrap(y - 1, h) * w + x];
                var dx = height[y * w + Wrap(x + 1, w)] - height[y * w + Wrap(x - 1, w)];
                var relief = (0.9f * dy + 0.4f * dx) * d;
                // Patches: warped noise, thresholded, so their edges are organic and fairly crisp.
                // Stretched along the brush, as the reference's dark patches are.
                float wx = x + 8f * d * noise.Sample(x, y, 16 * d, 8 * d, 3), wy = y + 2f * d * noise.Sample(x, y, 16 * d, 8 * d, 4);
                var patch = 0.55f * noise.Sample(wx, wy, 32 * d, 8 * d, 5) + 0.3f * noise.Sample(wx, wy, 16 * d, 4 * d, 6) + 0.15f * noise.Sample(wx, wy, 8 * d, 2 * d, 7);
                var dark = Mathf.SmoothStep(0.12f, 0.2f, patch);
                var thin = Mathf.SmoothStep(-0.22f, -0.3f, patch);
                var shade = MeanShade + strength * (0.085f * dark - 0.07f * thin - 0.035f * relief);
                layer.SetShade(x, y, shade);
            }
        });
        // Scuffs: patches of fine parallel scratches where something dragged across, as on the reference's
        // charcoal button.
        for (var i = 0; i < 6; i++)
        {
            var centre = new Vector2(rng.NextSingle() * FieldWidth, rng.NextSingle() * FieldHeight);
            Scuff(layer, rng, centre, new Vector2(24f + 40f * rng.NextSingle(), 3f + 5f * rng.NextSingle()), 8 + rng.Next(12), 0.9f * strength);
        }
        for (var i = 0; i < 30; i++)
        {
            var at = new Vector2(rng.NextSingle() * FieldWidth, rng.NextSingle() * FieldHeight);
            Scratch(layer, rng, at, Gaussian(rng) * 0.06f, 3f + 16f * rng.NextSingle(), (0.2f + 0.3f * rng.NextSingle()) * strength);
        }
        return layer;
    }

    private static int Wrap(int v, int m) => ((v % m) + m) % m;

    // ---- Rim bands --------------------------------------------------------------------------------

    /// <summary>
    /// Wear along one side, measured inward from the inner edge of the rim (depth 0). Under the rim's dark groove,
    /// the paint has rubbed thin into a pale warm line, broken along its length. Beyond it: grime smudges, a few
    /// small chips, and hairline scratches that thin out with depth. Top and bottom scratches run along the edge;
    /// on the sides they are short and run in from the rim, the way the brush moved.
    /// </summary>
    private static Layer PaintBand(Random rng, Side side, float wear, int scale)
    {
        var horizontal = side is Side.Top or Side.Bottom;
        var bandDepth = horizontal ? EdgeDepth : EndDepth;
        var layer = horizontal ? new Layer(BandLength, bandDepth, scale) : new Layer(bandDepth, BandLength, scale);
        var noise = new PeriodicNoise(BandLength, BandLength, rng.Next());
        Vector2 At(float along, float depth) => side switch
        {
            Side.Top => new Vector2(along, depth),
            Side.Bottom => new Vector2(along, bandDepth - depth),
            Side.Left => new Vector2(depth, along),
            _ => new Vector2(bandDepth - depth, along),
        };
        var angleAlong = horizontal ? 0f : MathF.PI / 2f;

        // The rubbed line: in the reference it lies under the rim's dark groove, 2–4 px wide at full size.
        // Only under the lit rims (top and left): a pale line inside the bottom or right would read as a lit wall.
        var step = 1f / layer.Detail;
        for (var along = 0f; along < (side is Side.Top or Side.Left ? BandLength : 0f); along += step)
        {
            var broken = 0.2f + 0.6f * noise.Sample(along, 3.5f, 16, 16, 1) + 0.35f * noise.Sample(along, 9.5f, 4, 4, 2);
            var alpha = Mathf.Clamp(broken, 0f, 1f) * Mathf.Min(wear, 1.2f) * 0.2f;
            if (alpha <= 0.01f) continue;
            var depth = 1.7f + 0.3f * noise.Sample(along, 20.5f, 32, 32, 3);
            layer.Dot(At(along, depth), 0.9f, Rubbed, alpha);
        }

        // Grime: soft dark smudges straddling the groove, longer along the edge. Only under the top and left rims,
        // where the groove is: dark smudges inside the bottom and right would read as a groove there too.
        var grime = side is Side.Top or Side.Left ? 9 : 0;
        for (var i = 0; i < (int)(grime * wear); i++)
        {
            var size = new Vector2(3f + 9f * rng.NextSingle(), 0.8f + 1.4f * rng.NextSingle());
            layer.Smudge(At(rng.NextSingle() * BandLength, MathF.Abs(Gaussian(rng)) * 1.2f), horizontal ? size : new Vector2(size.Y, size.X),
                Grime, (0.12f + 0.2f * rng.NextSingle()) * wear);
        }

        // Worn zones: irregular patches where the paint has rubbed through to dark bronze, crisp-edged and fading
        // with distance from the rim. Heaviest at the ends, where hands go, as on the reference's buttons.
        // Along the top and bottom the wear is scratches only: zones there read as a saw-tooth edge.
        var reach = 7f;
        var zones = new PeriodicNoise(layer.Wide, layer.High, rng.Next());
        int d = layer.Detail;
        for (var y = 0; y < (horizontal ? 0 : layer.High); y++)
        {
            for (var x = 0; x < layer.Wide; x++)
            {
                var at = new Vector2((x + 0.5f) / d, (y + 0.5f) / d);
                var depth = side switch { Side.Top => at.Y, Side.Bottom => bandDepth - at.Y, Side.Left => at.X, _ => bandDepth - at.X };
                // Broken into small patches: mostly fine octaves, so no solid strip forms along the rim.
                var n = 0.35f * zones.Sample(x, y, 8 * d, 8 * d, 1) + 0.4f * zones.Sample(x, y, 4 * d, 2 * d, 2) + 0.25f * zones.Sample(x, y, 2 * d, d, 3);
                var threshold = 0.12f + depth / reach * 0.6f - 0.2f * (wear - 0.6f);
                var mask = Mathf.SmoothStep(threshold, threshold + 0.1f, n);
                if (mask > 0f) layer.Blend(x, y, Worn, mask * 0.6f);
            }
        }

        // Chips: tiny flakes of bare bronze at the very edge, fewer under the top rim's groove.
        for (var i = 0; i < (int)((side == Side.Top ? 4 : 10) * wear); i++)
            layer.Dot(At(rng.NextSingle() * BandLength, MathF.Abs(Gaussian(rng)) * 0.8f), 0.5f + 0.7f * rng.NextSingle(),
                MetalAt(0.85f + 0.3f * rng.NextSingle()), 0.5f + 0.4f * rng.NextSingle());

        if (horizontal)
        {
            // Scratches along the edge, thinning out with depth. They start below the top rim's dark groove,
            // which must stay unbroken to read as the rim's shadow.
            for (var i = 0; i < (int)(55 * wear); i++)
            {
                var depth = 1f + MathF.Abs(Gaussian(rng)) * 1.5f;
                if (depth > bandDepth - 0.5f) continue;
                var alpha = (0.5f + 0.5f * rng.NextSingle()) * MathF.Exp(-depth / 3f);
                Scratch(layer, rng, At(rng.NextSingle() * BandLength, depth), angleAlong + Gaussian(rng) * 0.02f, 3f + 22f * rng.NextSingle() * rng.NextSingle(), alpha);
            }
            return layer;
        }
        // At the ends: short scratches running in from the rim, and scuff patches that fade with distance from it.
        for (var i = 0; i < (int)(24 * wear); i++)
            Scratch(layer, rng, At(rng.NextSingle() * BandLength, 0.6f + 1.2f * rng.NextSingle()), Gaussian(rng) * 0.25f, 1.5f + 3f * rng.NextSingle(),
                (0.35f + 0.45f * rng.NextSingle()) * 0.6f);
        for (var i = 0; i < (int)(7 * wear); i++)
        {
            var depth = 3f + MathF.Abs(Gaussian(rng)) * 4f;
            if (depth > bandDepth - 5f) continue;
            var extent = new Vector2(6f + 10f * rng.NextSingle(), 1.5f + 3f * rng.NextSingle());
            Scuff(layer, rng, At(rng.NextSingle() * BandLength, depth), extent, 3 + rng.Next(6), 0.55f * MathF.Exp(-depth / 7f), fadeInward: side == Side.Left ? 1 : -1);
        }
        return layer;
    }

    // ---- Marks ------------------------------------------------------------------------------------

    /// <summary>
    /// A patch of <paramref name="count"/> fine, near-parallel horizontal scratches inside an oval
    /// <paramref name="extent"/> (full width and height), fainter towards its top and bottom. With
    /// <paramref name="fadeInward"/> (+1 left band, -1 right band) the patch also fades as x moves away from the rim.
    /// </summary>
    private static void Scuff(Layer layer, Random rng, Vector2 centre, Vector2 extent, int count, float alpha, int fadeInward = 0)
    {
        var angle = Gaussian(rng) * 0.025f;
        for (var i = 0; i < count; i++)
        {
            var offset = new Vector2(Gaussian(rng) * extent.X * 0.3f, Gaussian(rng) * extent.Y * 0.35f);
            var falloff = MathF.Exp(-MathF.Pow(offset.Y / (extent.Y * 0.5f), 2f));
            if (fadeInward != 0) falloff *= MathF.Exp(-MathF.Max(offset.X * fadeInward, 0f) / extent.X);
            Scratch(layer, rng, centre + offset, angle + Gaussian(rng) * 0.012f, extent.X * (0.25f + 0.6f * rng.NextSingle()),
                alpha * falloff * (0.4f + 0.6f * rng.NextSingle()));
        }
    }

    /// <summary>
    /// A hairline scratch through the paint, centred on <paramref name="centre"/> at <paramref name="angle"/>
    /// (radians from horizontal): about half to most of a unit wide, tapered, with gaps where the point skipped, in
    /// bronze that catches the light unevenly along its length, or partly filled with dirt.
    /// </summary>
    private static void Scratch(Layer layer, Random rng, Vector2 centre, float angle, float length, float alpha)
    {
        var heading = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var start = centre - heading * (length / 2f);
        var width = 0.45f + 0.4f * rng.NextSingle();
        var shine = 0.85f + 0.25f * rng.NextSingle();
        // Some scratches are clean bronze, others filled with dirt, as in the reference.
        var dirt = 0.6f * rng.NextSingle() * rng.NextSingle();
        var gaps = new PeriodicNoise(64, 64, rng.Next());
        var steps = (int)MathF.Ceiling(length * layer.Detail * 2f);
        for (var s = 0; s <= steps; s++)
        {
            var t = s / (float)steps;
            var skip = Mathf.SmoothStep(-0.35f, 0.1f, gaps.Sample(t * length, 0f, 4, 4, 1));
            var taper = MathF.Sqrt(MathF.Sin(MathF.PI * Mathf.Clamp(t, 0.02f, 0.98f)));
            var glint = shine * (1f + 0.2f * gaps.Sample(t * length, 7f, 2, 2, 2));
            layer.Dot(start + heading * (length * t), width * (0.5f + 0.5f * taper), MetalAt(glint).Lerp(Grime, dirt), Mathf.Min(1f, alpha * 1.3f) * taper * skip);
        }
    }

    private static Color MetalAt(float shine) =>
        new(Mathf.Clamp(Metal.R * shine, 0f, 1f), Mathf.Clamp(Metal.G * shine, 0f, 1f), Mathf.Clamp(Metal.B * shine, 0f, 1f));

    private static float Gaussian(Random rng)
    {
        var u = 1f - rng.NextSingle();
        return MathF.Sqrt(-2f * MathF.Log(u)) * MathF.Cos(2f * MathF.PI * rng.NextSingle());
    }
    /// <summary>
    /// A tile <paramref name="width"/> x <paramref name="height"/> units, exported at <paramref name="scale"/>
    /// pixels per unit, that wraps on both axes. It is painted at <see cref="Detail"/> pixels per unit (at least
    /// three, a multiple of the scale) in two parts, a shade (black at a per-pixel alpha) and colour marks over it,
    /// and averaged down on export. Mark positions and sizes are in units.
    /// </summary>
    private sealed class Layer
    {
        private readonly int _width, _height, _scale;
        private readonly float[] _shade;
        /// <summary>Premultiplied colour marks.</summary>
        private readonly Color[] _marks;

        public Layer(int width, int height, int scale)
        {
            (_width, _height, _scale) = (width, height, scale);
            Detail = scale * (int)MathF.Ceiling(3f / scale);
            _shade = new float[Wide * High];
            _marks = new Color[Wide * High];
        }

        /// <summary>Painting pixels per unit.</summary>
        public int Detail { get; }
        public int Wide => _width * Detail;
        public int High => _height * Detail;

        /// <summary>Sets the shade of one painting pixel.</summary>
        public void SetShade(int x, int y, float alpha) => _shade[y * Wide + x] = Mathf.Clamp(alpha, 0f, 1f);

        /// <summary>Sets bare metal at one painting pixel, under any marks drawn later.</summary>
        public void SetMetal(int x, int y, Color metal, float alpha)
        {
            alpha = Mathf.Clamp(alpha, 0f, 1f);
            _marks[y * Wide + x] = new Color(metal.R * alpha, metal.G * alpha, metal.B * alpha, alpha);
        }

        /// <summary>An anti-aliased round dot of <paramref name="colour"/>. Overlapping dots never build up past
        /// the strongest one's alpha, so a scratch made of dots stays one even line.</summary>
        public void Dot(Vector2 centre, float diameter, Color colour, float alpha)
        {
            if (alpha <= 0f) return;
            var c = centre * Detail;
            var r = diameter * Detail / 2f;
            int x0 = (int)MathF.Floor(c.X - r - 1), x1 = (int)MathF.Ceiling(c.X + r + 1);
            int y0 = (int)MathF.Floor(c.Y - r - 1), y1 = (int)MathF.Ceiling(c.Y + r + 1);
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var coverage = Mathf.Clamp(r + 0.5f - new Vector2(x + 0.5f, y + 0.5f).DistanceTo(c), 0f, 1f);
                    if (coverage > 0f) Max(x, y, colour, alpha * coverage);
                }
            }
        }

        /// <summary>A soft oval of <paramref name="colour"/>, fading out from the middle; smudges add up.</summary>
        public void Smudge(Vector2 centre, Vector2 radius, Color colour, float alpha)
        {
            var c = centre * Detail;
            var r = radius * Detail;
            int x0 = (int)MathF.Floor(c.X - 2f * r.X), x1 = (int)MathF.Ceiling(c.X + 2f * r.X);
            int y0 = (int)MathF.Floor(c.Y - 2f * r.Y), y1 = (int)MathF.Ceiling(c.Y + 2f * r.Y);
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var q = new Vector2((x + 0.5f - c.X) / r.X, (y + 0.5f - c.Y) / r.Y);
                    Over(x, y, colour, alpha * MathF.Exp(-1.5f * q.LengthSquared()));
                }
            }
        }

        /// <summary>Paints <paramref name="colour"/> over one painting pixel.</summary>
        public void Blend(int x, int y, Color colour, float alpha) => Over(x, y, colour, alpha);

        private void Over(int x, int y, Color colour, float alpha)
        {
            if (alpha <= 0.002f) return;
            ref var p = ref _marks[Index(x, y)];
            alpha = Mathf.Clamp(alpha, 0f, 1f);
            p = new Color(colour.R * alpha + p.R * (1f - alpha), colour.G * alpha + p.G * (1f - alpha),
                colour.B * alpha + p.B * (1f - alpha), alpha + p.A * (1f - alpha));
        }

        private void Max(int x, int y, Color colour, float alpha)
        {
            var current = _marks[Index(x, y)].A;
            if (alpha <= current) return;
            Over(x, y, colour, (alpha - current) / (1f - current));
        }

        public Image ToImage()
        {
            int outWide = _width * _scale, outHigh = _height * _scale, f = Detail / _scale;
            var bytes = new byte[outWide * outHigh * 4];
            float cells = f * f;
            for (var y = 0; y < outHigh; y++)
            {
                for (var x = 0; x < outWide; x++)
                {
                    // Marks over the shade, both premultiplied; the shade is black, so it adds alpha only.
                    var p = new Color(0, 0, 0, 0);
                    for (var sy = 0; sy < f; sy++)
                    {
                        for (var sx = 0; sx < f; sx++)
                        {
                            var i = (y * f + sy) * Wide + x * f + sx;
                            var m = _marks[i];
                            p += new Color(m.R, m.G, m.B, m.A + _shade[i] * (1f - m.A));
                        }
                    }
                    p /= cells;
                    var c = p.A <= 0f ? new Color(0, 0, 0, 0) : new Color(p.R / p.A, p.G / p.A, p.B / p.A, p.A);
                    var o = (y * outWide + x) * 4;
                    bytes[o] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.R, 0f, 1f) * 255f);
                    bytes[o + 1] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.G, 0f, 1f) * 255f);
                    bytes[o + 2] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.B, 0f, 1f) * 255f);
                    bytes[o + 3] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.A, 0f, 1f) * 255f);
                }
            }
            return Image.CreateFromData(outWide, outHigh, false, Image.Format.Rgba8, bytes);
        }

        private int Index(int x, int y) => Wrap(y, High) * Wide + Wrap(x, Wide);
    }
}
