using System.Text.Json.Serialization;
using Ariel.Game;

namespace Ariel.Mods.Events;

/// <summary>Strongly-typed allocation-free key for event trigger cooldowns.</summary>
public readonly record struct EventCooldownKey : IComparable<EventCooldownKey>
{
    public int Player { get; }
    public string Trigger { get; }

    public EventCooldownKey(int player, string trigger)
    {
        Player = player;
        Trigger = trigger ?? string.Empty;
    }

    public static EventCooldownKey Of(int player, string trigger) => new(player, trigger);

    [JsonIgnore]
    public string Key => $"{Player}:{Trigger}";

    public override string ToString() => Key;

    public int CompareTo(EventCooldownKey other)
    {
        int cmp = Player.CompareTo(other.Player);
        return cmp != 0 ? cmp : string.CompareOrdinal(Trigger, other.Trigger);
    }

    public static EventCooldownKey Parse(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        int colon = key.IndexOf(':');
        if (colon < 0) throw new FormatException($"Invalid event cooldown key: {key}");
        return new(int.Parse(key.AsSpan(0, colon)), key[(colon + 1)..]);
    }

    public static implicit operator string(EventCooldownKey key) => key.Key;
}

/// <summary>An event waiting for a player choice. City and Plot are -1 when the event has no such subject.</summary>
public sealed record PendingEvent(int Id, int Player, string Trigger, int City, int Plot, int Turn);
/// <summary>An event was started for a player.</summary>
public sealed record EventTriggered(int Player, int EventId, string Trigger, int City) : DomainEvent;
/// <summary>A player or the automatic fallback resolved an event.</summary>
public sealed record EventResolved(int Player, int EventId, string Trigger, string Option, bool Automatic) : DomainEvent
{
    /// <summary>Option name indicating that a pending event expired with no valid options available.</summary>
    public const string Expired = "expired";
}

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
    /// <summary>The kept effects of a player's resolved events, in start order: timers of kind
    /// <c>events:option_effect</c> in the base game's store (<see cref="GameRules.Timers"/>) whose key is the chosen
    /// <see cref="EventOptionDef"/>. Cancel one with <see cref="ITimers.Cancel"/>.</summary>
    IReadOnlyList<GameTimer> ActiveEffects(Simulation sim, int player);
}

/// <summary>Resolve one pending event.</summary>
public sealed class ChooseEventOptionCommand : GameCommand
{
    public int EventId { get; init; }
    public string Option { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Service<IEventService>()!.ValidateChoice(sim, Player, EventId, Option);
    public override void Apply(Simulation sim) => sim.Service<IEventService>()!.Choose(sim, Player, EventId, Option);
}

/// <summary>Saved game-wide event state. No derived data is stored.</summary>
public sealed class EventState
{
    public int NextEventId { get; set; } = 1;
    public List<PendingEvent> Pending { get; set; } = [];
    /// <summary>Last turn an event happened, keyed "player:trigger".</summary>
    public SortedDictionary<string, int> LastFired { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore]
    public bool HasPending => Pending.Count > 0;

    [JsonIgnore]
    public int PendingCount => Pending.Count;

    /// <summary>Adds a pending event waiting for player resolution, replacing any existing entry with the same id.</summary>
    public void AddPending(PendingEvent pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        Pending.RemoveAll(p => p.Id == pending.Id);
        Pending.Add(pending);
    }

    /// <summary>Removes a pending event by its identifier.</summary>
    public bool RemovePending(int eventId)
    {
        int index = Pending.FindIndex(p => p.Id == eventId);
        if (index >= 0)
        {
            Pending.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>Finds a pending event by id, or null if not present.</summary>
    public PendingEvent? FindPending(int eventId) =>
        Pending.FirstOrDefault(p => p.Id == eventId);

    /// <summary>Records the turn an event trigger fired for a player using a typed cooldown key.</summary>
    public void RecordFired(EventCooldownKey key, int turn) =>
        LastFired[key.Key] = turn;

    /// <summary>Records the turn an event trigger fired for a player.</summary>
    public void RecordFired(int player, string trigger, int turn) =>
        RecordFired(EventCooldownKey.Of(player, trigger), turn);

    /// <summary>Returns the last turn an event trigger fired for a player, or null if it has never fired.</summary>
    public int? GetLastFired(EventCooldownKey key) =>
        LastFired.TryGetValue(key.Key, out int turn) ? turn : null;

    /// <summary>Returns the last turn an event trigger fired for a player, or null if it has never fired.</summary>
    public int? GetLastFired(int player, string trigger) =>
        GetLastFired(EventCooldownKey.Of(player, trigger));
}
