namespace Civ.Game;

/// <summary>Immutable rule notification. Dispatch is synchronous, FIFO, and deterministic.</summary>
public abstract record DomainEvent;
/// <summary>A team learned a technology.</summary>
public sealed record TechDiscovered(int Team, string Tech, bool ResearchCompletion = false) : DomainEvent;
/// <summary>A player founded a city.</summary>
public sealed record CityFounded(int City, int Player, Hex Position) : DomainEvent;
/// <summary>A city's population changed through growth.</summary>
public sealed record CityGrew(int City, int Population) : DomainEvent;
/// <summary>A city completed a building.</summary>
public sealed record BuildingCompleted(int City, string Building) : DomainEvent;
/// <summary>A unit entered the game.</summary>
public sealed record UnitCreated(int Unit, int Player, string Type, Hex Position) : DomainEvent;
/// <summary>A unit was removed because its owner could not pay recurring costs.</summary>
public sealed record UnitDisbandedForDeficit(int UnitId, int Player, Hex Position) : DomainEvent;
/// <summary>A combat finished; the loser was removed. Report is the fight as it resolved, so presentation can replay it after the turn rolls over.</summary>
public sealed record CombatResolved(int Attacker, int Defender, int Winner, int LoserOwner = -1, CombatReport? Report = null) : DomainEvent;
public sealed record AirMissionResolved(AirMissionReport Report) : DomainEvent;
/// <summary>A turn began.</summary>
public sealed record TurnStarted(int Turn) : DomainEvent;
/// <summary>A turn ended after all rule systems ran.</summary>
public sealed record TurnEnded(int Turn) : DomainEvent;
/// <summary>Mod-registered domain event handler contract.</summary>
public interface IDomainEventSubscriber
{
    /// <summary>Handle a notification. Any state changes must be deterministic.</summary>
    void Handle(Simulation sim, DomainEvent message);
}
/// <summary>Per-simulation dispatch bus. Nested publishes are queued, not recursively dispatched.
/// Presentation events are not saved or hashed.</summary>
public sealed class DomainEventBus
{
    private readonly Simulation _sim;
    private readonly IReadOnlyList<IDomainEventSubscriber> _subscribers;
    private readonly List<DomainEvent> _turnEvents = new();
    private readonly Queue<DomainEvent> _pending = new();
    private bool _dispatching;
    internal DomainEventBus(Simulation sim)
    {
        _sim = sim; _subscribers = sim.Content.Services.Get<IDomainEventSubscriber>();
    }
    /// <summary>Published events for the current turn, in publication order.</summary>
    public IReadOnlyList<DomainEvent> Published => _turnEvents.AsReadOnly();
    /// <summary>Complete previous turn's presentation events, retained until the next turn completes.</summary>
    public IReadOnlyList<DomainEvent> PreviousTurn { get; private set; } = [];
    /// <summary>Number of presentation events published this turn.</summary>
    internal int PublishedCount => _turnEvents.Count;
    /// <summary>Forget presentation events published after a restored state point.</summary>
    internal void Truncate(int count) => _turnEvents.RemoveRange(count, _turnEvents.Count - count);
    internal void BeginTurn()
    {
        PreviousTurn = Array.AsReadOnly(_turnEvents.ToArray());
        _turnEvents.Clear();
    }
    /// <summary>Publish and dispatch to all subscribers in mod registration order.</summary>
    public void Publish(DomainEvent message)
    {
        _pending.Enqueue(message);
        if (_dispatching) return;
        _dispatching = true;
        try
        {
            while (_pending.TryDequeue(out var next))
            {
                _turnEvents.Add(next);
                foreach (var subscriber in _subscribers) subscriber.Handle(_sim, next);
            }
        }
        finally { _dispatching = false; }
    }
}
