using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.Espionage;

public sealed class EspionageMod : IMod
{
    public void Register(IModContext context)
    {
        var service = new EspionageService();
        context.RegisterService<IEspionageService>("espionage", service);
        context.RegisterService<IModifierSource>("unhappiness", new SpyUnhappiness());
        context.RegisterStateComponent<EspionageTeamState>("team_state");
        context.RegisterStateComponent<EspionagePlayerState>("player_state");
        context.RegisterStateComponent<EspionageCityState>("city_state");
        context.RegisterCommand<SetEspionageWeightCommand>("set_weight");
        context.RegisterCommand<ExecuteEspionageMissionCommand>("execute_mission");
        context.TurnSystems.Register(new SpyAllocation(service));
        context.TurnSystems.Register(new SpyTimers());
    }
}
internal sealed class SpyAllocation(EspionageService service) : IPlayerTurnSystem
{
    public string Id => "espionage:allocation";
    public int Order => TurnPhase.Espionage + 1;
    public void Run(Simulation sim, int player) => service.Allocate(sim, player);
}
internal sealed class SpyTimers : IPlayerTurnSystem
{
    public string Id => "espionage:timers";
    public int Order => TurnPhase.PlayerEnd + 1;
    public void Run(Simulation sim, int player)
    {
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id))
        {
            var state = city.Extensions.Get<EspionageCityState>(sim.Content);
            if (state is null || !state.OwnsDisorder || sim.State.Turn + 1 < state.RevoltUntilTurn) continue;
            var rules = city.Extensions.Get<CityRuleState>(sim.Content);
            if (rules is not null) rules.Disorder = false;
            state.OwnsDisorder = false;
        }
    }
}
internal sealed class SpyUnhappiness : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        if (city.Extensions.Get<EspionageCityState>(context.Sim.Content)?.UnhappyUntilTurn > context.Sim.State.Turn)
            sink.Add("espionage:foment_unhappiness", "cities:happiness", -3);
    }
}
