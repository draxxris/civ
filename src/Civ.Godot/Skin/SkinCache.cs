using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;

namespace Civ.Client;

/// <summary>
/// The skin's painted textures, cached as PNG files on disk so every launch after the first loads them instead of
/// painting them: 109 textures come out at about 2.9 s painted and about 0.13 s loaded. PNG is lossless, so a cached
/// launch draws the same pixels as a painting one.
/// <para>
/// Nothing here notices that the painting code changed, so <see cref="Version"/> must be bumped whenever it does, or
/// the game silently keeps showing the previous skin. Delete the <c>skin-cache</c> directory to clear it.
/// </para>
/// Keys are hashed from the paint inputs, never from a call counter, so adding a plate or an icon cannot renumber
/// the files of the others. Main thread only: the theme is installed on the main thread, and so is the Image API.
/// </summary>
internal static class SkinCache
{
    /// <summary>
    /// Cache format version. Bump it whenever any painting code changes: every field of <see cref="Plate"/> and every
    /// colour, seed, dimension or blend in <see cref="BrassSkin"/>, <see cref="BrassPainter"/>,
    /// <see cref="ScrapedPaint"/>, <see cref="BackdropStyleBox"/> or <see cref="BrushedStyleBox"/>. Nothing else
    /// invalidates the cache, so without a bump a launch serves yesterday's texture.
    /// </summary>
    internal const int Version = 1;

    /// <summary>Cache directory for the install in progress; null when the cache cannot be used and every call paints.</summary>
    private static string? _root;
    private static long _began;
    private static int _painted, _loaded;

