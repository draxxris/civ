namespace Civ.Client.Terrain;

/// <summary>Three values, one per vertex of a corner triangle.</summary>
public readonly record struct Blend3(double A, double B, double C)
{
    /// <summary>Component by vertex index 0..2.</summary>
    public double this[int k] => k switch { 0 => A, 1 => B, 2 => C, _ => throw new ArgumentOutOfRangeException(nameof(k)) };
    /// <summary>Sum of the three components.</summary>
    public double Sum => A + B + C;
}

/// <summary>The triangle of three mutually adjacent tile centres that contains a point, with the point's
/// barycentric coordinates. Hexes are unbounded axial coordinates; the caller resolves wrap and map edges.</summary>
public readonly record struct TileTriangle(Hex A, Hex B, Hex C, Blend3 Lambda)
{
    /// <summary>Vertex hex by index 0..2.</summary>
    public Hex this[int k] => k switch { 0 => A, 1 => B, 2 => C, _ => throw new ArgumentOutOfRangeException(nameof(k)) };
}

/// <summary>Dual-triangle hex blending. Tile centres form a triangular lattice; every point lies in one triangle of
/// three centres, and its barycentric coordinates are continuous across triangle borders because the vertex
/// opposite a border has λ = 0 there. The terrain shader repeats exactly this computation.</summary>
public static class HexBlend
{
    /// <summary>Default sharpness for material (texture and scatter) weights.</summary>
    public const double MaterialSharpness = 4;
    /// <summary>Default sharpness for peak height weights. Soft, so a peak's flanks slope down across its neighbours
    /// instead of rising as walls at the hex border.</summary>
    public const double PeakSharpness = 2;

    private static readonly double Sqrt3 = Math.Sqrt(3);

    /// <summary>Corner triangle containing world point (x, z) for pointy-top hexes of outer radius 1.</summary>
    public static TileTriangle CornerTriangle(double x, double z)
    {
        double q = Sqrt3 / 3 * x - z / 3, r = 2 * z / 3;
        double fi = Math.Floor(q), fj = Math.Floor(r);
        int i = (int)fi, j = (int)fj;
        double fq = q - fi, fr = r - fj;
        return fq + fr < 1
            ? new(new Hex(i, j), new Hex(i + 1, j), new Hex(i, j + 1), new(1 - fq - fr, fq, fr))
            : new(new Hex(i + 1, j + 1), new Hex(i, j + 1), new Hex(i + 1, j), new(fq + fr - 1, 1 - fq, 1 - fr));
    }

    /// <summary>Sharpened, normalised weights <c>w_k = λ_k^s / Σ λ_j^s</c>. They sum to 1, lie in [0, 1], equal λ at
    /// a centre (1), an edge midpoint (½, ½) and a corner (⅓ each), and stay continuous wherever λ is.</summary>
    public static Blend3 Weights(Blend3 lambda, double sharpness = MaterialSharpness)
    {
        double a = Math.Pow(Math.Clamp(lambda.A, 0, 1), sharpness);
        double b = Math.Pow(Math.Clamp(lambda.B, 0, 1), sharpness);
        double c = Math.Pow(Math.Clamp(lambda.C, 0, 1), sharpness);
        double sum = a + b + c;
        return sum > 0 ? new(a / sum, b / sum, c / sum) : new(1 / 3.0, 1 / 3.0, 1 / 3.0);
    }
}
