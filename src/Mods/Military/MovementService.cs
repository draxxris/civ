using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Military;

internal sealed class MovementService : IMovementService
{
    public string? CanEnter(Simulation sim, int unitId, TileId from, TileId to, bool attack = false)
    {
        if (!ValidTile(sim, from) || !ValidTile(sim, to)) return "Off map";
        if (!sim.State.Grid.Neighbors(from).Contains(to)) return "Not adjacent";
        var unit = sim.State.UnitById(unitId);
        var def = MilitaryData.Definition(sim, unit);
        var plot = sim.State.Plots[to.Value];
        var effects = MilitaryData.Effects(sim, unit);
        var error = TerrainEntry(sim, unit, def, plot, effects);
        if (error is not null) return error;
        if (!TerritoryEntry(sim, unit, def, plot.Owner)) return "Closed borders";
        var target = sim.State.Grid.HexOf(to);
        var targetError = TargetEntry(sim, unit, def, effects, target, attack);
        if (targetError is not null) return targetError;
        return ControlledTransition(sim, unit, from, to, attack) ? "Enemy zone of control" : null;
    }
    private static string? TargetEntry(Simulation sim, Unit unit, S.UnitDef def, Civ.Content.ModifierSink effects, Hex target, bool attack)
    {
        if (!HostilePlot(sim, unit, target)) return null;
        if (!attack) return "Enemy occupies plot";
        if (!CanAttack(sim, unit, def, effects)) return "Unit cannot attack";
        if (def.NoCapture && sim.State.CityAt(target) is not null) return "Unit cannot capture cities";
        var defenders = sim.State.UnitsAt(target).Where(u => MilitaryData.Enemy(sim, unit.Owner, u.Owner) && MilitaryData.Definition(sim, u).Strength > 0);
        if (defenders.Any() && defenders.All(u => MilitaryData.Constants(sim).MaxHitPoints - u.Health >= def.CombatLimit)) return "Defenders are at combat limit";
        return null;
    }
    private static bool ValidTile(Simulation sim, TileId tile) => tile.Value >= 0 && tile.Value < sim.State.Grid.Count;
    private static string? TerrainEntry(Simulation sim, Unit unit, S.UnitDef def, Plot plot, Civ.Content.ModifierSink effects)
    {
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        if (!DomainEntry(sim, unit, def, terrain.IsWater, plot)) return "Wrong domain";
        if (def.CanMoveImpassable) return null;
        if (terrain.Impassable) return "Impassable terrain";
        if (plot.Feature is { } feature && sim.Content.Get<S.FeatureDef>(feature).Impassable) return "Impassable feature";
        if (plot.Kind == PlotKind.Peak && !PeakEntry(sim, unit, effects)) return "Cannot enter peaks";
        return UnitTerrainEntry(sim, unit, def, plot);
    }
    private static string? UnitTerrainEntry(Simulation sim, Unit unit, S.UnitDef def, Plot plot)
    {
        if (def.TerrainImpassables.GetValueOrDefault(plot.Terrain) && !TerrainTechnology(sim, unit, def, plot.Terrain)) return "Terrain requires technology";
        if (plot.Feature is { } f && def.FeatureImpassables.GetValueOrDefault(f)) return "Unit cannot enter feature";
        return null;
    }
    private static bool TerrainTechnology(Simulation sim, Unit unit, S.UnitDef def, string terrain) =>
        def.TerrainPassableTechs.TryGetValue(terrain, out var tech) && tech is not null && sim.KnowsTech(unit.Owner, tech);
    private static bool PeakEntry(Simulation sim, Unit unit, Civ.Content.ModifierSink effects) =>
        MilitaryData.Effect(effects, "move_peaks") > 0 || MilitaryData.Techs(sim, unit.Owner).Any(t => t.CanPassPeaks);
    private static bool DomainEntry(Simulation sim, Unit unit, S.UnitDef def, bool water, Plot plot)
    {
        if (def.CanMoveAllTerrain) return true;
        return def.Domain switch {
            "military:domain_land" => !water,
            "military:domain_sea" => water || FriendlyCoastalCity(sim, unit, plot),
            "military:domain_air" => false, // Rebase/air missions need a separate, non-adjacent command.
            _ => false,
        };
    }
    private static bool FriendlyCoastalCity(Simulation sim, Unit unit, Plot plot)
    {
        int index = Array.IndexOf(sim.State.Plots, plot);
        var tile = new TileId(index);
        var city = sim.State.CityAt(sim.State.Grid.HexOf(tile));
        return city is not null && MilitaryData.Friendly(sim, unit.Owner, city.Owner)
            && sim.State.Grid.Neighbors(tile).Any(n => sim.Content.Get<S.TerrainDef>(sim.State.Plots[n.Value].Terrain).IsWater);
    }
    private static bool TerritoryEntry(Simulation sim, Unit unit, S.UnitDef def, int? owner) => owner is null
        || MilitaryData.Friendly(sim, unit.Owner, owner) || MilitaryData.Enemy(sim, unit.Owner, owner.Value) || def.RivalTerritory;
    internal static bool HostilePlot(Simulation sim, Unit unit, Hex target) =>
        sim.State.UnitsAt(target).Any(other => MilitaryData.Enemy(sim, unit.Owner, other.Owner))
        || sim.State.CityAt(target) is { } city && MilitaryData.Enemy(sim, unit.Owner, city.Owner);
    private static bool CanAttack(Simulation sim, Unit unit, S.UnitDef def, Civ.Content.ModifierSink effects) =>
        def.Strength > 0 && !def.OnlyDefensive && (!MilitaryData.Read(sim, unit).Attacked || MilitaryData.Effect(effects, "blitz") > 0);
    private static bool ControlledTransition(Simulation sim, Unit unit, TileId from, TileId to, bool attack)
    {
        var def = MilitaryData.Definition(sim, unit);
        if (def.HiddenNationality || def.AlwaysHostile) return false;
        var city = sim.State.CityAt(sim.State.Grid.HexOf(to));
        if (city is not null && MilitaryData.Friendly(sim, unit.Owner, city.Owner)) return false;
        if (attack && sim.State.UnitsAt(sim.State.Grid.HexOf(to)).Any(u => HasControl(sim, unit, u))) return false;
        return InControl(sim, unit, from) && InControl(sim, unit, to);
    }
    private static bool InControl(Simulation sim, Unit unit, TileId tile) => sim.State.Grid.Neighbors(tile)
        .SelectMany(n => sim.State.UnitsAt(sim.State.Grid.HexOf(n))).Any(u => HasControl(sim, unit, u));
    private static bool HasControl(Simulation sim, Unit unit, Unit other) => MilitaryData.Enemy(sim, unit.Owner, other.Owner)
        && MilitaryData.Effect(MilitaryData.Effects(sim, other), "zone_control") > 0;
    public int StepCost(Simulation sim, int unitId, TileId from, TileId to)
    {
        var unit = sim.State.UnitById(unitId);
        var def = MilitaryData.Definition(sim, unit);
        int denominator = MilitaryData.Constants(sim).MoveDenominator;
        if (def.FlatMovementCost || def.Domain == "military:domain_air") return denominator;
        var effects = MilitaryData.Effects(sim, unit);
        int regular = RegularCost(sim, unit, def, sim.State.Plots[to.Value], effects);
        return Math.Max(1, Math.Min(regular, RouteCost(sim, unit, def, from, to, effects)));
    }
    private static int RegularCost(Simulation sim, Unit unit, S.UnitDef def, Plot plot, Civ.Content.ModifierSink effects)
    {
        int denominator = MilitaryData.Constants(sim).MoveDenominator;
        if (def.IgnoreTerrainCost) return denominator;
        int cost = plot.Feature is { } f ? sim.Content.Get<S.FeatureDef>(f).MovementCost : sim.Content.Get<S.TerrainDef>(plot.Terrain).MovementCost;
        if (plot.Kind == PlotKind.Hills) cost += MilitaryData.Raw(sim, "HILLS_EXTRA_MOVEMENT", 1);
        if (plot.Kind == PlotKind.Peak) cost += MilitaryData.Raw(sim, "PEAK_EXTRA_MOVEMENT", 1);
        cost = Math.Min(BaseMoves(sim, unit, def, effects), Math.Max(1, cost - MilitaryData.Effect(effects, "move_discount"))) * denominator;
        if (DoubleMove(sim, unit, plot)) cost /= 2;
        return cost;
    }
    internal static int BaseMoves(Simulation sim, Unit unit, S.UnitDef def, Civ.Content.ModifierSink effects) => Math.Max(1,
        def.Moves + MilitaryData.Effect(effects, "moves") + MilitaryData.Techs(sim, unit.Owner).Sum(t => t.DomainExtraMoves.GetValueOrDefault(def.Domain ?? "")));
    private static bool DoubleMove(Simulation sim, Unit unit, Plot plot) => MilitaryData.Promotions(sim, unit).Any(p =>
        plot.Kind == PlotKind.Hills && p.HillsDoubleMove || (plot.Feature is { } f ? p.FeatureDoubleMoves.GetValueOrDefault(f) : p.TerrainDoubleMoves.GetValueOrDefault(plot.Terrain)));
    private static int RouteCost(Simulation sim, Unit unit, S.UnitDef def, TileId from, TileId to, Civ.Content.ModifierSink effects)
    {
        var a = sim.State.Plots[from.Value]; var b = sim.State.Plots[to.Value];
        if (!ValidRoute(sim, unit, a, effects) || !ValidRoute(sim, unit, b, effects)) return int.MaxValue;
        if (MilitaryData.CrossesRiver(sim, from, to) && !MilitaryData.Techs(sim, unit.Owner).Any(t => t.BridgeBuilding)) return int.MaxValue;
        var x = sim.Content.Get<S.RouteDef>(a.Route!); var y = sim.Content.Get<S.RouteDef>(b.Route!);
        int cost = Math.Max(x.MovementCost, y.MovementCost);
        int flat = Math.Max(x.FlatMovement, y.FlatMovement) * BaseMoves(sim, unit, def, effects);
        return Math.Min(cost, flat);
    }
    private static bool ValidRoute(Simulation sim, Unit unit, Plot plot, Civ.Content.ModifierSink effects) => plot.Route is not null
        && (plot.Owner is null || !MilitaryData.Enemy(sim, unit.Owner, plot.Owner.Value) || MilitaryData.Effect(effects, "enemy_route") > 0);
    public string? ValidatePath(Simulation sim, int player, int unitId, IReadOnlyList<Hex> path)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Not your unit";
        if (unit.MovesLeft <= 0) return "No moves left";
        if (path.Count == 0) return "Empty path";
        var from = sim.State.Grid.TileAt(unit.Position);
        for (int i = 0; i < path.Count; i++)
        {
            if (!sim.State.Grid.TryTile(path[i], out var to)) return "Off map";
            var error = CanEnter(sim, unitId, from, to, i == path.Count - 1);
            if (error is not null) return error;
            from = to;
        }
        return null;
    }
    public void Move(Simulation sim, int unitId, IReadOnlyList<Hex> path)
    {
        var unit = sim.State.UnitById(unitId);
        foreach (var step in path)
        {
            if (unit.MovesLeft <= 0) break;
            if (MoveStep(sim, unit, sim.State.Grid.TileAt(step))) break;
        }
        MilitaryData.Sight(sim);
    }
    private bool MoveStep(Simulation sim, Unit unit, TileId to)
    {
        var from = sim.State.Grid.TileAt(unit.Position);
        var target = sim.State.Grid.HexOf(to);
        int cost = StepCost(sim, unit.Id, from, to);
        var state = MilitaryData.Edit(sim, unit);
        state.Moved = true; state.FortifyTurns = 0; state.Activity = UnitActivity.Awake;
        bool hostile = HostilePlot(sim, unit, target);
        var combatService = sim.Content.Services.Get<ICombatService>().Single();
        if (hostile && HostileDefenders(sim, unit, target) && combatService.BestDefender(sim, unit.Id, target) is null) return true;
        if (hostile && sim.Content.Services.Get<ICombatService>().Single().BestDefender(sim, unit.Id, target) is { } defender)
        {
            sim.Content.Services.Get<ICombatService>().Single().Resolve(sim, unit.Id, defender);
            if (sim.State.FindUnit(unit.Id) is null) return true;
            if (HostileDefenders(sim, unit, target)) return true;
        }
        else unit.MovesLeft = Math.Max(0, unit.MovesLeft - cost);
        CaptureCivilians(sim, unit, target);
        var city = sim.State.CityAt(target);
        if (city is not null && MilitaryData.Enemy(sim, unit.Owner, city.Owner) && !MilitaryData.Definition(sim, unit).NoCapture)
            sim.Content.Services.Get<ICityCaptureService>().Single().Capture(sim, city.Id, unit.Owner);
        sim.State.MoveUnit(unit.Id, target);
        GoodyService.Receive(sim, unit);
        MilitaryData.Sight(sim);
        return hostile;
    }
    private static bool HostileDefenders(Simulation sim, Unit unit, Hex target) => sim.State.UnitsAt(target)
        .Any(u => MilitaryData.Enemy(sim, unit.Owner, u.Owner) && MilitaryData.Definition(sim, u).Strength > 0);
    private static void CaptureCivilians(Simulation sim, Unit unit, Hex target)
    {
        foreach (var other in sim.State.UnitsAt(target).Where(u => MilitaryData.Enemy(sim, unit.Owner, u.Owner)).ToArray())
        {
            var def = MilitaryData.Definition(sim, other);
            sim.State.RemoveUnit(other.Id);
            if (def.Capture is { } type && !MilitaryData.Definition(sim, unit).NoCapture)
            {
                var captured = MilitaryData.Spawn(sim, unit.Owner, type, target);
                captured.MovesLeft = 0;
            }
        }
    }
}
