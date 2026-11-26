using System.Collections.Frozen;
using Ariel.Content;
using Ariel.Game;

namespace Ariel.Rules.Foundation;

/// <summary>The store of timers (<see cref="ITimers"/>): starting with the kind's stacking rule, ending by time, by
/// holder loss or by hand, and the hooks and events of each. Ordering is deterministic: timers are kept in start order
/// and every pass over them, and every hook, follows it.</summary>
internal sealed class Timers : ITimers
{
    private FrozenDictionary<string, ITimerKind>? _kinds;

    internal static TimerState? Read(Simulation sim) => sim.State.Extensions.Get<TimerState>(sim.Content);
    private static TimerState Edit(Simulation sim)
    {
        var state = Read(sim);
        if (state is null) sim.State.Extensions.Set(sim.Content, state = new TimerState());
        return state;
    }

    private FrozenDictionary<string, ITimerKind> Kinds(Simulation sim) =>
        _kinds ??= sim.Content.Services.Get<ITimerKind>().ToFrozenDictionary(k => k.Id, StringComparer.Ordinal);
    private ITimerKind? KindOf(Simulation sim, string id) => Kinds(sim).GetValueOrDefault(id);

    private static bool Same(GameTimer a, GameTimer b) =>
        a.Kind == b.Kind && a.Key == b.Key && a.Holder == b.Holder;

    public GameTimer Start(Simulation sim, GameTimer timer)
    {
        if (string.IsNullOrEmpty(timer.Kind)) throw new ArgumentException("A timer needs a kind.", nameof(timer));
        var kind = KindOf(sim, timer.Kind) ?? throw new ArgumentException($"No timer kind '{timer.Kind}' is registered.", nameof(timer));
        var state = Edit(sim);
        int turn = sim.State.Turn;
        GameTimer? before = null;
        List<GameTimer>? lapsed = null;
        int expires = timer.ExpiresTurn;
        if (kind.Stacking != TimerStacking.Stack)
        {
            var list = state.Timers;
            int write = 0;
            for (int read = 0; read < list.Count; read++)
            {
                var stored = list[read];
                if (!Same(stored, timer)) { list[write++] = stored; continue; }
                if (!stored.IsActive(turn)) (lapsed ??= []).Add(stored);
                else
                {
                    before ??= stored;
                    if (kind.Stacking == TimerStacking.Prolong)
                        expires = expires == 0 || stored.ExpiresTurn == 0 ? 0 : Math.Max(expires, stored.ExpiresTurn);
                }
            }
            list.RemoveRange(write, list.Count - write);
        }
        var started = timer with { Id = state.NextId++, ExpiresTurn = expires };
        state.Timers.Add(started);
        if (lapsed is not null) foreach (var gone in lapsed) Finish(sim, gone, TimerEnd.Expired);
        if (before is null) { kind.Started(sim, started); sim.Events.Publish(new TimerStarted(started)); }
        else { kind.Changed(sim, before, started); sim.Events.Publish(new TimerChanged(before, started)); }
        return started;
    }

    public bool Cancel(Simulation sim, int id)
    {
        if (Read(sim) is not { } state) return false;
        int index = state.Timers.FindIndex(t => t.Id == id);
        if (index < 0) return false;
        var timer = state.Timers[index];
        state.Timers.RemoveAt(index);
        Finish(sim, timer, TimerEnd.Cancelled);
        return true;
    }

    public GameTimer? Find(Simulation sim, int id)
    {
        if (Read(sim) is not { } state) return null;
        foreach (var timer in state.Timers) if (timer.Id == id) return timer;
        return null;
    }

    public GameTimer? Find(Simulation sim, string kind, TimerHolder holder, string key = "")
    {
        if (Read(sim) is not { } state) return null;
        int turn = sim.State.Turn;
        foreach (var timer in state.Timers)
            if (timer.IsActive(turn) && timer.Kind == kind && timer.Key == key && timer.Holder == holder) return timer;
        return null;
    }

    public bool IsActive(Simulation sim, string kind, TimerHolder holder, string key = "") => Find(sim, kind, holder, key) is not null;

    public IReadOnlyList<GameTimer> Active(Simulation sim)
    {
        if (Read(sim) is not { } state) return [];
        int turn = sim.State.Turn;
        return state.Timers.Where(t => t.IsActive(turn)).ToArray();
    }

    public IReadOnlyList<GameTimer> Active(Simulation sim, string kind)
    {
        if (Read(sim) is not { } state) return [];
        int turn = sim.State.Turn;
        return state.Timers.Where(t => t.Kind == kind && t.IsActive(turn)).ToArray();
    }

    public int Expiry(Simulation sim, int turns, int lead = 0) => checked(sim.State.Turn + lead + GamePace.Scale(sim, turns));

    /// <summary>The player whose turn a timer belongs to, or -1 for a pair or a holder nobody owns.</summary>
    private static int OwnerOf(Simulation sim, GameTimer timer)
    {
        if (timer.Holder.Unit is { } unit) return sim.State.FindUnit(unit)?.Owner ?? -1;
        if (timer.Holder.City is { } city) return sim.State.FindCity(city)?.Owner ?? -1;
        return timer.Holder.Player ?? -1;
    }

