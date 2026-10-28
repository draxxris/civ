using Ariel.Game;

namespace Ariel.Mods.Military;

internal sealed partial class CombatService
{
    private static string? ValidateFire(Simulation sim, int player, int unitId, Hex target)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Unit is not owned by player";
        if (unit.Health <= 0 || unit.MovesLeft <= 0) return "Unit cannot act";
        var state = MilitaryData.Read(sim, unit);
        if (state.Attacked || state.TransportId is not null) return "Unit has attacked or is cargo";
        if (MilitaryData.Definition(sim, unit).Domain == "military:domain_air") return "Aircraft cannot use artillery attacks";
        return sim.State.Grid.TryTile(target, out _) ? null : "Target is outside the map";
    }
    private static int BombardRate(Simulation sim, Unit unit) => MilitaryData.Definition(sim, unit).BombardRate
        + MilitaryData.Effect(MilitaryData.Effects(sim, unit), "bombard_rate");
    private static City? BombardTarget(Simulation sim, Unit unit, Hex target)
    {
        var city = sim.State.CityAt(target);
        return city is not null && MilitaryData.Enemy(sim, unit.Owner, city.Owner)
            && CombatCalculator.CityDefense(sim, city, false) > 0 ? city : null;
    }
    public string? ValidateBombard(Simulation sim, int player, int unitId, Hex target)
    {
        if (ValidateFire(sim, player, unitId, target) is { } error) return error;
        var unit = sim.State.UnitById(unitId);
        if (BombardRate(sim, unit) <= 0) return "Unit cannot bombard";
        if (sim.State.Grid.Distance(unit.Position, target) != 1) return "City must be adjacent";
        return BombardTarget(sim, unit, target) is null ? "No enemy city defenses to bombard" : null;
    }
    public int BombardCity(Simulation sim, int unitId, Hex target)
    {
        var unit = sim.State.UnitById(unitId);
        if (ValidateBombard(sim, unit.Owner, unitId, target) is { } error) throw new InvalidOperationException(error);
        int reduction = ReduceCityDefense(sim, unit, sim.State.CityAt(target)!);
        SpendFireMoves(sim, unit, MilitaryData.Constants(sim).MoveDenominator);
        return reduction;
    }
    private static int ReduceCityDefense(Simulation sim, Unit unit, City city)
    {
        var def = MilitaryData.Definition(sim, unit);
        int resistance = def.IgnoreBuildingDefense ? 0 : MilitaryData.Buildings(sim, city).Sum(b => b.BombardDefense);
        int reduction = Math.Max(1, BombardRate(sim, unit) * Math.Max(0, 100 - resistance) / 100);
        return ReduceCityDefense(sim, unit, city, reduction);
    }
    internal static int ReduceCityDefense(Simulation sim, Unit unit, City city, int reduction)
    {
        int maximum = Math.Max(1, MilitaryData.Constants(sim).MaximumCityDefenseDamage);
        int total = CombatCalculator.TotalCityDefense(sim, city, false);
        int delta = reduction <= 0 ? 0 : Math.Max(1, (maximum * reduction + total / 2) / Math.Max(1, total));
        var state = city.Extensions.Get<CityRuleState>(sim.Content) ?? new();
        city.Extensions.Set(sim.Content, state);
        state.DefenseDamage = Math.Clamp(state.DefenseDamage + delta, 0, maximum);
        state.BombardedThisTurn = true;
        sim.Events.Publish(new CityBombarded(unit.Id, city.Id, reduction, state.DefenseDamage));
        return reduction;
    }
    public string? ValidateRangedStrike(Simulation sim, int player, int unitId, Hex target)
    {
        if (ValidateFire(sim, player, unitId, target) is { } error) return error;
        var unit = sim.State.UnitById(unitId);
        int range = MilitaryData.Definition(sim, unit).BombRange;
        int distance = sim.State.Grid.Distance(unit.Position, target);
        if (distance < 1 || distance > range) return "Target is outside ranged attack range";
        var plot = sim.State.PlotAt(target);
        if (!plot.Visible.Contains(sim.TeamOf(player).Id)) return "Target is not visible";
        if (plot.Owner is { } owner && !MilitaryData.Friendly(sim, player, owner) && !MilitaryData.Enemy(sim, player, owner))
            return "Cannot fire into neutral territory";
        if (BestDefender(sim, unitId, target) is not null) return null;
        return BombardRate(sim, unit) > 0 && BombardTarget(sim, unit, target) is not null ? null : "No ranged attack target";
    }
    public CombatReport RangedStrike(Simulation sim, int unitId, Hex target)
    {
        var unit = sim.State.UnitById(unitId);
        if (ValidateRangedStrike(sim, unit.Owner, unitId, target) is { } error) throw new InvalidOperationException(error);
        var def = MilitaryData.Definition(sim, unit);
        var defender = BestDefender(sim, unitId, target) is { } id ? sim.State.UnitById(id) : null;
        int accuracy = Math.Clamp(def.BombAccuracyPercent > 0 ? def.BombAccuracyPercent : 50, 10, 100);
        bool hit = sim.Rng.Next(100) < accuracy;
        var collateral = hit && defender is not null ? HitRangedDefender(sim, unit, defender) : [];
        if (hit && BombardRate(sim, unit) > 0 && BombardTarget(sim, unit, target) is { } city)
            ReduceCityDefense(sim, unit, city);
        int denominator = MilitaryData.Constants(sim).MoveDenominator;
        int maximumMoves = sim.Content.Services.Get<IUnitService>().Single().MaximumMoves(sim, unitId);
        SpendFireMoves(sim, unit, Math.Max(denominator, maximumMoves * MilitaryData.Raw(sim, "VOLLEY_MP_COST_PERCENT", 65) / 100));
        return RecordRangedStrike(sim, unit, defender, target, collateral);
    }
    private IReadOnlyList<CollateralHit> HitRangedDefender(Simulation sim, Unit unit, Unit defender)
    {
        var preview = Preview(sim, unit.Id, defender.Id);
        var collateral = Collateral(sim, unit, defender);
        int remaining = MilitaryData.Definition(sim, unit).CombatLimit - (MilitaryData.Constants(sim).MaxHitPoints - defender.Health);
        defender.Health -= Math.Max(0, Math.Min(Math.Max(1, preview.DamageToDefender / 2), remaining));
        return collateral;
    }
    private static CombatReport RecordRangedStrike(Simulation sim, Unit unit, Unit? defender, Hex target, IReadOnlyList<CollateralHit> collateral)
    {
        bool died = defender?.Health <= 0;
        var report = new CombatReport(sim.State.Turn, unit.Id, defender?.Id ?? -1,
            died ? CombatOutcome.AttackerWon : CombatOutcome.LimitedDamage,
            defender is null ? [] : [new(1, unit.Health, defender.Health, false)], collateral,
            unit.Type, unit.Owner, unit.Position, defender?.Type ?? "", defender?.Owner ?? -1, target, died);
        if (died) MilitaryData.RemoveUnit(sim, defender!.Id);
        HistoryState(sim).Combats.Add(report);
        // City-only fire has no defeated unit and must not trigger unit-victory rewards.
        if (defender is not null) sim.Events.Publish(new CombatResolved(unit.Id, defender.Id, unit.Id, defender.Owner, report));
        MilitaryData.Sight(sim);
        return report;
    }
    private static void SpendFireMoves(Simulation sim, Unit unit, int cost)
    {
        var state = MilitaryData.Edit(sim, unit);
        state.Attacked = true; state.Moved = true; state.FortifyTurns = 0; state.Activity = UnitActivity.Awake;
        unit.MovesLeft = Math.Max(0, unit.MovesLeft - cost);
    }
}
