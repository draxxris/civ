namespace Civ.Game;

/// <summary>An event waiting for a player choice. City and Plot are -1 when the event has no such subject.</summary>
public sealed record PendingEvent(int Id, int Player, string Trigger, int City, int Plot, int Turn);
/// <summary>An event was started for a player.</summary>
public sealed record EventTriggered(int Player, int EventId, string Trigger, int City) : DomainEvent;
/// <summary>A player or the automatic fallback resolved an event.</summary>
public sealed record EventResolved(int Player, int EventId, string Trigger, string Option, bool Automatic) : DomainEvent;

/// <summary>Optional events owner. Queries never change state.</summary>
public interface IEventService
{
    /// <summary>Unresolved events of a player, by id.</summary>
    IReadOnlyList<PendingEvent> Pending(Simulation sim, int player);
    /// <summary>Option ids that a player can pick now, in id order.</summary>
    IReadOnlyList<string> Options(Simulation sim, int eventId);
    /// <summary>Null when the option is legal.</summary>
    string? ValidateChoice(Simulation sim, int player, int eventId, string option);
    /// <summary>Apply a validated option and publish EventResolved.</summary>
    void Choose(Simulation sim, int player, int eventId, string option, bool automatic = false);
}

/// <summary>Resolve one pending event.</summary>
public sealed class ChooseEventOptionCommand : GameCommand
{
    public int EventId { get; init; }
    public string Option { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IEventService>().Single().ValidateChoice(sim, Player, EventId, Option);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IEventService>().Single().Choose(sim, Player, EventId, Option);
}
