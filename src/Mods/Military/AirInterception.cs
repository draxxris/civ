using Ariel.Game;

namespace Ariel.Mods.Military;

internal sealed partial class AirMissionService
{
    private AirMissionReport? TryIntercept(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        int evasion = Math.Clamp(MilitaryData.Definition(sim, unit).EvasionPercent
            + MilitaryData.Effect(MilitaryData.Effects(sim, unit), "evasion"), 0, 100);
        if (sim.Rng.Next(100) < evasion) return null;
        var interceptor = BestInterceptor(sim, unit, target);
        if (interceptor is null || sim.Rng.Next(100) >= InterceptionProbability(sim, interceptor.Id)) return null;
        return ResolveInterception(sim, unit, interceptor, target, report);
    }
    private Unit? BestInterceptor(Simulation sim, Unit unit, Hex target) => sim.State.Units
        .Where(u => CanIntercept(sim, unit, u, target))
        .OrderByDescending(u => InterceptionProbability(sim, u.Id)).ThenBy(u => u.Id).FirstOrDefault();

    private bool CanIntercept(Simulation sim, Unit attacker, Unit candidate, Hex target)
    {
        if (candidate.Health <= 0 || !MilitaryData.Enemy(sim, attacker.Owner, candidate.Owner)) return false;
        if (InterceptionProbability(sim, candidate.Id) <= 0) return false;
        var state = MilitaryData.Read(sim, candidate);
        if (state.InterceptedThisTurn) return false;
        var def = MilitaryData.Definition(sim, candidate);
        if (def.Domain == "military:domain_air")
        {
            if (state.Moved || state.Activity != UnitActivity.Intercept) return false;
        }
        else if (state.TransportId is not null) return false;
        return sim.State.Grid.Distance(candidate.Position, target) <= AirRange(sim, candidate.Id);
    }
    // Five-round interception uses probability-based damage. Ground interceptors take no return fire.
    private AirMissionReport ResolveInterception(Simulation sim, Unit unit, Unit interceptor, Hex target, AirMissionReport report)
    {
        int attackerHealth = unit.Health, interceptorHealth = interceptor.Health;
        int ourStrength = InterceptionStrength(sim, unit), theirStrength = InterceptionStrength(sim, interceptor);
        int odds = 100 * ourStrength / Math.Max(1, ourStrength + theirStrength);
        int maximumDamage = MilitaryData.Raw(sim, "MAX_INTERCEPTION_DAMAGE", 50);
        int damageToAttacker = InterceptionProbability(sim, interceptor.Id) * maximumDamage / 100;
        int damageToInterceptor = Math.Max(MilitaryData.Raw(sim, "MIN_INTERCEPTION_DAMAGE", 10), InterceptionProbability(sim, unit.Id) * maximumDamage / 100);
        bool airInterceptor = MilitaryData.Definition(sim, interceptor).Domain == "military:domain_air";
        int rounds = MilitaryData.Raw(sim, "INTERCEPTION_MAX_ROUNDS", 5);
        for (int round = 0; round < rounds; round++)
        {
            if (sim.Rng.Next(100) < odds)
            {
                if (airInterceptor) interceptor.Health = Math.Max(0, interceptor.Health - damageToInterceptor);
            }
            else unit.Health = Math.Max(0, unit.Health - damageToAttacker);
            if (unit.Health <= 0 || interceptor.Health <= 0) break;
        }
        if (!airInterceptor) MilitaryData.Edit(sim, interceptor).InterceptedThisTurn = true;
        AwardInterceptionExperience(sim, unit, interceptor, target, ourStrength, theirStrength, attackerHealth, interceptorHealth);
        if (unit.Health <= 0) MilitaryData.RemoveUnit(sim, unit.Id);
        if (interceptor.Health <= 0) MilitaryData.RemoveUnit(sim, interceptor.Id);
        return report with { Interceptor = interceptor.Id, AttackerDamage = attackerHealth - unit.Health, InterceptorDamage = interceptorHealth - interceptor.Health };
    }
    private static int InterceptionStrength(Simulation sim, Unit unit)
    {
        var def = MilitaryData.Definition(sim, unit);
        int strength = def.Domain == "military:domain_air" ? def.AirCombat : def.Strength;
        int modifier = MilitaryData.Effect(MilitaryData.Effects(sim, unit), "combat_percent");
        int maximum = modifier >= 0 ? strength * (100 + modifier) : strength * 10000 / (100 - modifier);
        return Math.Max(1, maximum * unit.Health / MilitaryData.Constants(sim).MaxHitPoints);
    }
    private static void AwardInterceptionExperience(Simulation sim, Unit unit, Unit interceptor, Hex target,
        int ourStrength, int theirStrength, int attackerHealth, int interceptorHealth)
    {
        if (unit.Health <= 0)
        {
            int amount = MilitaryData.Definition(sim, unit).AttackExperience * ourStrength / Math.Max(1, theirStrength);
            CombatService.Experience(sim, interceptor, unit, target, ClampExperience(sim, amount));
        }
        else if (interceptor.Health <= 0)
        {
            int amount = MilitaryData.Definition(sim, interceptor).DefenseExperience * theirStrength / Math.Max(1, ourStrength);
            CombatService.Experience(sim, unit, interceptor, target, ClampExperience(sim, amount));
        }
        else if (unit.Health < attackerHealth)
            CombatService.Experience(sim, interceptor, unit, target, MilitaryData.Raw(sim, "EXPERIENCE_FROM_WITHDRAWL", 1));
        else if (interceptor.Health < interceptorHealth)
            CombatService.Experience(sim, unit, interceptor, target, MilitaryData.Raw(sim, "EXPERIENCE_FROM_WITHDRAWL", 1));
    }
    private static int ClampExperience(Simulation sim, int amount) => Math.Clamp(amount,
        MilitaryData.Constants(sim).MinExperiencePerCombat, MilitaryData.Constants(sim).MaxExperiencePerCombat);
}
