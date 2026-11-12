using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.RangedCombat;

/// <summary>The rules of bombardment and ranged strikes, built on the base game's combat and unit services.</summary>
internal static class RangedRules
{
    private const string AirDomain = "military:domain_air";

    private static UnitDef Definition(Simulation sim, Unit unit) => sim.Content.Get<UnitDef>(unit.Type);
    private static RuleConstantsDef Constants(Simulation sim) => sim.Content.Get<RuleConstantsDef>().All.Single();
    private static bool Enemy(Simulation sim, int player, int other) => sim.TeamOf(player).Wars.Contains(sim.TeamOf(other).Id);
    private static bool Friendly(Simulation sim, int player, int other) => sim.TeamOf(player).Id == sim.TeamOf(other).Id;

    private static string? ValidateFire(Simulation sim, int player, int unitId, Hex target)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Unit is not owned by player";
        if (unit.Health <= 0 || unit.MovesLeft <= 0) return "Unit cannot act";
        var state = unit.Extensions.Get<MilitaryUnitState>(sim.Content);
        if (state is { Attacked: true } or { TransportId: not null }) return "Unit has attacked or is cargo";
        if (Definition(sim, unit).Domain == AirDomain) return "Aircraft cannot use artillery attacks";
        return sim.State.Grid.TryTile(target, out _) ? null : "Target is outside the map";
    }

    private static int BombardRate(Simulation sim, Unit unit) =>
        Definition(sim, unit).BombardRate + sim.Rules().Units.Effect(sim, unit.Id, "bombard_rate");

    private static City? BombardTarget(Simulation sim, Unit unit, Hex target)
    {
        var city = sim.State.CityAt(target);
        return city is not null && Enemy(sim, unit.Owner, city.Owner) && sim.Rules().Combat.CityDefense(sim, city.Id) > 0 ? city : null;
    }

    public static string? ValidateBombard(Simulation sim, int player, int unitId, Hex target)
    {
        if (ValidateFire(sim, player, unitId, target) is { } error) return error;
        var unit = sim.State.UnitById(unitId);
        if (BombardRate(sim, unit) <= 0) return "Unit cannot bombard";
        if (sim.State.Grid.Distance(unit.Position, target) != 1) return "City must be adjacent";
        return BombardTarget(sim, unit, target) is null ? "No enemy city defenses to bombard" : null;
    }

    public static void BombardCity(Simulation sim, int unitId, Hex target)
    {
        var unit = sim.State.UnitById(unitId);
        if (ValidateBombard(sim, unit.Owner, unitId, target) is { } error) throw new InvalidOperationException(error);
        ReduceCityDefense(sim, unit, sim.State.CityAt(target)!);
        sim.Rules().Units.SpendAttack(sim, unitId, Constants(sim).Values.MoveDenominator);
    }

    private static void ReduceCityDefense(Simulation sim, Unit unit, City city)
    {
        var combat = sim.Rules().Combat;
        int resistance = Definition(sim, unit).IgnoreBuildingDefense ? 0 : combat.BombardResistance(sim, city.Id);
        combat.DamageCityDefense(sim, unit.Id, city.Id, Math.Max(1, BombardRate(sim, unit) * Math.Max(0, 100 - resistance) / 100));
    }

    public static string? ValidateRangedStrike(Simulation sim, int player, int unitId, Hex target)
    {
        if (ValidateFire(sim, player, unitId, target) is { } error) return error;
        var unit = sim.State.UnitById(unitId);
        int range = Definition(sim, unit).BombRange;
        int distance = sim.State.Grid.Distance(unit.Position, target);
        if (distance < 1 || distance > range) return "Target is outside ranged attack range";
        var plot = sim.State.PlotAt(target);
        if (!plot.Visible.Contains(sim.TeamOf(player).Id)) return "Target is not visible";
        if (plot.Owner is { } owner && !Friendly(sim, player, owner) && !Enemy(sim, player, owner))
            return "Cannot fire into neutral territory";
        if (sim.Rules().Combat.BestDefender(sim, unitId, target) is not null) return null;
        return BombardRate(sim, unit) > 0 && BombardTarget(sim, unit, target) is not null ? null : "No ranged attack target";
    }

    public static void RangedStrike(Simulation sim, int unitId, Hex target)
    {
        var unit = sim.State.UnitById(unitId);
        if (ValidateRangedStrike(sim, unit.Owner, unitId, target) is { } error) throw new InvalidOperationException(error);
        var rules = sim.Rules();
        var def = Definition(sim, unit);
        var defender = rules.Combat.BestDefender(sim, unitId, target) is { } id ? sim.State.UnitById(id) : null;
        int accuracy = Math.Clamp(def.BombAccuracyPercent > 0 ? def.BombAccuracyPercent : 50, 10, 100);
        bool hit = sim.Rng.Next(100) < accuracy;
        IReadOnlyList<CollateralHit> collateral = hit && defender is not null ? HitDefender(sim, unit, defender) : [];
        if (hit && BombardRate(sim, unit) > 0 && BombardTarget(sim, unit, target) is { } city) ReduceCityDefense(sim, unit, city);
        var constants = Constants(sim);
        int volleyCost = constants.Raw.TryGetValue("VOLLEY_MP_COST_PERCENT", out var raw) && int.TryParse(raw, out int parsed) ? parsed : 65;
        rules.Units.SpendAttack(sim, unitId,
            Math.Max(constants.Values.MoveDenominator, rules.Units.MaximumMoves(sim, unitId) * volleyCost / 100));
        rules.Combat.RecordStrike(sim, unitId, defender?.Id, target, collateral);
    }

    /// <summary>Half a combat round's damage, capped by the weapon's combat limit, plus collateral on the stack.</summary>
    private static IReadOnlyList<CollateralHit> HitDefender(Simulation sim, Unit unit, Unit defender)
    {
        var combat = sim.Rules().Combat;
        var preview = combat.Preview(sim, unit.Id, defender.Id);
        var collateral = combat.Collateral(sim, unit.Id, defender.Id);
        int remaining = Definition(sim, unit).CombatLimit - (Constants(sim).Values.MaxHitPoints - defender.Health);
        defender.Health -= Math.Max(0, Math.Min(Math.Max(1, preview.DamageToDefender / 2), remaining));
        return collateral;
    }
}
