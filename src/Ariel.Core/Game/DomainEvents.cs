namespace Ariel.Game;

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
/// <summary>A unit moved from one plot to another, usually a single step, for presentation to animate.</summary>
public sealed record UnitMoved(int Unit, int Player, Hex From, Hex To) : DomainEvent;
/// <summary>A unit was removed because its owner could not pay recurring costs.</summary>
public sealed record UnitDisbandedForDeficit(int UnitId, int Player, Hex Position) : DomainEvent;
/// <summary>A turn began.</summary>
public sealed record TurnStarted(int Turn) : DomainEvent;
/// <summary>A turn ended after all rule systems ran.</summary>
public sealed record TurnEnded(int Turn) : DomainEvent;
/// <summary>Mod-registered domain event handler contract.</summary>
public interface IDomainEventSubscriber
{
    /// <summary>Handle a notification. Any state changes must be deterministic.</summary>
    void Handle(Simulation sim, DomainEvent message);
    /// <summary>Whether this subscriber may act on events of <paramref name="eventType"/>. An event that some subscriber
    /// handles widens the write window it is published in to everything (<see cref="Simulation"/>, "Write windows"),
    /// since the subscriber may write anything. The default, true, is the safe answer.</summary>
    bool Handles(Type eventType) => true;
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
    private readonly Dictionary<Type, bool> _handled = new();
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
    /// <summary>Forget presentation events published after a restored state point, and any events still queued
    /// for dispatch: they belong to the abandoned branch.</summary>
    internal void Truncate(int count)
    {
        _turnEvents.RemoveRange(count, _turnEvents.Count - count);
        _pending.Clear();
    }
    /// <summary>Take over another bus's events, as a copy of this simulation made them: this turn's in a list of
    /// our own, and the previous turn's as the same list, so readers can tell whether the turn rolled over.</summary>
    internal void CopyFrom(DomainEventBus other)
    {
        _turnEvents.Clear();
        _turnEvents.AddRange(other._turnEvents);
        PreviousTurn = other.PreviousTurn;
    }
    internal void BeginTurn()
    {
        PreviousTurn = Array.AsReadOnly(_turnEvents.ToArray());
        _turnEvents.Clear();
    }
    private bool Handled(Type type)
    {
        if (_handled.TryGetValue(type, out bool handled)) return handled;
        handled = _subscribers.Any(s => s.Handles(type));
        _handled[type] = handled;
        return handled;
    }
    /// <summary>Publish and dispatch to all subscribers in mod registration order. If a subscriber throws, the
    /// events still queued behind the failing one are dropped with it, so the next unrelated publish does not
    /// dispatch them as if its own command had caused them.</summary>
    public void Publish(DomainEvent message)
    {
        // A subscriber may write anything: the open write window widens to everything, and an event published
        // outside any window dispatches inside one of its own, so the subscribers' writes are always stamped.
        bool handled = Handled(message.GetType());
        bool widened = handled && _sim.WidenWriteWindow();
        _pending.Enqueue(message);
        if (_dispatching) return;
        _dispatching = true;
        using var window = handled && !widened ? _sim.OpenWriteWindow(null, "event:" + message.GetType().Name) : default;
        try
        {
            while (_pending.TryDequeue(out var next))
            {
                _turnEvents.Add(next);
                foreach (var subscriber in _subscribers) subscriber.Handle(_sim, next);
            }
        }
        catch
        {
            _pending.Clear();
            throw;
        }
        finally { _dispatching = false; }
    }
}
