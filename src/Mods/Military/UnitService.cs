using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Military;

internal sealed partial class UnitService : IUnitService, IUnitSupportService
{
    public int MaximumMoves(Simulation sim, int unit)
    {
        var u = sim.State.UnitById(unit);
        return MovementService.BaseMoves(sim, u, MilitaryData.Definition(sim, u), MilitaryData.Effects(sim, u)) * MilitaryData.Constants(sim).MoveDenominator;
    }
    public int ExperienceNeeded(Simulation sim, int unit)
    {
        var u = sim.State.UnitById(unit);
        int level = MilitaryData.Read(sim, u).Level;
        int percent = MilitaryData.Effect(MilitaryData.Effects(sim, u), "level_experience_percent");
        int threshold = level * level + 1;
        return Math.Max(0, threshold + (threshold * percent + 99) / 100);
    }
    public int HealRate(Simulation sim, int unit) => UnitHealing.Rate(sim, sim.State.UnitById(unit));
    public long UpgradePrice(Simulation sim, int unit, string target) => UnitUpgrades.Price(sim, sim.State.UnitById(unit), sim.Content.Get<S.UnitDef>(target));
    public string? Validate(Simulation sim, int player, int unitId, UnitAction action, string argument)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Not your unit";
        return action switch {
            UnitAction.Promote => ValidatePromotion(sim, unit, argument),
            UnitAction.Activity => ValidateActivity(sim, unit, argument),
            UnitAction.Pillage => ValidatePillage(sim, unit),
            UnitAction.Upgrade => UnitUpgrades.Validate(sim, unit, argument),
            UnitAction.Disband => null,
            _ => "Unknown unit action",
        };
    }
    private string? ValidatePromotion(Simulation sim, Unit unit, string id)
    {
        if (!sim.Content.TryGet<S.PromotionDef>(id, out var promotion)) return "Unknown promotion";
        if (unit.Promotions.Contains(id)) return "Promotion already held";
        if (unit.MovesLeft <= 0) return "Unit has no movement left";
        if (unit.Experience < ExperienceNeeded(sim, unit.Id)) return "Not enough experience";
        return PromotionEligibility(sim, unit, promotion);
    }
    internal static string? PromotionEligibility(Simulation sim, Unit unit, S.PromotionDef promotion)
    {
        string? type = MilitaryData.Definition(sim, unit).CombatType;
        if (type is null || !promotion.CombatTypes.GetValueOrDefault(type)) return "Promotion not allowed for this combat type";
        if (promotion.Leader && MilitaryData.Read(sim, unit).AttachedLeaderUnitType is null) return "Requires an attached leader";
        if (promotion.ObsoleteTech is { } obsolete && sim.KnowsTech(unit.Owner, obsolete)) return "Promotion is obsolete";
        var result = sim.Content.Requirements.Evaluate(promotion.Requirements, new(sim, unit.Owner, Unit: unit, Plot: sim.State.Grid.TileAt(unit.Position)));
        return result.Success ? null : string.Join("; ", result.Failures.Select(f => f.Explanation));
    }
    private static string? ValidateActivity(Simulation sim, Unit unit, string argument)
    {
        if (!Enum.TryParse<UnitActivity>(argument, out var activity) || !Enum.IsDefined(activity)) return "Unknown activity";
        if (activity == UnitActivity.Fortify && !CombatCalculator.Fortifiable(MilitaryData.Definition(sim, unit))) return "Unit cannot fortify";
        if (activity == UnitActivity.Intercept)
        {
            if (sim.Content.Services.Get<IAirMissionService>().Single().InterceptionProbability(sim, unit.Id) <= 0) return "Unit cannot intercept";
            if (MilitaryData.Definition(sim, unit).Domain == "military:domain_air" && MilitaryData.Read(sim, unit).Moved) return "Aircraft has moved";
        }
        return null;
    }
    private static string? ValidatePillage(Simulation sim, Unit unit)
    {
        var def = MilitaryData.Definition(sim, unit); var plot = sim.State.PlotAt(unit.Position);
        if (!def.Pillage) return "Unit cannot pillage";
        if (unit.MovesLeft <= 0) return "No moves left";
        if (MilitaryData.Read(sim, unit).TransportId is not null) return "Loaded cargo cannot pillage";
        if (sim.State.CityAt(unit.Position) is not null) return "Cannot pillage a city";
        return ValidatePillagePlot(sim, unit, def, plot);
    }
    private static string? ValidatePillagePlot(Simulation sim, Unit unit, S.UnitDef unitDef, Plot plot)
    {
        if (!ValidPillageDomain(sim, unitDef, plot)) return "Invalid domain for pillaging";
        if (plot.Improvement is { } id)
        {
            var improvement = sim.Content.Get<S.ImprovementDef>(id);
            if (improvement.Goody) return "Cannot pillage a goody hut";
            if (improvement.Permanent) return "Cannot pillage a permanent improvement";
        }
        else if (plot.Route is null) return "Nothing to pillage";

        if (plot.Owner is { } owner && !MilitaryData.Enemy(sim, unit.Owner, owner)
            && (owner != unit.Owner || plot.Improvement is null)) return "Cannot pillage at peace";
        return null;
    }
    private static bool ValidPillageDomain(Simulation sim, S.UnitDef unit, Plot plot)
    {
        if (unit.Domain == "military:domain_air") return false;
        if (unit.CanMoveAllTerrain) return true;
        bool water = sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater;
        return unit.Domain switch
        {
            "military:domain_land" => !water,
            "military:domain_sea" => water,
            "military:domain_immobile" => !water,
            _ => false,
        };
    }
    public void Execute(Simulation sim, int unitId, UnitAction action, string argument)
    {
        var unit = sim.State.UnitById(unitId);
        switch (action)
        {
            case UnitAction.Promote: Promote(sim, unit, argument); break;
            case UnitAction.Activity: SetActivity(sim, unit, Enum.Parse<UnitActivity>(argument)); break;
            case UnitAction.Pillage: Pillage(sim, unit); break;
            case UnitAction.Upgrade: UnitUpgrades.Apply(sim, unit, argument); break;
            case UnitAction.Disband: MilitaryData.RemoveUnit(sim, unitId); MilitaryData.Sight(sim); break;
        }
    }
    private static void SetActivity(Simulation sim, Unit unit, UnitActivity activity)
    {
        var state = MilitaryData.Edit(sim, unit);
        if (activity != UnitActivity.Fortify) state.FortifyTurns = 0;
        state.Activity = activity;
    }
    private static void Promote(Simulation sim, Unit unit, string id)
    {
        unit.Promotions.Add(id); MilitaryData.Edit(sim, unit).Level++;
        int maximum = MilitaryData.Constants(sim).MaxHitPoints;
        unit.Health += (maximum - unit.Health) / 2;
        MilitaryData.Sight(sim);
    }
    private static void Pillage(Simulation sim, Unit unit)
    {
        var plot = sim.State.PlotAt(unit.Position);
        var tile = sim.State.Grid.TileAt(unit.Position);
        string? improvementId = plot.Improvement;
        string? routeId = plot.Route;
        string? replacement = null;
        int gold = 0;
        bool removedCityImprovement = false;
        if (improvementId is { } id)
        {
            var improvement = sim.Content.Get<S.ImprovementDef>(id);
            removedCityImprovement = improvement.ActsAsCity;
            if (plot.Owner is not { } owner || sim.TeamOf(owner).Id != sim.TeamOf(unit.Owner).Id)
            {
                gold = PillageGold(sim, unit, improvement);
                sim.State.PlayerById(unit.Owner).Treasury += gold;
            }
            plot.Improvement = replacement = improvement.ImprovementPillage;
            plot.ImprovementTurns = 0;
        }
        else plot.Route = null;
        plot.BuildProgress.Clear();
        unit.MovesLeft = Math.Max(0, unit.MovesLeft - MilitaryData.Constants(sim).MoveDenominator);
        var state = MilitaryData.Edit(sim, unit); state.Moved = true; state.FortifyTurns = 0;
        sim.Events.Publish(new PlotPillaged(unit.Id, unit.Owner, tile, improvementId, replacement,
            improvementId is null ? routeId : null, gold));
        if (removedCityImprovement) MilitaryData.Sight(sim);
    }
    private static int PillageGold(Simulation sim, Unit unit, S.ImprovementDef improvement)
    {
        if (improvement.PillageGold <= 0) return 0;
        int gold = sim.Rng.Next(improvement.PillageGold) + sim.Rng.Next(improvement.PillageGold);
        return gold + gold * MilitaryData.Effect(MilitaryData.Effects(sim, unit), "pillage_percent") / 100;
    }
    public void Initialize(Simulation sim, int unitId)
    {
        var unit = sim.State.UnitById(unitId);
        if (unit.Extensions.Get<MilitaryUnitState>(sim.Content) is not null) return;
        var state = MilitaryData.Edit(sim, unit); state.CreatedTurn = sim.State.Turn;
        foreach (var promotion in MilitaryData.Definition(sim, unit).FreePromotions.Where(p => p.Value).OrderBy(p => p.Key, StringComparer.Ordinal)) unit.Promotions.Add(promotion.Key);
        var effects = MilitaryData.Effects(sim, unit);
        foreach (var promotion in sim.Content.Get<S.PromotionDef>().All)
            if (MilitaryData.Effect(effects, "free_promotion:" + promotion.Id) > 0) unit.Promotions.Add(promotion.Id);
        unit.Experience += Math.Max(0, MilitaryData.Effect(effects, "initial_experience"));
        unit.MovesLeft = MaximumMoves(sim, unitId);
    }
    public UnitSupportCount Count(Simulation sim, int player)
    {
        var units = sim.State.Units.Where(u => u.Owner == player).ToArray();
        int military = units.Count(u => MilitaryData.Definition(sim, u).MilitarySupport);
        int outside = units.Count(u => !MilitaryData.Friendly(sim, player, sim.State.PlotAt(u.Position).Owner));
        return new(units.Length, military, outside);
    }
}

