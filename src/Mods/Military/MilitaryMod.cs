using Ariel.Content.Schema;
using Ariel.Modding;
using Ariel.Game;

namespace Ariel.Mods.Military;

/// <summary>Deterministic movement, melee combat, unit lifecycle and barbarian rules.</summary>
public sealed class MilitaryMod : IPreparableMod
{
    private static readonly ModJsonFile<DomainDef[]> Domains = new("data/domain.json");
    private static readonly ModJsonFile<InvisibilityDef[]> Invisibility = new("data/invisibility.json");
    private static readonly ModJsonFile<MissionDef[]> Missions = new("data/mission.json");
    private static readonly ModJsonFile<OutcomeDef[]> Outcomes = new("data/outcome.json");
    private static readonly ModJsonFile<PromotionDef[]> Promotions = new("data/promotion.json");
    private static readonly ModJsonFile<UnitAiDef[]> UnitAi = new("data/unit-ai.json");
    private static readonly ModJsonFile<UnitCategoryDef[]> Categories = new("data/unit-category.json");
    private static readonly ModJsonFile<UnitCombatDef[]> Combat = new("data/unit-combat.json");
    private static readonly ModJsonFile<UnitDef[]> Units = new("data/unit.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles =>
        [Domains, Invisibility, Missions, Outcomes, Promotions, UnitAi, Categories, Combat, Units];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Domains);
        ContentRegistration.Load(context, Invisibility);
        ContentRegistration.Load(context, Missions);
        ContentRegistration.Load(context, Outcomes);
        ContentRegistration.Load(context, Promotions);
        ContentRegistration.Load(context, UnitAi);
        ContentRegistration.Load(context, Categories);
        ContentRegistration.Load(context, Combat);
        ContentRegistration.Load(context, Units);
        var movement = new MovementService();
        var units = new UnitService();
        context.RegisterService<IMovementService>("movement", movement);
        context.RegisterService<IPathfindingService>("paths", new PathfindingService(movement));
        context.RegisterService<ICombatService>("combat", new CombatService());
        context.RegisterService<IAirMissionService>("air_missions", new AirMissionService());
        context.RegisterService<IUnitService>("units", units);
        context.RegisterService<INavalBlockadeService>("blockades", new NavalBlockades());
        context.RegisterService<IUnitSupportService>("support", units);
        context.RegisterService<Ariel.Content.IModifierSource>("promotions", new PromotionEffects());

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
