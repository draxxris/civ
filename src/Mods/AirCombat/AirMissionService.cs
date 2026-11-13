using System.Globalization;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.AirCombat;

/// <summary>Rebasing, reconnaissance, air strikes and bombing, built on the base game's unit, combat and visibility
/// services. Interception is in <c>AirInterception.cs</c>.</summary>
internal sealed partial class AirMissionService : IAirMissionService, IAirOperations
{
    internal const string AirDomain = "military:domain_air";

    internal static S.UnitDef Definition(Simulation sim, Unit unit) => sim.Content.Get<S.UnitDef>(unit.Type);
    internal static S.RuleConstants Constants(Simulation sim) => sim.Content.Get<S.RuleConstantsDef>().All.Single().Values;
    internal static int Raw(Simulation sim, string key, int fallback) =>
        sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : fallback;
    internal static MilitaryUnitState Read(Simulation sim, Unit unit) => unit.Extensions.Get<MilitaryUnitState>(sim.Content) ?? new();
    internal static MilitaryUnitState Edit(Simulation sim, Unit unit)
    {
        var state = unit.Extensions.Get<MilitaryUnitState>(sim.Content);
        if (state is not null) return state;
        state = new(); unit.Extensions.Set(sim.Content, state); return state;
    }
    internal static bool Enemy(Simulation sim, int player, int other) => sim.TeamOf(player).Wars.Contains(sim.TeamOf(other).Id);
    private static int Effect(Simulation sim, Unit unit, string effect) => sim.Rules().Units.Effect(sim, unit.Id, effect);

    public IReadOnlyList<ReconSight> ActiveRecon(Simulation sim) =>
        sim.State.Extensions.Get<MilitaryGameState>(sim.Content)?.ReconSights.ToArray() ?? [];

    public IReadOnlyList<ReconSight> ActiveRecon(Simulation sim, int team) =>
        sim.State.Extensions.Get<MilitaryGameState>(sim.Content)?.ReconSights.Where(r => r.Team == team).ToArray() ?? [];

    public int AirRange(Simulation sim, int unitId)
    {
        var unit = sim.State.UnitById(unitId);
        return Math.Max(0, Definition(sim, unit).AirRange + Effect(sim, unit, "air_range"));
    }

