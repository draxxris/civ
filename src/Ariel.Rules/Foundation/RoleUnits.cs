using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Foundation;

/// <summary>Which unit fills a role (a city defender, a worker, an explorer) for a player: the best land unit its
/// civilization can train with the technologies it has. New-game setup grants these units, and a nation that breaks
/// away from its owner is given defenders the same way.</summary>
internal static class RoleUnits
{
    /// <summary>The unit that best fills the role for the player, or null when none qualifies. Only the civilization's
    /// resolved classes can supply a role, and tech prerequisites apply. Ties go to the lower id.</summary>
    internal static UnitDef? Best(Simulation sim, int player, string role)
    {
        var civ = sim.Content.Get<CivilizationDef>(sim.State.PlayerById(player).Civilization);
        return sim.Content.Get<UnitDef>().All
            .Where(u => civ.UnitOverrides.GetValueOrDefault(u.DefaultUnit, u.DefaultUnit) == u.Id)
            .Where(u => u.Cost >= 0 && u.Domain == "military:domain_land" && Eligible(u, role))
            .Where(u => sim.Content.Requirements.Evaluate(u.Requirements, new(sim, player)).Success)
            .OrderByDescending(u => Value(u, role)).ThenBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    private static bool Eligible(UnitDef unit, string role) => role switch
    {
        "military:unitai_worker" => unit.WorkRate > 0,
        "military:unitai_explore" => unit.Strength > 0 && !unit.NoRevealMap && !unit.CanFoundCity,
        "military:unitai_city_defense" => unit.Strength > 0 && !unit.NoDefensiveBonus,
        _ => unit.UnitAiTypes.GetValueOrDefault(role),
    };

    private static int Value(UnitDef unit, string role) => role switch
    {
        "military:unitai_worker" => unit.WorkRate,
        "military:unitai_explore" => unit.Moves * 100 + (unit.NoBadGoodies ? 100 : 0) + unit.Strength,
        _ => unit.Strength * (100 + unit.CityDefense),
    };

    /// <summary>The role a city's defenders fill.</summary>
    internal const string CityDefense = "military:unitai_city_defense";
}
