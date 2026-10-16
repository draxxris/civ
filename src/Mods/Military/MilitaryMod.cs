using Civ.Content.Schema;
using Civ.Modding;
using Civ.Game;

namespace Civ.Mods.Military;

/// <summary>Deterministic movement, melee combat, unit lifecycle and barbarian rules.</summary>
public sealed class MilitaryMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<DomainDef>(context, "domain.json");
        ContentRegistration.Load<InvisibilityDef>(context, "invisibility.json");
        ContentRegistration.Load<MissionDef>(context, "mission.json");
        ContentRegistration.Load<OutcomeDef>(context, "outcome.json");
        ContentRegistration.Load<PromotionDef>(context, "promotion.json");
        ContentRegistration.Load<UnitAiDef>(context, "unit-ai.json");
        ContentRegistration.Load<UnitCategoryDef>(context, "unit-category.json");
        ContentRegistration.Load<UnitCombatDef>(context, "unit-combat.json");
        ContentRegistration.Load<UnitDef>(context, "unit.json");
        var movement = new MovementService();
        var units = new UnitService();
        context.RegisterService<IMovementService>("movement", movement);
        context.RegisterService<IPathfindingService>("paths", new PathfindingService(movement));
        context.RegisterService<ICombatService>("combat", new CombatService());
        context.RegisterService<IAirMissionService>("air_missions", new AirMissionService());
        context.RegisterService<IUnitService>("units", units);
        context.RegisterService<INavalBlockadeService>("blockades", new NavalBlockades());
        context.RegisterService<IUnitSupportService>("support", units);
        context.RegisterService<Civ.Content.IModifierSource>("promotions", new PromotionEffects());

        context.RegisterStateComponent<MilitaryUnitState>("unit_state");
        context.RegisterStateComponent<MilitaryPlayerState>("player_state");
        context.RegisterStateComponent<MilitaryGameState>("combat_history");
        context.RegisterCommand<MoveUnitPathCommand>("move_unit");
        context.RegisterCommand<LoadUnitCommand>("load_unit");
        context.RegisterCommand<UnloadUnitCommand>("unload_unit");
        context.RegisterCommand<BombardCityCommand>("bombard_city");
        context.RegisterCommand<AirMissionCommand>("air_mission");
        context.RegisterCommand<RangedStrikeCommand>("ranged_strike");
        context.RegisterCommand<PromoteMilitaryUnitCommand>("promote_unit");
        context.RegisterCommand<SetUnitActivityCommand>("set_unit_activity");
        context.RegisterCommand<PillageCommand>("pillage");
        context.RegisterCommand<UpgradeUnitCommand>("upgrade_unit");
        context.RegisterCommand<DisbandUnitCommand>("disband_unit");
        context.RegisterCommand<RazeCityCommand>("raze_city");
        context.TurnSystems.Register(new UnitTurnSystem(units));
        context.TurnSystems.Register(new BarbarianSpawns());
        context.TurnSystems.Register(new BarbarianBehavior());
        context.Subscribe<UnitCreated>("initialize", (sim, created) => units.Initialize(sim, created.Unit));
        context.Subscribe<TurnStarted>("begin", (sim, _) =>
        {
            foreach (var unit in sim.State.Units.OrderBy(u => u.Id)) units.Initialize(sim, unit.Id);
            foreach (var player in sim.State.Players.Where(p => MilitaryData.Player(sim, p.Id).Barbarian)) player.EndedTurn = true;
            CombatService.HistoryState(sim).Combats.Clear();
            CombatService.HistoryState(sim).ReconSights.Clear();
            MilitaryData.Sight(sim);
        });
        ContentRegistration.Evaluator<UnitDef>(context, "unit", (leaf, subject) => subject.Unit?.Type == leaf.Id);
        ContentRegistration.Evaluator<UnitDef>(context, "unit-class");
        ContentRegistration.Evaluator<UnitCombatDef>(context, "unit-combat");
        ContentRegistration.Evaluator<PromotionDef>(context, "promotion", (leaf, subject) => subject.Unit?.Promotions.Contains(leaf.Id) == true);
    }
}
