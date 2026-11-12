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
    /// <summary>Active ongoing event modifier effects, by id order.</summary>
    IReadOnlyList<ActiveEventEffect> ActiveEffects(Simulation sim);
    /// <summary>Active ongoing event modifier effects of a player, by id order.</summary>
    IReadOnlyList<ActiveEventEffect> ActiveEffects(Simulation sim, int player);
    /// <summary>Finds an active effect by its canonical identifier, or null if not found.</summary>
    ActiveEventEffect? FindEffect(Simulation sim, int effectId);
    /// <summary>Removes an active effect by its canonical identifier, returning true if found and removed.</summary>
    bool CancelEffect(Simulation sim, int effectId);
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
    public int NextEffectId { get; set; } = 1;
    public List<PendingEvent> Pending { get; set; } = [];
    /// <summary>Last turn an event happened, keyed "player:trigger".</summary>
    public SortedDictionary<string, int> LastFired { get; set; } = new(StringComparer.Ordinal);
    public List<ActiveEventEffect> Effects { get; set; } = [];

    [JsonIgnore]
    public bool HasPending => Pending.Count > 0;

    [JsonIgnore]
    public int PendingCount => Pending.Count;

    [JsonIgnore]
    public int ActiveEffectsCount => Effects.Count;

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

    /// <summary>Adds an active modifier effect from an event option choice.</summary>
    public void AddEffect(ActiveEventEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.Id <= 0) effect.Id = NextEffectId++;
        Effects.Add(effect);
    }

    /// <summary>Finds an active effect by its canonical identifier, or null if not present.</summary>
    public ActiveEventEffect? FindEffect(int effectId) =>
        Effects.FirstOrDefault(e => e.Id == effectId);

    /// <summary>Removes an active effect by its canonical identifier, returning true if removed.</summary>
    public bool RemoveEffect(int effectId)
    {
        int index = Effects.FindIndex(e => e.Id == effectId);
        if (index >= 0)
        {
            Effects.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>Removes timed effects where ExpiresTurn is greater than zero and currentTurn has reached or exceeded ExpiresTurn.</summary>
    public int PruneExpiredEffects(int currentTurn) =>
        Effects.RemoveAll(e => e.ExpiresTurn > 0 && currentTurn >= e.ExpiresTurn);

    /// <summary>Removes active effects associated with a captured or razed city.</summary>
    public int PruneCityEffects(int cityId)
    {
        if (cityId < 0) return 0;
        return Effects.RemoveAll(e => e.City == cityId);
    }

    /// <summary>Removes effects associated with cities that no longer exist or whose owner no longer matches the effect recipient.</summary>
    public int PruneInvalidEffects(Simulation sim)
    {
        ArgumentNullException.ThrowIfNull(sim);
        return Effects.RemoveAll(e =>
        {
            if (e.City >= 0)
            {
                var city = sim.State.FindCity(e.City);
                if (city is null || city.Owner != e.Player) return true;
            }
            if (e.Plot >= 0 && e.Plot >= sim.State.Plots.Length)
            {
                return true;
            }
            return false;
        });
    }

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

/// <summary>A kept happiness, health or plot yield change. ExpiresTurn zero means permanent.</summary>
public sealed class ActiveEventEffect
{
    public int Id { get; set; }
    public int EventId { get; set; } = -1;
    public string Trigger { get; set; } = "";
    public int Player { get; set; }
    public int City { get; set; } = -1;
    public int Plot { get; set; } = -1;
    public string Option { get; set; } = "";
    public int ExpiresTurn { get; set; }
    public int Happiness { get; set; }
    public int Health { get; set; }
    public int PlotFood { get; set; }
    public int PlotProduction { get; set; }
    public int PlotCommerce { get; set; }

    [JsonIgnore]
    public bool IsPermanent => ExpiresTurn == 0;

    [JsonIgnore]
    public bool IsCityScope => City >= 0;

    [JsonIgnore]
    public bool IsEmpireScope => City == -1;

    [JsonIgnore]
    public bool IsPlotScope => Plot >= 0;
}
