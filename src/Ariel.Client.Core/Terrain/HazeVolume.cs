using Ariel.Game;

namespace Ariel.Client.Terrain;

/// <summary>The vertical extent of one kind of haze: its bottom above the ground (or the sea surface) and its depth, in
/// world units.</summary>
public readonly record struct HazeLayer(double Bottom, double Depth)
{
    /// <summary>Layer of a haze kind; <see cref="FeatureHaze.None"/> and <see cref="FeatureHaze.Smoke"/> (drawn as plumes)
    /// have none.</summary>
    public static HazeLayer? Of(FeatureHaze haze) => haze switch
    {
        // Ground fogs fill the low ground up to about the height of the trees.
        FeatureHaze.Mist => new(0, 0.16),
        FeatureHaze.Smog => new(0, 0.26),
        // Storm clouds stand clear of the sea with a flat base and towering tops.
        FeatureHaze.Storm => new(0.34, 0.62),
        _ => null,
    };
}

/// <summary>A smoke column over a known crater: the crater floor in world coordinates (canonical x), the column's
/// density (0..1), its colour and its fog brightness (1 visible, 0.55 fogged).</summary>
public readonly record struct SmokePlume(int Tile, double X, double Y, double Z, double Density, Rgb Colour, float Fog);

/// <summary>A box of the world that holds haze, for the renderer's raymarch proxies.</summary>
public readonly record struct HazeBox(double X0, double Y0, double Z0, double X1, double Y1, double Z1);

/// <summary>Haze baked into two maps over the whole world for the volume shader, so it needs no tile lookups and no
/// depth reads per raymarch step. Texel (i, j) covers world x in [i, i + 1)·<see cref="Period"/>/<see cref="Width"/> and
/// world z in <see cref="ZMin"/> + [j, j + 1)·<see cref="TexelZ"/>; x repeats over the map period.
/// <para><see cref="Shape"/> (RGBA8): R = density, G = floor (ground or sea surface height, where rays stop), B = layer
/// bottom, A = layer top; heights are encoded by <see cref="EncodeHeight"/>. A layer's bottom and top follow each hazy
/// tile's low ground, not the ground under the texel, so fog lies flat and high ground rises out of it. <see cref="Look"/> (RGBA8): RGB = colour
/// (toned down for fogged tiles), A = kind (<see cref="FeatureHaze"/> / 4).</para>
/// Density blends the hazy tiles around a texel with broad weights, so a region fades out past its edge. Hidden tiles
/// never contribute. Only texels near hazy tiles sample the height field, so the bake costs nothing on clear maps.</summary>
public sealed class HazeVolume
{
    public const double TexelsPerUnit = 5;
    /// <summary>Lowest and highest encodable height.</summary>
    public const double HeightMin = -0.5, HeightMax = 2.5;
    /// <summary>Broad blend weights, as for feature ground shade.</summary>
    private const double Sharpness = 2;

    private HazeVolume(int width, int height, double period, double zMin, double texelZ)
    {
        Width = width;
        Height = height;
        Period = period;
        ZMin = zMin;
        TexelZ = texelZ;
        Shape = new byte[width * height * 4];
        Look = new byte[width * height * 4];
    }

    public int Width { get; }
    public int Height { get; }
    public double Period { get; }
    public double ZMin { get; }
    public double TexelZ { get; }
    public double ZSpan => Height * TexelZ;
    public double TexelX => Period / Width;
    public byte[] Shape { get; }
    public byte[] Look { get; }
    /// <summary>True when any texel holds haze.</summary>
    public bool Any { get; private set; }
    /// <summary>Smoke columns over known smoking craters.</summary>
    public IReadOnlyList<SmokePlume> Plumes { get; private set; } = [];

    public static byte EncodeHeight(double h) => (byte)Math.Round(Math.Clamp((h - HeightMin) / (HeightMax - HeightMin), 0, 1) * 255);
    public static double DecodeHeight(byte b) => HeightMin + b / 255.0 * (HeightMax - HeightMin);

