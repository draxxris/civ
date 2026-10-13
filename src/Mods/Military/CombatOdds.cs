using System.Numerics;
using Civ.Game;

namespace Civ.Mods.Military;

/// <summary>Dynamic probability calculation uses integer fixed point with 24 decimal places.
/// Only the final display result rounds to millionths. No floating point or simulation RNG.</summary>
internal static class CombatOdds
{
    private static readonly BigInteger Scale = BigInteger.Pow(10, 24);
    internal static int Success(CombatPreview combat, int attackerHealth, int defenderHealth, int maximumHealth)
    {
        int aHits = (attackerHealth + combat.DamageToAttacker - 1) / combat.DamageToAttacker;
        int damage = maximumHealth - defenderHealth;
        int dHits = combat.CombatLimit < maximumHealth
            ? Math.Max(1, (combat.CombatLimit - damage) / combat.DamageToDefender + 1)
            : (defenderHealth + combat.DamageToDefender - 1) / combat.DamageToDefender;
        var memo = new Dictionary<(int A, int D, int Strikes), BigInteger>();
        BigInteger total = 0;
        for (int a = 0; a <= combat.AttackerChanceFirstStrikes; a++)
            for (int d = 0; d <= combat.DefenderChanceFirstStrikes; d++)
                total += Probability(aHits, dHits, combat.AttackerFirstStrikes + a - combat.DefenderFirstStrikes - d, combat, memo);
        total /= (combat.AttackerChanceFirstStrikes + 1) * (combat.DefenderChanceFirstStrikes + 1);
        return (int)((total * 1_000_000 + Scale / 2) / Scale);
    }
    private static BigInteger Probability(int a, int d, int strikes, CombatPreview combat,
        Dictionary<(int A, int D, int Strikes), BigInteger> memo)
    {
        if (d <= 0) return Scale;
        if (a <= 0) return 0;
        var key = (a, d, strikes);
        if (memo.TryGetValue(key, out var found)) return found;
        int remaining = strikes - Math.Sign(strikes);
        BigInteger loss = strikes > 0 ? Probability(a, d, remaining, combat, memo) : Probability(a - 1, d, remaining, combat, memo);
        BigInteger win = strikes < 0 ? Probability(a, d, remaining, combat, memo) : Probability(a, d - 1, remaining, combat, memo);
        var result = (loss * combat.DefenderRoundOdds + win * (combat.DieSides - combat.DefenderRoundOdds)) / combat.DieSides;
        memo[key] = result;
        return result;
    }
}
