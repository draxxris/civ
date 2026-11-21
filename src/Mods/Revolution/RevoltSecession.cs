using Ariel.Game;

namespace Ariel.Mods.Revolution;

/// <summary>A nation's city that stays in revolt long enough breaks away as a new nation. A city is in revolt while
/// it is at the mutiny tier or above, or while the unrest a rebellion leaves lasts. The count holds while the city is
/// only at the warning tier, so the lull after a rebellion does not start it over, and clears once the city is calm.</summary>
internal static class RevoltSecession
{
    internal static void Run(Simulation sim, City city, RevolutionRulesDef rules)
    {
        if (rules.SecessionTurns <= 0 || !PlayerRules.IsNation(sim, city.Owner)) return;
        var read = RevolutionService.Read(sim, city);
        // A city that may not secede (a capital, a last city) does not count, so it never breaks away the moment
        // that changes with no warning on its screen.
        if (sim.Rules().CityCapture.ValidateSecession(sim, city.Id) is not null)
        {
            if (read.RevoltTurns > 0) RevolutionService.Edit(sim, city).RevoltTurns = 0;
            return;
        }
        bool unrest = RevolutionService.UnrestEffect(sim, city) is not null;
        if (read.InstabilityIndex >= rules.MutinyThreshold || unrest) RevolutionService.Edit(sim, city).RevoltTurns++;
        else if (read.InstabilityIndex < rules.WarningThreshold && read.RevoltTurns > 0) RevolutionService.Edit(sim, city).RevoltTurns = 0;
        if (TurnsLeft(sim, city, RevolutionService.Read(sim, city), rules) != 0) return;
        // The Revolution state is reset by the secession itself: the new nation's capital starts calm.
        sim.Rules().CityCapture.Secede(sim, city.Id, new SecessionTerms(rules.SecessionWar, rules.RebelUnit, rules.RebelUnits));
    }

    /// <summary>Turn ends in revolt left before the city breaks away; null while it is not counting or may not secede
    /// (a capital, a last city, no nation left to become).</summary>
    internal static int? TurnsLeft(Simulation sim, City city, RevolutionCityState state, RevolutionRulesDef rules)
    {
        if (rules.SecessionTurns <= 0 || state.RevoltTurns <= 0 || !PlayerRules.IsNation(sim, city.Owner)) return null;
        if (sim.Rules().CityCapture.ValidateSecession(sim, city.Id) is not null) return null;
        return Math.Max(0, GamePace.Scale(sim, rules.SecessionTurns) - state.RevoltTurns);
    }
}