    /// <summary>Bake the haze of a field's snapshot. <paramref name="grounds"/> is the palette's feature ground table.</summary>
    public static HazeVolume Bake(TerrainField field, IReadOnlyList<FeatureStyle?> grounds)
    {
        var snapshot = field.Snapshot;
        int width = Math.Max(1, (int)Math.Round(snapshot.Period * TexelsPerUnit));
        // One row of tiles beyond the north and south edges, where clamped rows still blend in.
        double zMin = -1.5, zMax = snapshot.Height * 1.5 + 1.5;
        int height = Math.Max(1, (int)Math.Ceiling((zMax - zMin) * TexelsPerUnit));
        var volume = new HazeVolume(width, height, snapshot.Period, zMin, 1 / TexelsPerUnit);
        FeatureStyle? StyleOf(int tile) =>
            snapshot.Relief[tile] == ReliefClass.Hidden || snapshot.FeatureGround[tile] >= grounds.Count ? null : grounds[snapshot.FeatureGround[tile]];
        HazeLayer? LayerOf(int tile) => StyleOf(tile) is { } style ? HazeLayer.Of(style.Look.Haze) : null;

        var rows = HazyRows(volume, snapshot, LayerOf);
        var bases = new Dictionary<int, double>();
        for (int tile = 0; tile < snapshot.Relief.Length; tile++)
            if (LayerOf(tile) is not null) bases[tile] = BaseOf(field, tile);
        int any = 0;
        Parallel.For(0, height, j =>
        {
            if (!rows[j]) return;
            for (int i = 0; i < width; i++)
                if (volume.BakeTexel(field, i, j, StyleOf, bases)) any = 1;
        });
        volume.Any = any != 0;
        volume.Plumes = FindPlumes(field, StyleOf);
        return volume;
    }

    /// <summary>The level haze settles on over a tile: its lowest ground (or the sea surface) among the centre and six
    /// points toward its corners. Fog fills the low ground and lies flat on top, so hills and peaks rise out of it.</summary>
    private static double BaseOf(TerrainField field, int tile)
    {
        var (cx, cz) = field.Snapshot.Grid.HexOf(new TileId(tile)).ToPixel(1);
        double low = field.HeightAt(cx, cz);
        for (int k = 0; k < 6; k++)
        {
            double a = Math.PI / 6 + k * Math.PI / 3;
            low = Math.Min(low, field.HeightAt(cx + 0.75 * Math.Cos(a), cz + 0.75 * Math.Sin(a)));
        }
        return Math.Max(low, 0);
    }

    /// <summary>Rows of texels within reach (one tile plus the warp) of a hazy tile.</summary>
    private static bool[] HazyRows(HazeVolume volume, TerrainSnapshot snapshot, Func<int, HazeLayer?> layerOf)
    {
        var rows = new bool[volume.Height];
        for (int tile = 0; tile < snapshot.Relief.Length; tile++)
        {
            if (layerOf(tile) is null) continue;
            var (_, z) = snapshot.Grid.HexOf(new TileId(tile)).ToPixel(1);
            int from = (int)Math.Floor((z - 2.1 - volume.ZMin) / volume.TexelZ), to = (int)Math.Ceiling((z + 2.1 - volume.ZMin) / volume.TexelZ);
            for (int j = Math.Max(0, from); j <= Math.Min(volume.Height - 1, to); j++) rows[j] = true;
        }
        return rows;
    }

