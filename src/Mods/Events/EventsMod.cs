using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Civ.Modding;
using Civ.Mods;

namespace Civ.Mods.Events;

/// <summary>Deterministic random events with player choices.</summary>
public sealed class EventsMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<EventTriggerDef>(context, "event-trigger.json");
        ContentRegistration.Load<EventOptionDef>(context, "event-option.json");
        var service = new EventService();
        context.RegisterService<IEventService>("queries", service);
        context.RegisterService<IModifierSource>("effects", new EventModifiers());
        context.RegisterStateComponent<EventState>("state");
        context.RegisterCommand<ChooseEventOptionCommand>("choose_option");
        context.TurnSystems.Register(new EventTurn(service));
    }
}

internal sealed class EventTurn(EventService service) : ITurnSystem
{
    public string Id => "events:turn";
    public int Order => TurnPhase.Spawns;
    public void Run(Simulation sim) => service.RunTurn(sim);
}
