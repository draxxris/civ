namespace Ariel.Mods.Ai;

/// <summary>Integer cube-coordinate line across the closest horizontal wrap image.</summary>
internal static class HexLine
{
    public static IEnumerable<Hex> Between(HexGrid grid, Hex a, Hex b)
    {
        if (grid.Spec.Wrap) b = new[] { b, new Hex(b.Q - grid.Spec.Width, b.R), new Hex(b.Q + grid.Spec.Width, b.R) }
            .OrderBy(h => a.DistanceTo(h)).ThenBy(h => h.Q).First();
        int n = a.DistanceTo(b);
        for (int i = 0; i <= n; i++) yield return Lerp(a, b, i, Math.Max(1, n));
    }

    private static Hex Lerp(Hex a, Hex b, int i, int n)
    {
        long nq = (long)a.Q * (n - i) + (long)b.Q * i, nr = (long)a.R * (n - i) + (long)b.R * i, ns = (long)a.S * (n - i) + (long)b.S * i;
        long q = Round(nq, n), r = Round(nr, n), s = Round(ns, n);
        long dq = Math.Abs(q * n - nq), dr = Math.Abs(r * n - nr), ds = Math.Abs(s * n - ns);
        if (dq > dr && dq > ds) q = -r - s;
        else if (dr > ds) r = -q - s;
        return new((int)q, (int)r);
    }

    private static long Round(long numerator, long n)
    {
        long value = 2 * numerator + n, denominator = 2 * n;
        return value / denominator - (value < 0 && value % denominator != 0 ? 1 : 0);
    }
}
