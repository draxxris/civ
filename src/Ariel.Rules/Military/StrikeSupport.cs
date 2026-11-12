using Ariel.Game;

namespace Ariel.Rules.Military;

/// <summary>What fire from a distance acts on: city defences, collateral damage and the combat record. The orders
/// that fire (bombardment, ranged strikes, air bombing) live with their features and call these.</summary>
internal sealed partial class CombatService
{
    public int CityDefense(Simulation sim, int city) => CombatCalculator.CityDefense(sim, sim.State.CityById(city), false);

    public int BombardResistance(Simulation sim, int city) =>
        MilitaryData.Buildings(sim, sim.State.CityById(city)).Sum(b => b.BombardDefense);

    public int DamageCityDefense(Simulation sim, int unit, int city, int reduction) =>
        ReduceCityDefense(sim, sim.State.UnitById(unit), sim.State.CityById(city), reduction);

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

    public IReadOnlyList<CollateralHit> Collateral(Simulation sim, int attacker, int defender) =>
        Collateral(sim, sim.State.UnitById(attacker), sim.State.UnitById(defender));

    public CombatReport RecordStrike(Simulation sim, int attacker, int? defender, Hex target, IReadOnlyList<CollateralHit> collateral)
    {
        var unit = sim.State.UnitById(attacker);
        var hit = defender is { } id ? sim.State.UnitById(id) : null;
        bool died = hit?.Health <= 0;
        var report = new CombatReport(sim.State.Turn, unit.Id, hit?.Id ?? -1,
            died ? CombatOutcome.AttackerWon : CombatOutcome.LimitedDamage,
            hit is null ? [] : [new(1, unit.Health, hit.Health, false)], collateral,
            unit.Type, unit.Owner, unit.Position, hit?.Type ?? "", hit?.Owner ?? -1, target, died);
        if (died) MilitaryData.RemoveUnit(sim, hit!.Id);
        HistoryState(sim).Combats.Add(report);
        // City-only fire has no defeated unit and must not trigger unit-victory rewards.
        if (hit is not null) sim.Events.Publish(new CombatResolved(unit.Id, hit.Id, unit.Id, hit.Owner, report));
        MilitaryData.Sight(sim);
        return report;
    }
}
