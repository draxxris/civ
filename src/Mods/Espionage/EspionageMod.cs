using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Espionage;

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
        context.TurnSystems.Register(new SpyExpiry());
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
/// <summary>Drops expired counter-espionage records at the first turn boundary after their expiry so dead entries cannot accumulate in authoritative state.</summary>
internal sealed class SpyExpiry : ITurnSystem
{
    public string Id => "espionage:expiry";
    public int Order => TurnPhase.GameBegin + 1;
    public void Run(Simulation sim)
    {
        foreach (var team in sim.State.Teams.OrderBy(t => t.Id))
        {
            var state = team.Extensions.Get<EspionageTeamState>(sim.Content);
            if (state is null) continue;
            foreach (int rival in state.CounterEspionage.Where(p => p.Value.ExpiresTurn <= sim.State.Turn).Select(p => p.Key).ToArray())
                state.CounterEspionage.Remove(rival);
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
