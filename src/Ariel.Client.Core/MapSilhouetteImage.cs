using Ariel.Game;

namespace Ariel.Client;

/// <summary>RGBA8 pixels, row-major, already fitted to the box a screen asked for.</summary>
public sealed record MapImage(int Width, int Height, byte[] Rgba);

/// <summary>Draws a silhouette's coastline into pixels. Every pixel averages several point samples taken in hex space,
/// which anti-aliases the hex edges without a shader; points beyond the map edges stay transparent. Hexes are pointy-top
/// with a circumradius of one: a column is √3 wide, a row is 1.5 high, and odd rows shift right by half a column.</summary>
public static class MapSilhouetteImage
{
    private static readonly double Sqrt3 = Math.Sqrt(3);

    /// <summary>The picture of a silhouette at the largest size that keeps the map's proportions inside the box.</summary>
    public static MapImage Render(MapSilhouette silhouette, int maxWidth, int maxHeight, Rgb water, Rgb land, int samples = 3)
    {
        if (maxWidth <= 0) throw new ArgumentOutOfRangeException(nameof(maxWidth));
        if (maxHeight <= 0) throw new ArgumentOutOfRangeException(nameof(maxHeight));
        if (samples <= 0) throw new ArgumentOutOfRangeException(nameof(samples));
        int columns = silhouette.Map.Width, rows = silhouette.Map.Height;
        // A map repeats every W columns when it wraps, so its period is exactly W columns wide; without wrapping,
        // odd rows shift right and add half a column beyond the last one.
        double spanX = Sqrt3 * columns + (silhouette.Map.Wrap || rows <= 1 ? 0 : Sqrt3 / 2), spanY = 1.5 * (rows - 1) + 2;
        // Keep the map's proportions: fit the longer axis and centre the shorter one.
        double scale = Math.Min(maxWidth / spanX, maxHeight / spanY);
        int width = Math.Max(1, (int)Math.Round(spanX * scale)), height = Math.Max(1, (int)Math.Round(spanY * scale));
        double stepX = spanX / width / samples, stepY = spanY / height / samples;
        var pixels = new byte[width * height * 4];
        int perPixel = samples * samples;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int inside = 0, onLand = 0;
                for (int sy = 0; sy < samples; sy++)
                    for (int sx = 0; sx < samples; sx++)
                    {
                        double wx = -Sqrt3 / 2 + (x * samples + sx + 0.5) * stepX;
                        double wy = -1 + (y * samples + sy + 0.5) * stepY;
                        if (Tile(silhouette, wx, wy) is not { } tile) continue;
                        inside++;
                        if (silhouette.Land[tile]) onLand++;
                    }
                if (inside == 0) continue; // Beyond the map edges, so the panel behind shows through.
                var colour = water.Lerp(land, (float)onLand / inside);
                int at = (y * width + x) * 4;
                pixels[at] = Tone(colour.R);
                pixels[at + 1] = Tone(colour.G);
                pixels[at + 2] = Tone(colour.B);
                pixels[at + 3] = Tone((float)inside / perPixel);
            }
        return new(width, height, pixels);
    }

    /// <summary>Tile under a world point, or null beyond the map edges. Columns wrap only when the map wraps.</summary>
    private static int? Tile(MapSilhouette silhouette, double x, double y)
    {
        int columns = silhouette.Map.Width, rows = silhouette.Map.Height;
        // World point to fractional axial coordinates, then to the nearest hex by cube rounding.
        double q = Sqrt3 / 3 * x - y / 3, r = 2.0 / 3 * y, s = -q - r;
        int rq = (int)Math.Round(q), rr = (int)Math.Round(r), rs = (int)Math.Round(s);
        double dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);
        if (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds) rr = -rq - rs;
        if (rr < 0 || rr >= rows) return null;
        int col = rq + (rr - (rr & 1)) / 2;
        if (silhouette.Map.Wrap) col = ((col % columns) + columns) % columns;
        if (col < 0 || col >= columns) return null;
        return rr * columns + col;
    }

    private static byte Tone(float value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
