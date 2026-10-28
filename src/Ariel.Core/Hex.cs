namespace Ariel;

/// <summary>Axial hex coordinate (pointy-top layout). Cube S is derived.</summary>
public readonly record struct Hex(int Q, int R)
{
    /// <summary>Derived cube coordinate.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int S => -Q - R;

    /// <summary>East, northeast, northwest, west, southwest and southeast deltas.</summary>
    public static IReadOnlyList<Hex> Directions { get; } = Array.AsReadOnly<Hex>([
        new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1),
    ]);

    /// <summary>Coordinate addition.</summary>
    public static Hex operator +(Hex a, Hex b) => new(a.Q + b.Q, a.R + b.R);
    /// <summary>Coordinate displacement.</summary>
    public static Hex operator -(Hex a, Hex b) => new(a.Q - b.Q, a.R - b.R);
    /// <summary>Scale a displacement.</summary>
    public static Hex operator *(Hex a, int k) => new(a.Q * k, a.R * k);

    /// <summary>Unbounded neighbor; direction indices normalize modulo six.</summary>
    public Hex Neighbor(int direction) => this + Directions[((direction % 6) + 6) % 6];

    /// <summary>Unbounded neighbors in direction order; map rules should use HexGrid.Neighbors.</summary>
    public IEnumerable<Hex> Neighbors()
    {
        for (int i = 0; i < 6; i++) yield return Neighbor(i);
    }

    /// <summary>Unwrapped cube distance; map rules should use HexGrid.Distance.</summary>
    public int DistanceTo(Hex other)
    {
        var d = this - other;
        return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.S)) / 2;
    }

    /// <summary>All hexes within <paramref name="radius"/> of <paramref name="center"/>, in a stable order.</summary>
    public static IEnumerable<Hex> Range(Hex center, int radius)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Math.Max(-radius, -q - radius);
            int r2 = Math.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++) yield return center + new Hex(q, r);
        }
    }

    /// <summary>Unbounded ring in counter-clockwise traversal order as drawn with +Y down (pointy-top layout), starting at the south-west corner.</summary>
    public static IEnumerable<Hex> Ring(Hex center, int radius)
    {
        if (radius == 0) { yield return center; yield break; }
        var h = center + Directions[4] * radius;
        for (int side = 0; side < 6; side++)
            for (int step = 0; step < radius; step++)
            {
                yield return h;
                h = h.Neighbor(side);
            }
    }

    /// <summary>Center of this hex in 2D space for pointy-top hexes of the given outer radius.</summary>
    public (double X, double Y) ToPixel(double size) =>
        (size * Math.Sqrt(3) * (Q + R / 2.0), size * 1.5 * R);

    /// <summary>Round a pointy-top pixel position to its axial plot; presentation only.</summary>
    public static Hex FromPixel(double x, double y, double size)
    {
        double q = (Math.Sqrt(3) / 3 * x - 1.0 / 3 * y) / size;
        double r = (2.0 / 3 * y) / size;
        return Round(q, r);
    }

    private static Hex Round(double q, double r)
    {
        double s = -q - r;
        int rq = (int)Math.Round(q), rr = (int)Math.Round(r), rs = (int)Math.Round(s);
        double dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);
        if (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds) rr = -rq - rs;
        return new Hex(rq, rr);
    }
}
