using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Military;

internal sealed partial class AirMissionService : IAirMissionService
{
    public int AirRange(Simulation sim, int unitId)
    {
        var unit = sim.State.UnitById(unitId);
        return Math.Max(0, MilitaryData.Definition(sim, unit).AirRange + MilitaryData.Effect(MilitaryData.Effects(sim, unit), "air_range"));
    }
    public int InterceptionProbability(Simulation sim, int unitId)
    {
        var unit = sim.State.UnitById(unitId);
        int probability = Math.Clamp(MilitaryData.Definition(sim, unit).InterceptionPercent
            + MilitaryData.Effect(MilitaryData.Effects(sim, unit), "intercept"), 0, MilitaryData.Raw(sim, "MAX_INTERCEPTION_PROBABILITY", 100));
        return probability * unit.Health / MilitaryData.Constants(sim).MaxHitPoints;
    }
    public string? Validate(Simulation sim, int player, int unitId, AirMissionKind kind, Hex target)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Not your unit";
        if (ValidateAircraft(sim, unit) is { } error) return error;
        if (!sim.State.Grid.TryTile(target, out var tile)) return "Target is outside the map";
        target = sim.State.Grid.HexOf(tile);
        int distance = sim.State.Grid.Distance(unit.Position, target);
        int range = AirRange(sim, unitId) * (kind == AirMissionKind.Rebase ? 2 : 1);
        if (distance < 1 || distance > range) return "Target is outside air mission range";
        return kind switch
        {
            AirMissionKind.Rebase => ValidateRebase(sim, unit, target),
            AirMissionKind.Recon => MilitaryData.Definition(sim, unit).Suicide ? "Missiles cannot perform reconnaissance" : null,
            AirMissionKind.AirStrike => ValidateStrike(sim, unit, target),
            AirMissionKind.AirBomb => ValidateBomb(sim, unit, target),
            _ => "Unknown air mission",
        };
    }
    private static string? ValidateAircraft(Simulation sim, Unit unit)
    {
        if (unit.Health <= 0 || unit.MovesLeft <= 0) return "Aircraft cannot act";
        if (MilitaryData.Definition(sim, unit).Domain != "military:domain_air") return "Unit is not an aircraft";
        return MilitaryData.Read(sim, unit).Attacked ? "Aircraft has attacked" : null;
    }
    private static string? ValidateRebase(Simulation sim, Unit unit, Hex target) =>
        UnitService.AirBaseError(sim, unit, target) is { } error && CarrierAt(sim, unit, target) is null ? error : null;

    private static Unit? CarrierAt(Simulation sim, Unit unit, Hex target) => sim.State.UnitsAt(target).OrderBy(u => u.Id)
        .FirstOrDefault(u => UnitService.LoadError(sim, unit, u, target) is null);

    private static string? ValidateStrike(Simulation sim, Unit unit, Hex target)
    {
        if (MilitaryData.Definition(sim, unit).AirCombat <= 0) return "Aircraft cannot strike";
        if (!sim.State.PlotAt(target).Visible.Contains(sim.TeamOf(unit.Owner).Id)) return "Target is not visible";
        return AirStrikeTarget(sim, unit, target) is null ? "No air strike target" : null;
    }
    private static string? ValidateBomb(Simulation sim, Unit unit, Hex target)
    {
        if (MilitaryData.Definition(sim, unit).BombRate <= 0) return "Aircraft cannot bomb";
        var plot = sim.State.PlotAt(target);
        if (!plot.Visible.Contains(sim.TeamOf(unit.Owner).Id)) return "Target is not visible";
        if (plot.Owner is not { } owner || !MilitaryData.Enemy(sim, unit.Owner, owner)) return "Target is not enemy territory";
        if (sim.State.CityAt(target) is { } city)
            return CombatCalculator.CityDefense(sim, city, false) > 0 ? null : "City has no defenses";
        return plot.Improvement is { } id && !sim.Content.Get<S.ImprovementDef>(id).Goody ? null : "No improvement to bomb";
    }
    private static Unit? AirStrikeTarget(Simulation sim, Unit unit, Hex target)
    {
        int limit = MilitaryData.Definition(sim, unit).AirCombatLimit;
        int maximum = MilitaryData.Constants(sim).MaxHitPoints;
        return sim.State.UnitsAt(target).Where(u => CombatService.EligibleDefender(sim, unit, u) && maximum - u.Health < limit)
            .OrderByDescending(u => CombatCalculator.Calculate(sim, unit, u).DefenseStrength.Current).ThenBy(u => u.Id).FirstOrDefault();
    }
    public AirMissionReport Execute(Simulation sim, int unitId, AirMissionKind kind, Hex target)
    {
        var unit = sim.State.UnitById(unitId);
        if (Validate(sim, unit.Owner, unitId, kind, target) is { } error) throw new InvalidOperationException(error);
        target = sim.State.Grid.Normalize(target);
        var report = new AirMissionReport(kind, unitId, target, null, 0, 0, null, 0, 0, null);
        report = kind switch
        {
            AirMissionKind.Rebase => Rebase(sim, unit, target, report),
            AirMissionKind.Recon => Recon(sim, unit, target, report),
            _ => Attack(sim, unit, target, report),
        };
        MilitaryData.Sight(sim);
        sim.Events.Publish(new AirMissionResolved(report));
        return report;
    }
    private static AirMissionReport Rebase(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        // A land base takes precedence over a carrier on the same plot.
        var carrier = UnitService.AirBaseError(sim, unit, target) is null ? null : CarrierAt(sim, unit, target);
        var state = MilitaryData.Edit(sim, unit);
        state.TransportId = carrier?.Id;
        MilitaryData.MoveUnit(sim, unit.Id, target);
        SpendMoves(sim, unit, finish: true);
        return report;
    }
    private static AirMissionReport Recon(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        int radius = sim.Content.Get<S.HexOverridesDef>("foundation:hex_overrides").UnitSight + 1;
        CombatService.HistoryState(sim).ReconSights.Add(new(sim.TeamOf(unit.Owner).Id, target, radius));
        SpendMoves(sim, unit, finish: true);
        return report;
    }
    private AirMissionReport Attack(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        var intercepted = TryIntercept(sim, unit, target, report);
        var result = intercepted
            ?? (report.Kind == AirMissionKind.AirStrike ? Strike(sim, unit, target, report) : Bomb(sim, unit, target, report));
        SpendMoves(sim, unit, finish: false);
        if (intercepted is null && MilitaryData.Definition(sim, unit).Suicide && sim.State.FindUnit(unit.Id) is not null)
            MilitaryData.RemoveUnit(sim, unit.Id);
        return result;
    }
    private static void SpendMoves(Simulation sim, Unit unit, bool finish)
    {
        var state = MilitaryData.Edit(sim, unit);
        state.Attacked |= !finish;
        state.Moved = true; state.FortifyTurns = 0; state.Activity = UnitActivity.Awake;
        unit.MovesLeft = finish ? 0 : Math.Max(0, unit.MovesLeft - MilitaryData.Constants(sim).MoveDenominator);
    }
    private static AirMissionReport Strike(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        var defender = AirStrikeTarget(sim, unit, target)!;
        int maximum = MilitaryData.Constants(sim).MaxHitPoints;
        int airStrength = MilitaryData.Definition(sim, unit).AirCombat * unit.Health / maximum;
        int defense = CombatCalculator.Calculate(sim, unit, defender).DefenseStrength.Current / 100;
        int factor = (airStrength + defense + 1) / 2;
        int damage = Math.Max(1, MilitaryData.Constants(sim).AirCombatDamage * (airStrength + factor) / Math.Max(1, defense + factor));
        if (sim.State.CityAt(target) is { } city)
            damage = damage * Math.Max(0, 100 + MilitaryData.Buildings(sim, city).Sum(b => b.AirModifier)) / 100;
        int remaining = MilitaryData.Definition(sim, unit).AirCombatLimit - (maximum - defender.Health);
        damage = Math.Max(0, Math.Min(defender.Health, Math.Min(damage, remaining)));
        CombatService.Collateral(sim, unit, defender);
        defender.Health -= damage;
        if (defender.Health <= 0) MilitaryData.RemoveUnit(sim, defender.Id);
        return report with { TargetUnit = defender.Id, TargetDamage = damage };
    }
    private static AirMissionReport Bomb(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        var def = MilitaryData.Definition(sim, unit);
        int rate = Math.Max(1, def.BombRate * unit.Health / MilitaryData.Constants(sim).MaxHitPoints);
        if (sim.State.CityAt(target) is { } city)
        {
            int resistance = MilitaryData.Buildings(sim, city).Sum(b => b.BombardDefense);
            int reduction = rate * Math.Max(0, 100 - resistance) / 100;
            return report with { CityDefenseReduction = CombatService.ReduceCityDefense(sim, unit, city, reduction) };
        }
        var plot = sim.State.PlotAt(target);
        string id = plot.Improvement!;
        var improvement = sim.Content.Get<S.ImprovementDef>(id);
        if (sim.Rng.Next(rate) < sim.Rng.Next(Math.Max(1, improvement.AirBombDefense))) return report;
        plot.Improvement = improvement.ImprovementPillage; plot.ImprovementTurns = 0; plot.BuildProgress.Clear();
        return report with { DestroyedImprovement = id };
    }
}