    /// <summary>Whether a timer ends in this pass, and why. <paramref name="turnEnd"/> is the end of
    /// <paramref name="player"/>'s turn; otherwise it is the start of the turn.</summary>
    private bool Ends(Simulation sim, GameTimer timer, bool turnEnd, int player, out TimerEnd reason)
    {
        reason = TimerEnd.Expired;
        int turn = sim.State.Turn;
        if (turnEnd)
        {
            if (timer.ExpiresTurn == 0 || turn + 1 < timer.ExpiresTurn) return false;
            return KindOf(sim, timer.Kind)?.Expiry == TimerExpiry.OwnerTurnEnd && OwnerOf(sim, timer) == player;
        }
        if (timer.Holder.City is { } city && sim.State.FindCity(city) is null
            || timer.Holder.Unit is { } unit && sim.State.FindUnit(unit) is null)
        {
            reason = TimerEnd.HolderLost;
            return true;
        }
        return !timer.IsActive(turn);
    }

    /// <summary>Drop the timers that ended: at the start of a turn (<paramref name="turnEnd"/> false) those whose time
    /// is up or whose holder is gone, at the end of a player's turn the owner's <see cref="TimerExpiry.OwnerTurnEnd"/>
    /// ones. Nothing is allocated when nothing ends.</summary>
    /// <summary>What the expiry steps declare: nothing, as an expiry widens.</summary>
    internal static readonly IReadOnlyList<string> WritesNothing = [];
    internal void Expire(Simulation sim, bool turnEnd, int player = -1)
    {
        if (Read(sim) is not { Timers.Count: > 0 } state) return;
        var list = state.Timers;
        int first = -1;
        for (int i = 0; i < list.Count && first < 0; i++)
            if (Ends(sim, list[i], turnEnd, player, out _)) first = i;
        if (first < 0) return;
        // Timers carry effects that sources read, and ending one runs its kind and subscribers: the expiry steps declare
        // that they write nothing, so an expiry widens first.
        sim.WidenWriteWindow();
        var ended = new List<(GameTimer Timer, TimerEnd Reason)>();
        int write = first;
        for (int read = first; read < list.Count; read++)
        {
            var timer = list[read];
            if (Ends(sim, timer, turnEnd, player, out var reason)) ended.Add((timer, reason));
            else list[write++] = timer;
        }
        list.RemoveRange(write, list.Count - write);
        foreach (var (timer, reason) in ended) Finish(sim, timer, reason);
    }

    /// <summary>A city changed hands (<paramref name="newOwner"/>) or was destroyed (null): each timer it holds follows
    /// its kind's <see cref="ITimerKind.OnOwnerChange"/>, and always ends with a destroyed city.</summary>
    internal void CityLeft(Simulation sim, int city, int? newOwner)
    {
        if (Read(sim) is not { Timers.Count: > 0 } state) return;
        var list = state.Timers;
        List<GameTimer>? ended = null;
        int write = 0;
        for (int read = 0; read < list.Count; read++)
        {
            var timer = list[read];
            if (timer.Holder.City != city) { list[write++] = timer; continue; }
            var rule = newOwner is null ? TimerOwnerChange.End : KindOf(sim, timer.Kind)?.OnOwnerChange ?? TimerOwnerChange.End;
            switch (rule)
            {
                case TimerOwnerChange.End: (ended ??= []).Add(timer); break;
                case TimerOwnerChange.Keep: list[write++] = timer; break;
                default: list[write++] = timer with { Holder = timer.Holder with { Player = newOwner } }; break;
            }
        }
        if (ended is null && write == list.Count) return;
        list.RemoveRange(write, list.Count - write);
        if (ended is not null) foreach (var timer in ended) Finish(sim, timer, TimerEnd.HolderLost);
    }

    private void Finish(Simulation sim, GameTimer timer, TimerEnd reason)
    {
        KindOf(sim, timer.Kind)?.Ended(sim, timer, reason);
        sim.Events.Publish(new TimerEnded(timer, reason));
    }
}

/// <summary>Drops the timers that ran out or lost their holder, at the start of a turn's processing.</summary>
internal sealed class TimersTurnStart(Timers timers) : ITurnSystem
{
    public string Id => "foundation:timers";
    public int Order => TurnPhase.GameBegin;
    /// <summary>Nothing, unless a timer ends, which widens (<see cref="Timers.Expire"/>).</summary>
    public IReadOnlyList<string>? WriteDomains => Timers.WritesNothing;
    public void Run(Simulation sim) => timers.Expire(sim, turnEnd: false);
}

/// <summary>Drops the player's timers of kinds that end with the owner's turn (<see cref="TimerExpiry.OwnerTurnEnd"/>).</summary>
internal sealed class TimersTurnEnd(Timers timers) : IPlayerTurnSystem
{
    public string Id => "foundation:timers_turn_end";
    public int Order => TimerSystems.TurnEndOrder;
    /// <summary>Nothing, unless a timer ends, which widens (<see cref="Timers.Expire"/>).</summary>
    public IReadOnlyList<string>? WriteDomains => Timers.WritesNothing;
    public void Run(Simulation sim, int player) => timers.Expire(sim, turnEnd: true, player);
}