    private bool BakeTexel(TerrainField field, int i, int j, Func<int, FeatureStyle?> styleOf, IReadOnlyDictionary<int, double> bases)
    {
        var snapshot = field.Snapshot;
        double x = (i + 0.5) * TexelX, z = ZMin + (j + 0.5) * TexelZ;
        var (ox, oz) = field.Warp.Sample(x, z);
        double a = field.Tuning.WarpAmplitude;
        var tri = HexBlend.CornerTriangle(x + ox * a, z + oz * a);
        var weights = HexBlend.Weights(tri.Lambda, Sharpness);
        double density = 0, bottom = 0, depth = 0, r = 0, g = 0, b = 0, hazy = 0;
        FeatureHaze kind = FeatureHaze.None;
        double kindWeight = 0;
        for (int k = 0; k < 3; k++)
        {
            if (weights[k] <= 0) continue;
            int tile = snapshot.ClampedTile(tri[k]);
            if (styleOf(tile) is not { } style || HazeLayer.Of(style.Look.Haze) is not { } layer) continue;
            double w = weights[k];
            hazy += w;
            density += w * Math.Clamp(style.Look.HazePercent, 0, 100) / 100.0;
            bottom += w * (bases[tile] + layer.Bottom);
            depth += w * layer.Depth;
            var colour = Rgb.Hex(style.Look.HazeRgb);
            if (snapshot.Sight[tile] == Sight.Fogged) colour = FogTone(colour);
            r += w * colour.R; g += w * colour.G; b += w * colour.B;
            if (w > kindWeight) { kindWeight = w; kind = style.Look.Haze; }
        }
        if (hazy <= 1e-6) return false;
        double floor = Math.Max(field.HeightAt(x, z), 0);
        double low = bottom / hazy, high = low + depth / hazy;
        int at = (j * Width + i) * 4;
        Shape[at] = ToByte(density);
        Shape[at + 1] = EncodeHeight(floor);
        Shape[at + 2] = EncodeHeight(low);
        Shape[at + 3] = EncodeHeight(high);
        Look[at] = ToByte(r / hazy);
        Look[at + 1] = ToByte(g / hazy);
        Look[at + 2] = ToByte(b / hazy);
        Look[at + 3] = (byte)((int)kind * 255 / 4);
        return true;
    }

    private static List<SmokePlume> FindPlumes(TerrainField field, Func<int, FeatureStyle?> styleOf)
    {
        var snapshot = field.Snapshot;
        var plumes = new List<SmokePlume>();
        for (int tile = 0; tile < snapshot.Crater.Length; tile++)
        {
            if (!snapshot.Crater[tile] || styleOf(tile) is not { Look.Haze: FeatureHaze.Smoke } style) continue;
            if (field.CraterCentre(tile) is not { } c) continue;
            float fog = snapshot.Sight[tile] == Sight.Visible ? 1f : 0.55f;
            plumes.Add(new(tile, c.X, c.Y, c.Z, Math.Clamp(style.Look.HazePercent, 0, 100) / 100.0, Rgb.Hex(style.Look.HazeRgb), fog));
        }
        return plumes;
    }

    /// <summary>Boxes that hold the baked haze inside a chunk's rectangle, clipped to it so neighbouring chunks never
    /// march the same air twice. Null when the chunk has no haze.</summary>
    public HazeBox? BoxIn(TerrainChunk chunk)
    {
        // Two texels beyond the chunk, so haze that starts just outside it still opens the box at its edge.
        int i0 = (int)Math.Floor(chunk.X0 / TexelX) - 2, i1 = (int)Math.Ceiling(chunk.X1 / TexelX) + 2;
        int j0 = Math.Max(0, (int)Math.Floor((chunk.Z0 - ZMin) / TexelZ) - 2);
        int j1 = Math.Min(Height - 1, (int)Math.Ceiling((chunk.Z1 - ZMin) / TexelZ) + 2);
        double x0 = double.MaxValue, x1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
        double y0 = double.MaxValue, y1 = double.MinValue;
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                int at = (j * Width + ((i % Width) + Width) % Width) * 4;
                if (Shape[at] == 0) continue;
                double x = (i + 0.5) * TexelX, z = ZMin + (j + 0.5) * TexelZ;
                x0 = Math.Min(x0, x - 2 * TexelX); x1 = Math.Max(x1, x + 2 * TexelX);
                z0 = Math.Min(z0, z - 2 * TexelZ); z1 = Math.Max(z1, z + 2 * TexelZ);
                y0 = Math.Min(y0, DecodeHeight(Shape[at + 1]));
                y1 = Math.Max(y1, DecodeHeight(Shape[at + 3]));
            }
        if (x0 > x1) return null;
        return new(Math.Max(x0, chunk.X0), y0 - 0.02, Math.Max(z0, chunk.Z0), Math.Min(x1, chunk.X1), y1 + 0.03, Math.Min(z1, chunk.Z1));
    }

    private static Rgb FogTone(Rgb c)
    {
        float l = 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        return new Rgb(l, l, l).Lerp(c, 0.55f).Scale(0.72f);
    }

    private static byte ToByte(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
}
