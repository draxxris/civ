using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods;

namespace Ariel.Mods.Events;

/// <summary>Deterministic random events with player choices.</summary>
public sealed class EventsMod : IPreparableMod
{
    private static readonly ModJsonFile<EventTriggerDef[]> Triggers = new("data/event-trigger.json");
    private static readonly ModJsonFile<EventOptionDef[]> Options = new("data/event-option.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Triggers, Options];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Triggers);
        ContentRegistration.Load(context, Options);
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
