using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.GreatPeople;

public sealed class GreatPeopleMod : IMod
{
    public void Register(IModContext context)
    {
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