internal sealed class UnitTurnSystem(IUnitService units) : IPlayerTurnSystem
{
    public string Id => "military:units";
    public int Order => TurnPhase.Units + 10; // Workers spend their moves before this refresh.
    public void Run(Simulation sim, int player)
    {
        foreach (var unit in sim.State.Units.Where(u => u.Owner == player).OrderBy(u => u.Id).ToArray()) Refresh(sim, unit);
    }
    public void Run(Simulation sim)
    {
        foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id);
    }
    private void Refresh(Simulation sim, Unit unit)
    {
        units.Initialize(sim, unit.Id);
        var state = MilitaryData.Edit(sim, unit);
        bool active = state.Moved || state.Attacked || unit.MovesLeft < units.MaximumMoves(sim, unit.Id);
        bool alwaysHeal = MilitaryData.Effect(MilitaryData.Effects(sim, unit), "always_heal") > 0;
        if (state.CreatedTurn != sim.State.Turn)
        {
            if (!active || alwaysHeal) unit.Health = Math.Min(MilitaryData.Constants(sim).MaxHitPoints, unit.Health + units.HealRate(sim, unit.Id));
            if (!active && state.Activity == UnitActivity.Fortify && CombatCalculator.Fortifiable(MilitaryData.Definition(sim, unit)))
                state.FortifyTurns = Math.Min(MilitaryData.Raw(sim, "MAX_FORTIFY_TURNS", 5), state.FortifyTurns + 1);
        }
        WakeSentry(sim, unit, state);
        unit.MovesLeft = units.MaximumMoves(sim, unit.Id); state.Moved = false; state.Attacked = false;
        state.InterceptedThisTurn = false;
    }
    private static void WakeSentry(Simulation sim, Unit unit, MilitaryUnitState state)
    {
        if (state.Activity != UnitActivity.Sentry) return;
        int team = sim.TeamOf(unit.Owner).Id;
        if (sim.State.Units.Any(u => MilitaryData.Enemy(sim, unit.Owner, u.Owner)
            && sim.Content.Services.Get<IVisibilityService>().Single().IsUnitVisible(sim, u.Id, team))) state.Activity = UnitActivity.Awake;
    }
}
