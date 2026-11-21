using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ariel.Content;

namespace Ariel.Game;

/// <summary>What a timer is attached to. Unused parts are null. A timer may name several parts at once: an event's
/// effect is held by its player, its city and its plot, and the part decides where its modifiers apply (see
/// <see cref="GameTimer.Effects"/>) and what ends it (<see cref="ITimerKind.OnOwnerChange"/>, a vanished city or unit).
/// <see cref="Team"/> with <see cref="Rival"/> is a pair: something one team has against another.</summary>
public readonly record struct TimerHolder
{
    public int? Player { get; init; }
    public int? City { get; init; }
    public int? Plot { get; init; }
    public int? Unit { get; init; }
    public int? Team { get; init; }
    /// <summary>The other team of a pair; only with <see cref="Team"/>.</summary>
    public int? Rival { get; init; }

    public static TimerHolder OfPlayer(int player) => new() { Player = player };
    public static TimerHolder OfCity(int city) => new() { City = city };
    public static TimerHolder OfPlot(int plot) => new() { Plot = plot };
    public static TimerHolder OfUnit(int unit) => new() { Unit = unit };
    public static TimerHolder OfTeams(int team, int rival) => new() { Team = team, Rival = rival };
}

/// <summary>Something that lasts for a while, kept by the base game (<see cref="GameRules.Timers"/>): an event's
/// happiness, a spy's unrest, a cooldown, a team's protection against a rival. It belongs to a registered
/// <see cref="ITimerKind"/>, which declares how a second one of the same kind and holder combines with it, what ends
/// it besides time, and what happens when it starts, changes or ends. A timer may also carry modifiers, which the
/// base game adds to the calculations of the scope each applies to: a plot effect to <see cref="TimerHolder.Plot"/>; a
/// city effect to <see cref="TimerHolder.City"/>, or with no city to every city <see cref="TimerHolder.Player"/>
/// owns; a player effect to <see cref="TimerHolder.Player"/>; a unit effect to <see cref="TimerHolder.Unit"/>.
/// Immutable once started.</summary>
public sealed record GameTimer
{
    /// <summary>Assigned when the timer starts; unique for the game and rising in start order.</summary>
    public int Id { get; init; }
    /// <summary>The registered <see cref="ITimerKind.Id"/>.</summary>
    public string Kind { get; init; } = "";
    /// <summary>Tells apart timers of one kind and holder that are not the same thing (an event option's id), and
    /// names the ledger source of the modifiers. Empty: the kind's id is the source.</summary>
    public string Key { get; init; } = "";
    public TimerHolder Holder { get; init; }
    /// <summary>The first turn the timer no longer applies; 0 means it never ends by time.</summary>
    public int ExpiresTurn { get; init; }
    /// <summary>Modifiers the timer grants while it applies; none for a timer that only limits an action.</summary>
    public ImmutableArray<ModifierEffect> Effects { get; init; } = [];
    /// <summary>A number the kind keeps with the timer, such as a cost percentage.</summary>
    public int Value { get; init; }

    /// <summary>The ledger source of the timer's modifier lines: its <see cref="Key"/>, or else its kind.</summary>
    [JsonIgnore] public string Source => Key.Length > 0 ? Key : Kind;
    [JsonIgnore] public bool IsPermanent => ExpiresTurn == 0;
    /// <summary>Whether the timer applies on this turn.</summary>
    public bool IsActive(int turn) => ExpiresTurn == 0 || turn < ExpiresTurn;
    /// <summary>Turns the timer still applies, counting this one; null for a permanent timer.</summary>
    public int? TurnsRemaining(int turn) => ExpiresTurn == 0 ? null : Math.Max(0, ExpiresTurn - turn);
}

/// <summary>Saved timers, in the order they were started.</summary>
public sealed class TimerState
{
    public int NextId { get; set; } = 1;
    public List<GameTimer> Timers { get; set; } = [];
}

/// <summary>What starting a timer does when one of the same kind, key and holder is already stored.</summary>
public enum TimerStacking
{
    /// <summary>The new timer replaces the old one: its expiry and payload win, even if shorter. It counts as started
    /// again, so it is the newest timer.</summary>
    Replace,
    /// <summary>Like <see cref="Replace"/>, but a timer still running is never shortened: the later expiry wins.</summary>
    Prolong,
    /// <summary>Both are kept, each with its own expiry.</summary>
    Stack,
}

/// <summary>What happens to a timer held by a city when the city changes hands (captured or broken away).
/// A city that is destroyed ends its timers whatever the kind says.</summary>
public enum TimerOwnerChange
{
    /// <summary>The timer ends. The default: a conquest must not hand the loser's penalties or bonuses to the winner.</summary>
    End,
    /// <summary>The timer goes on untouched.</summary>
    Keep,
    /// <summary>The timer goes on, held by the city's new owner.</summary>
    Transfer,
}

/// <summary>When a timer that has run out is dropped.</summary>
public enum TimerExpiry
{
    /// <summary>At the start of the first turn it no longer applies (<c>foundation:timers</c>, <see cref="TurnPhase.GameBegin"/>).</summary>
    TurnStart,
    /// <summary>At the end of the last turn it applies, in its holder's owner's turn
    /// (<c>foundation:timers_turn_end</c>, <see cref="TimerSystems.TurnEndOrder"/>), for a timer whose end a rule of the
    /// owner's own turn depends on. It still stops applying on the expiry turn.</summary>
    OwnerTurnEnd,
}

