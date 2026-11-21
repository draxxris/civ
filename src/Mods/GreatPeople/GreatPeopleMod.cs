using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.GreatPeople;

public sealed class GreatPeopleMod : IPreparableMod
{
    private static readonly ModJsonFile<BuildingGreatPeopleDef[]> Buildings = new("data/building-great-people.json");
    private static readonly ModJsonFile<UnitGreatPeopleDef[]> Units = new("data/unit-great-people.json");
    private static readonly ModJsonFile<SpecialistGreatPeopleDef[]> Specialists = new("data/specialist-great-people.json");
    private static readonly ModJsonFile<YieldGreatPeopleDef[]> Yields = new("data/yield-great-people.json");
    private static readonly ModJsonFile<GameSpeedGreatPeopleDef[]> Speeds = new("data/game-speed-great-people.json");
    private static readonly ModJsonFile<EraGreatPeopleDef[]> Eras = new("data/era-great-people.json");
    private static readonly ModJsonFile<RuleConstantsGreatPeopleDef[]> Constants = new("data/rule-constants-great-people.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Buildings, Units, Specialists, Yields, Speeds, Eras, Constants];

    public void Register(IModContext context)
    {
        // What base definitions mean to great people. TechGreatPeopleDef has no shipped rows: no source field sets
        // a technology's trade mission modifier, but another mod may attach one.
        Ariel.Rules.ContentRegistration.Load(context, Buildings);
        Ariel.Rules.ContentRegistration.Load(context, Units);
        Ariel.Rules.ContentRegistration.Load(context, Specialists);
        Ariel.Rules.ContentRegistration.Load(context, Yields);
        Ariel.Rules.ContentRegistration.Load(context, Speeds);
        Ariel.Rules.ContentRegistration.Load(context, Eras);
        Ariel.Rules.ContentRegistration.Load(context, Constants);
        var service = new GreatPeopleService();
        var effects = new GreatPeopleEffects();
        context.RegisterService<IGreatPeopleService>("queries", service);
        context.RegisterService<ICityGreatPeopleService>("city_queries", service);
        context.RegisterService<Ariel.Presentation.ICityPanel>("city", new GreatPeopleCityStats());
        context.RegisterService<Ariel.Presentation.ICityPanel>("settled", new SettledGreatPeoplePanel());
        context.RegisterService<Ariel.Presentation.IPlayerSummaryItem>("golden_age", new GoldenAgeSummary());
        foreach (var mission in GreatPersonMission.All) context.RegisterService<IMission>(mission.Id["great_people:".Length..], mission);
        context.RegisterService<IModifierSource>("effects", effects);
        context.RegisterService<IPlotYieldFinalizer>("golden_age_yields", effects);
        context.RegisterService<Ariel.Ai.IAiPlanner>("planner", new GreatPersonAiPlanner());
        context.RegisterStateComponent<GreatPeopleCityState>("city_state");
        context.RegisterStateComponent<GreatPeoplePlayerState>("player_state");
        context.Subscribe<BuildingCompleted>("golden_age_wonder", GoldenAges.Completed);
        context.TurnSystems.Register(new GreatPeopleTurns(service));
        context.TurnSystems.Register(new GoldenAgeTimers());
        context.RegisterService<ICombatLeaders>("great_generals", new GreatGenerals(service));
        context.RegisterService<ICapitalWeight>("capital_weight", new SettledCapitalWeight(service));
    }
}
internal sealed class GreatPeopleTurns(GreatPeopleService service) : IPlayerTurnSystem
{
    public string Id => "great_people:births";
    public int Order => TurnPhase.CityPostProduction;
    public void Run(Simulation sim, int player)
    {
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id)) service.Run(sim, city);
    }
    public void Run(Simulation sim) { foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id); }
}
internal sealed class GoldenAgeTimers : IPlayerTurnSystem
{
    public string Id => "great_people:golden_age_timers";
    public int Order => TurnPhase.PlayerEnd + 2;
    public void Run(Simulation sim, int player) => GoldenAges.Tick(sim, player);
    public void Run(Simulation sim) { foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Run(sim, player.Id); }
}