    /// <summary>
    /// Points the cache at <c>user://skin-cache/v&lt;Version&gt;/x&lt;scale&gt;</c>, since a theme applied at another
    /// scale is painted at another pixel density. Call it before installing a theme; a cache that cannot be created
    /// only makes the launch paint.
    /// </summary>
    public static void Begin(int scale)
    {
        _painted = _loaded = 0;
        _began = Stopwatch.GetTimestamp();
        try
        {
            _root = Path.Combine(OS.GetUserDataDir(), "skin-cache", $"v{Version}", $"x{scale}");
            Directory.CreateDirectory(_root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _root = null;
        }
    }

    /// <summary>
    /// Ends a session, reporting it unless it painted and loaded nothing: a runtime session that only found what it
    /// needed already in memory stays quiet, while the theme install still prints its one line.
    /// </summary>
    public static void End()
    {
        var ms = Stopwatch.GetElapsedTime(_began).TotalMilliseconds;
        if (_painted + _loaded > 0)
        {
            var what = _painted == 0 ? "loaded" : _loaded == 0 ? "painted" : "painted and loaded";
            GD.Print($"[skin-cache] {_painted + _loaded} textures {what} in {ms:F1} ms ({_loaded} loaded, {_painted} painted), {_root ?? "no cache"}");
        }
        _root = null;
    }

    /// <summary>
    /// Runs <paramref name="action"/> inside a cache session when none is open, so a texture painted after the theme
    /// install (a bar fill for a tint the theme does not use) is cached too. Never touches an open session.
    /// </summary>
    public static T Within<T>(int scale, Func<T> action)
    {
        if (_root is not null) return action();
        Begin(scale);
        try { return action(); } finally { End(); }
    }

    /// <summary>
    /// The texture painted by <paramref name="paint"/>, from <paramref name="key"/> if it is cached: an absent,
    /// unreadable, short or corrupt file means painting it and writing it over. A failure to read or write never
    /// fails the launch.
    /// </summary>
    public static Image Image(string key, Func<Image> paint) => Images(key, 1, () => [paint()])[0];

    /// <summary>
    /// The <paramref name="count"/> images painted together by <paramref name="paint"/>, all from the cache or all
    /// painted: one missing layer repaints the set (a scrape set's five layers only make sense as a set).
    /// </summary>
    public static Image[] Images(string key, int count, Func<Image[]> paint)
    {
        if (_root is { } root)
        {
            var loaded = new Image[count];
            var cached = true;
            for (var i = 0; i < count && cached; i++)
            {
                var image = Load(Path.Combine(root, $"{key}-{i}.png"));
                cached = image is not null;
                if (image is not null) loaded[i] = image;
            }
            if (cached) return loaded;
        }
        var images = paint();
        _painted += images.Length;
        if (_root is { } dir)
            for (var i = 0; i < images.Length; i++) Save(Path.Combine(dir, $"{key}-{i}.png"), images[i]);
        return images;
    }

    /// <summary>Key for a tiled 9-slice plate: every field of the plate, the tiling period and the scale.</summary>
    public static string TileKey(Plate plate) =>
        $"tile-{Hash($"tile|{BrassPainter.PeriodX}x{BrassPainter.PeriodY}|{Fields(plate)}")}";

    /// <summary>
    /// Key for an icon: its size, the scale, the plate it fills and the call site's own name for its glyphs. Glyphs
    /// are delegates, so their geometry cannot be hashed; the site name stands in for it and must be unique for each
    /// distinct set of glyphs.
    /// </summary>
    public static string IconKey(string site, int width, int height, int scale, Plate? plate) =>
        $"icon-{width}x{height}-{Hash($"icon|{width}x{height}|{scale}|{site}|{(plate is { } p ? Fields(p) : "-")}")}";

    /// <summary>Key for an icon a scrape set is composited into, before its glyphs: the plate as painted, its
    /// overlay inset, and the seed that picks the tile phase.</summary>
    public static string PaintedIconKey(string site, int width, int height, int scale, Plate plate, Vector4 inset, ulong phase) =>
        $"icon-{width}x{height}-{Hash($"icon|{width}x{height}|{scale}|{site}|{phase}|" +
            $"{Number(inset.X)},{Number(inset.Y)},{Number(inset.Z)},{Number(inset.W)}|{Fields(plate)}")}";

    /// <summary>Key for one scrape set: its seed (which picks every layer's random draws), the strengths and scale.</summary>
    public static string ScrapeKey(int seed, float strokes, float wear, int scale) =>
        $"scrape-{Hash($"scrape|{seed}|{Number(strokes)}|{Number(wear)}|{scale}")}";

    /// <summary>Key for the backdrop's three layers: the colour and the scale.</summary>
    public static string BackdropKey(Color colour, int scale) =>
        $"backdrop-{Hash($"backdrop|{Number(colour.R)}|{Number(colour.G)}|{Number(colour.B)}|{Number(colour.A)}|{scale}")}";

    /// <summary>
    /// FNV-1a 64 over the UTF-8 bytes of <paramref name="text"/>, as 16 hex digits. .NET randomizes string hashing per
    /// process, so <c>string.GetHashCode</c> or <c>HashCode.Combine</c> would name a different file on every launch.
    /// </summary>
    private static string Hash(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    /// <summary>Every field of a plate, so a key changes whenever a painted pixel could.</summary>
    private static string Fields(Plate p)
    {
        var text = new StringBuilder(256);
        foreach (var c in new[] { p.Fill.R, p.Fill.G, p.Fill.B, p.Fill.A })
            text.Append(Number(c)).Append(',');
        text.Append(Number(p.Rim)).Append('|').Append(Number(p.TopRimWidth)).Append('|').Append(Number(p.Brass)).Append('|')
            .Append(Number(p.Tarnish)).Append('|').Append(p.Finish).Append('|').Append(Number(p.Radius)).Append('|')
            .Append(Number(p.Chamfer)).Append('|').Append(p.ChamferSides).Append('|').Append(Number(p.Pad)).Append('|')
            .Append(p.Open).Append('|').Append(p.Hollow).Append('|').Append(Number(p.Light)).Append('|')
            .Append(Number(p.Well)).Append('|').Append(Number(p.Glow)).Append('|').Append(Number(p.Mottle)).Append('|')
            .Append(Number(p.Strokes)).Append('|').Append(Number(p.Wear)).Append('|').Append(p.Outline).Append('|')
            .Append(Number(p.Shadow)).Append('|').Append(Number(p.FieldLift)).Append('|')
            .Append(Number(p.FieldInset.X)).Append(',').Append(Number(p.FieldInset.Y)).Append(',')
            .Append(Number(p.FieldInset.Z)).Append(',').Append(Number(p.FieldInset.W)).Append('|')
            .Append(Number(p.Falloff)).Append('|').Append(Number(p.Bevel)).Append('|').Append(p.Seed).Append('|').Append(p.Scale);
        return text.ToString();
    }

    /// <summary>A float as the same text in every process, whatever the current culture.</summary>
    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static Godot.Image? Load(string file)
    {
        try
        {
            var image = new Godot.Image();
            if (image.LoadPngFromBuffer(File.ReadAllBytes(file)) == Error.Ok && image.GetWidth() > 0 && image.GetHeight() > 0)
            {
                _loaded++;
                return image;
            }
            image.Dispose();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // No file, no permission, or a truncated one: paint and overwrite it.
        }
        return null;
    }

    private static void Save(string file, Godot.Image image)
    {
        try
        {
            var bytes = image.SavePngToBuffer();
            if (bytes.Length > 0) File.WriteAllBytes(file, bytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // A cache that cannot be written only costs the next launch its painting.
        }
    }
}
