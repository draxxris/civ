using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.GreatPeople;

public sealed class GreatPeopleMod : IMod
{
    public void Register(IModContext context)
    {
        var service = new GreatPeopleService();
        var effects = new GreatPeopleEffects();
        context.RegisterService<IGreatPeopleService>("queries", service);
        context.RegisterService<ICityGreatPeopleService>("city_queries", service);
        context.RegisterService<IGreatPeopleCommandService>("commands", new GreatPeopleCommands());
        context.RegisterService<IModifierSource>("effects", effects);
        context.RegisterService<IPlotYieldFinalizer>("golden_age_yields", effects);
        context.RegisterStateComponent<GreatPeopleCityState>("city_state");
        context.RegisterStateComponent<GreatPeoplePlayerState>("player_state");
        context.RegisterCommand<LeadGreatPersonCommand>("lead_unit");
        context.RegisterCommand<ConstructGreatPersonCommand>("construct_building");
        context.RegisterCommand<TradeGreatPersonCommand>("trade_mission");
        context.RegisterCommand<JoinGreatPersonCommand>("join_city");
        context.RegisterCommand<DiscoverGreatPersonCommand>("discover_tech");
        context.RegisterCommand<HurryGreatPersonCommand>("hurry_production");
        context.RegisterCommand<CultureGreatPersonCommand>("culture_bomb");
        context.RegisterCommand<GoldenAgeGreatPersonCommand>("golden_age");
        context.Subscribe<BuildingCompleted>("golden_age_wonder", GoldenAges.Completed);
        context.TurnSystems.Register(new GreatPeopleTurns(service));
        context.TurnSystems.Register(new GoldenAgeTimers());
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
