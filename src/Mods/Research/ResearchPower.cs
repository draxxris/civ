using System.Numerics;

namespace Ariel.Mods.Research;

/// <summary>Integer fixed-point powers for the three reference research curves.</summary>
internal static class ResearchPower
{
    // Q36. Constants are natural logarithms truncated at 36 decimal places.
    private static readonly BigInteger Unit = BigInteger.Pow(10, 36);
    private static readonly BigInteger Ln2 = BigInteger.Parse("693147180559945309417232121458176568");
    private static readonly BigInteger Ln85 = BigInteger.Parse("-162518929497774913185688958269414240");
    private static readonly BigInteger Ln98 = BigInteger.Parse("-20202707317519448408045301024192387");

    internal static long TimelinePercent(BigInteger numerator, BigInteger denominator)
    {
        long raw = checked((long)(100 * Exp(numerator * (2 * Ln2) / denominator) / Unit));
        return checked((raw + 10) * 10 / 11);
    }

    internal static long DiffusionBonus(int maximum, BigInteger numerator, BigInteger denominator) =>
        DecayBonus(maximum, 85, numerator, denominator, Ln85);

    internal static long WelfareBonus(int maximum, int deficit) => DecayBonus(maximum, 98, deficit, 1, Ln98);

    private static long DecayBonus(int maximum, int basis, BigInteger numerator, BigInteger denominator, BigInteger logarithm)
    {
        // Integer powers have exact rational results. Preserve half-up ties such as 50 * .85 = 42.5;
        // a truncated exponential approximation would put that value just below the tie.
        var exponent = BigInteger.DivRem(numerator, denominator, out var remainder);
        if (remainder == 0 && exponent >= 0 && exponent <= 4096)
        {
            var divisor = BigInteger.Pow(100, (int)exponent);
            var dividend = Math.Max(0, maximum) * BigInteger.Pow(basis, (int)exponent);
            return Math.Max(0, maximum - checked((long)((2 * dividend + divisor) / (2 * divisor))));
        }
        return Bonus(maximum, numerator * logarithm / denominator);
    }

    private static long Bonus(int maximum, BigInteger logarithm)
    {
        if (maximum <= 0) return 0;
        // The reference rounds the decaying term to nearest, with halves rounded up.
        long decayed = checked((long)((maximum * Exp(logarithm) + Unit / 2) / Unit));
        return Math.Max(0, maximum - decayed);
    }

    private static BigInteger Exp(BigInteger logarithm)
    {
        // Reduce to 0 <= remainder < ln(2), then use the positive Taylor series.
        // Every division truncates. Within the supported range the relative error is < 1e-30.
        var shift = BigInteger.DivRem(logarithm, Ln2, out var remainder);
        if (remainder < 0) { shift--; remainder += Ln2; }
        // Positive overflow is an error, not a gameplay cap. Negative underflow cannot
        // affect any integer result with the supported Int32 multipliers (< 1e-60).
        if (shift > 128) throw new OverflowException("Research power exceeds the supported cost range");
        if (shift < -256) return 0;
        BigInteger sum = Unit, term = Unit;
        for (int divisor = 1; term > 0; divisor++)
        {
            term = term * remainder / (Unit * divisor);
            sum += term;
        }
        int bits = (int)shift;
        return bits >= 0 ? sum << bits : sum >> -bits;
    }
}