/// <summary>Why a timer ended.</summary>
public enum TimerEnd
{
    /// <summary>Its expiry turn came.</summary>
    Expired,
    /// <summary>A feature ended it through <see cref="ITimers.Cancel"/>.</summary>
    Cancelled,
    /// <summary>Its city was captured, broke away or was destroyed, or its city or unit no longer exists.</summary>
    HolderLost,
}

/// <summary>A kind of timer, registered as a service by the module that owns it (<c>ctx.RegisterService&lt;ITimerKind&gt;</c>),
/// the way victory conditions and missions are. Its <see cref="IHasId"/> id is what <see cref="GameTimer.Kind"/>
/// names; two kinds with one id fail at load, and starting a timer of an unregistered kind throws. The properties are
/// the kind's declared rules; the hooks let the owner react. Hooks run after the store is already updated, in the
/// order the changes happened, and may use <see cref="ITimers"/> and change state, but must be deterministic and must
/// not use the RNG unless the feature already did. <see cref="TimerKind"/> is the ready-made base.</summary>
public interface ITimerKind : IHasId
{
    /// <summary>How a new timer combines with one of the same kind, key and holder.</summary>
    TimerStacking Stacking { get; }
    /// <summary>What a change of owner does to a timer held by a city.</summary>
    TimerOwnerChange OnOwnerChange { get; }
    /// <summary>When a timer of this kind that ran out is dropped.</summary>
    TimerExpiry Expiry { get; }
    /// <summary>A timer started with nothing of its kind and holder stored (or a <see cref="TimerStacking.Stack"/> one).</summary>
    void Started(Simulation sim, GameTimer timer);
    /// <summary>A timer took the place of another (<see cref="TimerStacking.Replace"/>, <see cref="TimerStacking.Prolong"/>).</summary>
    void Changed(Simulation sim, GameTimer before, GameTimer after);
    /// <summary>A timer ended, already removed.</summary>
    void Ended(Simulation sim, GameTimer timer, TimerEnd reason);
}

/// <summary>Ready-made <see cref="ITimerKind"/>: declare the rules with initializers and override the hooks that
/// matter. The defaults replace, end with a captured city, drop at the start of the turn and do nothing on hooks.</summary>
public class TimerKind(string id) : ITimerKind
{
    public string Id { get; } = id;
    public virtual TimerStacking Stacking { get; init; } = TimerStacking.Replace;
    public virtual TimerOwnerChange OnOwnerChange { get; init; } = TimerOwnerChange.End;
    public virtual TimerExpiry Expiry { get; init; } = TimerExpiry.TurnStart;
    public virtual void Started(Simulation sim, GameTimer timer) { }
    public virtual void Changed(Simulation sim, GameTimer before, GameTimer after) { }
    public virtual void Ended(Simulation sim, GameTimer timer, TimerEnd reason) { }
}

/// <summary>A timer started with nothing of its kind and holder stored, or stacked on one.</summary>
public sealed record TimerStarted(GameTimer Timer) : DomainEvent;
/// <summary>A timer replaced another one of its kind and holder.</summary>
public sealed record TimerChanged(GameTimer Before, GameTimer After) : DomainEvent;
/// <summary>A timer ended.</summary>
public sealed record TimerEnded(GameTimer Timer, TimerEnd Reason) : DomainEvent;

/// <summary>Where the base game's timer systems run, for kinds and features that order themselves around them.</summary>
public static class TimerSystems
{
    /// <summary>The phase of <c>foundation:timers_turn_end</c>: after the players' own end-of-turn upkeep.</summary>
    public const int TurnEndOrder = TurnPhase.PlayerEnd + 2;
}

/// <summary>The one store of things that last for a while: every "until turn N" a feature needs is a
/// <see cref="GameTimer"/> of a registered <see cref="ITimerKind"/>. Starting, cancelling and ending always go through
/// here, so stacking, holder loss, expiry order and hooks are the same for every feature. Timers expire in the order
/// they were started. Queries never change state or use the RNG, and cost a pass over the live timers, of which there
/// are few.</summary>
public interface ITimers
{
    /// <summary>Start a timer, combining it with a stored one as its kind's <see cref="ITimerKind.Stacking"/> says.
    /// Returns the stored timer, with its id. Throws for an unregistered kind.</summary>
    GameTimer Start(Simulation sim, GameTimer timer);
    /// <summary>End one timer by id before its time. Returns false when there is none.</summary>
    bool Cancel(Simulation sim, int id);
    /// <summary>The timer with this id, active or not yet dropped, or null.</summary>
    GameTimer? Find(Simulation sim, int id);
    /// <summary>The active timer of this kind, key and holder (the oldest, for a stacking kind), or null.</summary>
    GameTimer? Find(Simulation sim, string kind, TimerHolder holder, string key = "");
    /// <summary>Whether a timer of this kind, key and holder is active.</summary>
    bool IsActive(Simulation sim, string kind, TimerHolder holder, string key = "");
    /// <summary>Every active timer, in start order.</summary>
    IReadOnlyList<GameTimer> Active(Simulation sim);
    /// <summary>Every active timer of a kind, in start order.</summary>
    IReadOnlyList<GameTimer> Active(Simulation sim, string kind);
    /// <summary>The turn a timer of <paramref name="turns"/> nominal turns, scaled to the game's pace
    /// (<see cref="GamePace.Scale"/>), expires when it starts now; <paramref name="lead"/> turns later for one that
    /// counts from the next turn.</summary>
    int Expiry(Simulation sim, int turns, int lead = 0);
}
