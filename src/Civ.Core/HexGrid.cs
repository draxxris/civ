using System.Text.Json.Serialization;

namespace Civ;

/// <summary>Row-major plot index. Valid values are in [0, Width * Height).</summary>
public readonly record struct TileId(int Value);

/// <summary>Rectangular pointy-top odd-row map; only east–west wrapping is supported.</summary>
public sealed record MapSpec(int Width, int Height, bool Wrap = false);

/// <summary>Coordinate conversion and bounded geometry. Enumeration is in ascending tile index,
/// except neighbors, which follow Hex.Directions (east, northeast, northwest, west, southwest, southeast).</summary>
public sealed class HexGrid
{
    /// <summary>Create a grid from a positive-sized map specification.</summary>
    public HexGrid(MapSpec spec)
    {
        if (spec.Width <= 0 || spec.Height <= 0) throw new ArgumentOutOfRangeException(nameof(spec));
        _ = checked(spec.Width * spec.Height);
        Spec = spec;
    }

    /// <summary>Map dimensions and wrap policy.</summary>
    public MapSpec Spec { get; }
    /// <summary>Number of plots.</summary>
    [JsonIgnore] public int Count => Spec.Width * Spec.Height;
    /// <summary>All valid tile ids, in row-major order.</summary>
    public IEnumerable<TileId> Tiles => Enumerable.Range(0, Count).Select(i => new TileId(i));
    /// <summary>Convert axial to odd-row offset coordinates; does not normalize.</summary>
    public static (int Col, int Row) ToOffset(Hex hex) => (hex.Q + (hex.R - (hex.R & 1)) / 2, hex.R);
    /// <summary>Convert odd-row offset coordinates to axial coordinates.</summary>
    public static Hex FromOffset(int col, int row) => new(col - (row - (row & 1)) / 2, row);
    /// <summary>Resolve a coordinate, normalizing the column when wrapping; false beyond bounded edges.</summary>
    public bool TryTile(int col, int row, out TileId tile)
    {
        if (Spec.Wrap) col = ((col % Spec.Width) + Spec.Width) % Spec.Width;
        tile = default;
        if (row < 0 || row >= Spec.Height || col < 0 || col >= Spec.Width) return false;
        tile = new TileId(row * Spec.Width + col);
        return true;
    }
    /// <summary>Resolve an axial coordinate under the map's wrap policy.</summary>
    public bool TryTile(Hex hex, out TileId tile)
    {
        var (col, row) = ToOffset(hex);
        return TryTile(col, row, out tile);
    }
    /// <summary>Resolve a coordinate or throw when it is off-map.</summary>
    public TileId TileAt(Hex hex) => TryTile(hex, out var id) ? id : throw new ArgumentOutOfRangeException(nameof(hex));
    /// <summary>Resolve an offset coordinate or throw when it is off-map.</summary>
    public TileId TileAt(int col, int row) => TryTile(col, row, out var id) ? id : throw new ArgumentOutOfRangeException(nameof(col));
    /// <summary>Convert a valid tile index to offset coordinates.</summary>
    public (int Col, int Row) OffsetOf(TileId tile)
    {
        if (tile.Value < 0 || tile.Value >= Count) throw new ArgumentOutOfRangeException(nameof(tile));
        return (tile.Value % Spec.Width, tile.Value / Spec.Width);
    }
    /// <summary>Canonical axial coordinate for a tile.</summary>
    public Hex HexOf(TileId tile) { var (col, row) = OffsetOf(tile); return FromOffset(col, row); }
    /// <summary>Whether this is a canonical, rendered coordinate (no duplicated wrap images).</summary>
    public bool Contains(Hex hex) => TryTile(hex, out var tile) && HexOf(tile) == hex;
    /// <summary>Canonicalize a valid coordinate.</summary>
    public Hex Normalize(Hex hex) => HexOf(TileAt(hex));
    /// <summary>Neighbor in one direction, or null at a bounded edge.</summary>
    public TileId? Neighbor(TileId tile, int direction) => TryTile(HexOf(tile).Neighbor(direction), out var n) ? n : null;
    /// <summary>Distinct neighbors, excluding self on narrow wrapped maps.</summary>
    public IEnumerable<TileId> Neighbors(TileId tile)
    {
        // Only narrow wrapped maps can map distinct directions to the same tile (or self).
        HashSet<TileId>? seen = Spec.Wrap && Spec.Width < 3 ? new() { tile } : null;
        for (int i = 0; i < 6; i++)
            if (Neighbor(tile, i) is { } n && (seen is null || seen.Add(n))) yield return n;
    }
    /// <summary>Neighbors expressed as canonical axial coordinates.</summary>
    public IEnumerable<Hex> Neighbors(Hex hex) => Neighbors(TileAt(hex)).Select(HexOf);
    /// <summary>Shortest hex distance across east–west wrap images.</summary>
    public int Distance(TileId a, TileId b)
    {
        var x = HexOf(a); var y = HexOf(b);
        if (!Spec.Wrap) return x.DistanceTo(y);
        // Cube distance is convex in the horizontal displacement; its minimum is near -dr/2.
        long dq = y.Q - x.Q, dr = y.R - x.R;
        long k = (-2 * dq - dr) / (2L * Spec.Width);
        long best = long.MaxValue;
        for (long shift = k - 1; shift <= k + 1; shift++)
        {
            long q = dq + shift * Spec.Width;
            best = Math.Min(best, (Math.Abs(q) + Math.Abs(dr) + Math.Abs(q + dr)) / 2);
        }
        return checked((int)best);
    }
    private int? _maximumDistance;
    /// <summary>Largest shortest hex distance on this rectangle. Derived geometry is cached, never saved.</summary>
    public int MaximumDistance => _maximumDistance ??= CalculateMaximumDistance();
    private int CalculateMaximumDistance()
    {
        int maximum = 0;
        for (int a = 0; a < Spec.Width; a++)
            for (int b = 0; b < Spec.Width; b++)
            {
                maximum = Math.Max(maximum, Distance(TileAt(a, 0), TileAt(b, Spec.Height - 1)));
                maximum = Math.Max(maximum, Distance(TileAt(a, 0), TileAt(b, 0)));
            }
        return maximum;
    }
    /// <summary>Wrapped distance for valid axial coordinates.</summary>
    public int Distance(Hex a, Hex b) => Distance(TileAt(a), TileAt(b));
    /// <summary>Clipped range, deduplicated at wrap seams, in tile order.</summary>
    public IEnumerable<TileId> Range(TileId center, int radius)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        return RangeRows(HexOf(center), radius);
    }

    private IEnumerable<TileId> RangeRows(Hex center, int radius)
    {
        // A hex disk intersects each axial row in one interval. Emit canonical columns directly
        // in tile order, splitting wrapped intervals at the seam instead of sorting a hex disk.
        int firstRow = (int)Math.Max(0, (long)center.R - radius);
        int lastRow = (int)Math.Min(Spec.Height - 1, (long)center.R + radius);
        for (int row = firstRow; row <= lastRow; row++)
        {
            long dr = (long)row - center.R;
            long offset = (long)center.Q + (row - (row & 1)) / 2;
            long first = offset + Math.Max(-(long)radius, -dr - radius);
            long last = offset + Math.Min(radius, -dr + radius);
            var columns = RangeColumns(first, last);
            for (int col = 0; col <= columns.PrefixLast; col++) yield return new TileId(row * Spec.Width + col);
            for (int col = columns.First; col <= columns.Last; col++) yield return new TileId(row * Spec.Width + col);
        }
    }

    private (int First, int Last, int PrefixLast) RangeColumns(long first, long last)
    {
        if (!Spec.Wrap)
            return ((int)Math.Clamp(first, 0, Spec.Width), (int)Math.Clamp(last, -1, Spec.Width - 1), -1);
        if (last - first + 1 >= Spec.Width) return (0, Spec.Width - 1, -1);
        int start = (int)((first % Spec.Width + Spec.Width) % Spec.Width);
        long end = start + last - first;
        return end < Spec.Width ? (start, (int)end, -1) : (start, Spec.Width - 1, (int)(end - Spec.Width));
    }
    /// <summary>Clipped shortest-distance ring (not duplicated wrap images), in ascending TileId order.</summary>
    public IEnumerable<TileId> Ring(TileId center, int radius) => Range(center, radius).Where(t => Distance(center, t) == radius);
    /// <summary>Range expressed as canonical axial coordinates.</summary>
    public IEnumerable<Hex> Range(Hex center, int radius) => Range(TileAt(center), radius).Select(HexOf);
    /// <summary>Ring expressed as canonical axial coordinates, in the same ascending TileId order.</summary>
    public IEnumerable<Hex> Ring(Hex center, int radius) => Ring(TileAt(center), radius).Select(HexOf);
}