    public int InterceptionProbability(Simulation sim, int unitId)
    {
        var unit = sim.State.UnitById(unitId);
        int probability = Math.Clamp(Definition(sim, unit).InterceptionPercent + Effect(sim, unit, "intercept"), 0,
            Raw(sim, "MAX_INTERCEPTION_PROBABILITY", 100));
        return probability * unit.Health / Constants(sim).MaxHitPoints;
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
            AirMissionKind.Rebase => sim.Rules().Units.ValidateRebase(sim, unit.Id, target),
            AirMissionKind.Recon => Definition(sim, unit).Suicide ? "Missiles cannot perform reconnaissance" : null,
            AirMissionKind.AirStrike => ValidateStrike(sim, unit, target),
            AirMissionKind.AirBomb => ValidateBomb(sim, unit, target),
            _ => "Unknown air mission",
        };
    }

    private static string? ValidateAircraft(Simulation sim, Unit unit)
    {
        if (unit.Health <= 0 || unit.MovesLeft <= 0) return "Aircraft cannot act";
        if (Definition(sim, unit).Domain != AirDomain) return "Unit is not an aircraft";
        return Read(sim, unit).Attacked ? "Aircraft has attacked" : null;
    }

    private static string? ValidateStrike(Simulation sim, Unit unit, Hex target)
    {
        if (Definition(sim, unit).AirCombat <= 0) return "Aircraft cannot strike";
        if (!sim.State.PlotAt(target).Visible.Contains(sim.TeamOf(unit.Owner).Id)) return "Target is not visible";
        return AirStrikeTarget(sim, unit, target) is null ? "No air strike target" : null;
    }

    private static string? ValidateBomb(Simulation sim, Unit unit, Hex target)
    {
        if (Definition(sim, unit).BombRate <= 0) return "Aircraft cannot bomb";
        var plot = sim.State.PlotAt(target);
        if (!plot.Visible.Contains(sim.TeamOf(unit.Owner).Id)) return "Target is not visible";
        if (plot.Owner is not { } owner || !Enemy(sim, unit.Owner, owner)) return "Target is not enemy territory";
        if (sim.State.CityAt(target) is { } city)
            return sim.Rules().Combat.CityDefense(sim, city.Id) > 0 ? null : "City has no defenses";
        return plot.Improvement is { } id && !sim.Content.Get<S.ImprovementDef>(id).Goody ? null : "No improvement to bomb";
    }

    private static Unit? AirStrikeTarget(Simulation sim, Unit unit, Hex target)
    {
        var combat = sim.Rules().Combat;
        int limit = Definition(sim, unit).AirCombatLimit;
        int maximum = Constants(sim).MaxHitPoints;
        return sim.State.UnitsAt(target).Where(u => combat.CanDefend(sim, unit.Id, u.Id) && maximum - u.Health < limit)
            .OrderByDescending(u => combat.Preview(sim, unit.Id, u.Id).DefenseStrength.Current).ThenBy(u => u.Id).FirstOrDefault();
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
        sim.Rules().Visibility.Recompute(sim);
        sim.Rules().Relations.DiscoverContacts(sim);
        sim.Events.Publish(new AirMissionResolved(report));
        return report;
    }

    private static AirMissionReport Rebase(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        sim.Rules().Units.Rebase(sim, unit.Id, target);
        SpendMoves(sim, unit, finish: true);
        return report;
    }

    private static AirMissionReport Recon(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        int radius = sim.Content.Get<S.HexOverridesDef>("foundation:hex_overrides").UnitSight + 1;
        var state = sim.State.Extensions.Get<MilitaryGameState>(sim.Content);
        if (state is null) { state = new(); sim.State.Extensions.Set(sim.Content, state); }
        state.AddReconSight(new(sim.TeamOf(unit.Owner).Id, target, radius));
        SpendMoves(sim, unit, finish: true);
        return report;
    }

    private AirMissionReport Attack(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        var intercepted = TryIntercept(sim, unit, target, report);
        var result = intercepted
            ?? (report.Kind == AirMissionKind.AirStrike ? Strike(sim, unit, target, report) : Bomb(sim, unit, target, report));
        SpendMoves(sim, unit, finish: false);
        if (intercepted is null && Definition(sim, unit).Suicide && sim.State.FindUnit(unit.Id) is not null)
            sim.Rules().Units.Destroy(sim, unit.Id);
        return result;
    }

    private static void SpendMoves(Simulation sim, Unit unit, bool finish)
    {
        var state = Edit(sim, unit);
        state.Attacked |= !finish;
        state.Moved = true; state.BreakActivity();
        unit.MovesLeft = finish ? 0 : Math.Max(0, unit.MovesLeft - Constants(sim).MoveDenominator);
    }

    private static AirMissionReport Strike(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        var combat = sim.Rules().Combat;
        var defender = AirStrikeTarget(sim, unit, target)!;
        int maximum = Constants(sim).MaxHitPoints;
        int airStrength = Definition(sim, unit).AirCombat * unit.Health / maximum;
        int defense = combat.Preview(sim, unit.Id, defender.Id).DefenseStrength.Current / 100;
        int factor = (airStrength + defense + 1) / 2;
        int damage = Math.Max(1, Constants(sim).AirCombatDamage * (airStrength + factor) / Math.Max(1, defense + factor));
        if (sim.State.CityAt(target) is { } city)
            damage = damage * Math.Max(0, 100 + combat.AirStrikeModifier(sim, city.Id)) / 100;
        int remaining = Definition(sim, unit).AirCombatLimit - (maximum - defender.Health);
        damage = Math.Max(0, Math.Min(defender.Health, Math.Min(damage, remaining)));
        combat.Collateral(sim, unit.Id, defender.Id);
        defender.Health -= damage;
        if (defender.Health <= 0) sim.Rules().Units.Destroy(sim, defender.Id);
        return report with { TargetUnit = defender.Id, TargetDamage = damage };
    }

    private static AirMissionReport Bomb(Simulation sim, Unit unit, Hex target, AirMissionReport report)
    {
        var def = Definition(sim, unit);
        int rate = Math.Max(1, def.BombRate * unit.Health / Constants(sim).MaxHitPoints);
        if (sim.State.CityAt(target) is { } city)
        {
            var combat = sim.Rules().Combat;
            int resistance = combat.BombardResistance(sim, city.Id);
            int reduction = rate * Math.Max(0, 100 - resistance) / 100;
            return report with { CityDefenseReduction = combat.DamageCityDefense(sim, unit.Id, city.Id, reduction) };
        }
        var plot = sim.State.PlotAt(target);
        string id = plot.Improvement!;
        var improvement = sim.Content.Get<S.ImprovementDef>(id);
        if (sim.Rng.Next(rate) < sim.Rng.Next(Math.Max(1, improvement.AirBombDefense))) return report;
        plot.Improvement = improvement.ImprovementPillage; plot.ImprovementTurns = 0; plot.BuildProgress.Clear();
        return report with { DestroyedImprovement = id };
    }
}
