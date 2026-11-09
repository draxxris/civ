using Ariel.Game;

namespace Ariel.Client;

/// <summary>Something the map replays, in the order it happened: units stepping and units fighting.</summary>
public abstract record MapPlayback;

/// <summary>One unit's step from a plot to the next.</summary>
public sealed record StepPlayback(int UnitId, string Type, int Owner, Hex From, Hex To) : MapPlayback;

/// <summary>One fight as the map should replay it. Hexes are where each unit stood before the fight.</summary>
public sealed record BattlePlayback(
    int AttackerId, string AttackerType, int AttackerOwner, Hex AttackerHex,
    int DefenderId, string DefenderType, int DefenderOwner, Hex DefenderHex,
    CombatOutcome Outcome, int WinnerId, int LoserId, bool LoserDied, IReadOnlyList<CombatRound> Rounds) : MapPlayback;

public sealed partial class GameSession
{
    /// <summary>Most replays kept waiting; the oldest give way, and the map shows where units ended up.</summary>
    private const int MaxPlayback = 1024;

    private readonly Queue<MapPlayback> _playback = new();
    private Dictionary<int, (string Type, int Owner, Hex Hex)> _unitSnapshot = [];

    /// <summary>Take the oldest step or battle to animate, or null when none is waiting.</summary>
    public MapPlayback? DequeuePlayback() => _playback.Count > 0 ? _playback.Dequeue() : null;

    private void Enqueue(MapPlayback item)
    {
        _playback.Enqueue(item);
        while (_playback.Count > MaxPlayback) _playback.Dequeue();
    }

    /// <summary>Steps of a unit that only lived between two reads have no type to draw, so they are skipped.</summary>
    private void RecordStep(UnitMoved m)
    {
        if (m.From == m.To || Placement(m.Unit) is not { } unit) return;
        Enqueue(new StepPlayback(m.Unit, unit.Type, m.Player, m.From, m.To));
    }

    private void SnapshotUnits() =>
        _unitSnapshot = Sim.State.Units.ToDictionary(u => u.Id, u => (u.Type, u.Owner, u.Position));

    private (string Type, int Owner, Hex Hex)? Placement(int id)
    {
        if (_unitSnapshot.TryGetValue(id, out var before)) return before;
        return Sim.State.FindUnit(id) is { } unit ? (unit.Type, unit.Owner, unit.Position) : null;
    }

    private void RecordBattle(CombatResolved c)
    {
        var battle = c.Report is { } report ? FromReport(c, report) : FromPlacement(c);
        if (battle is not null) Enqueue(battle);
    }

    /// <summary>The report was captured as the fight resolved, so it stays exact after turn rollover, unit removal and later moves.</summary>
    private static BattlePlayback FromReport(CombatResolved c, CombatReport r) =>
        new(c.Attacker, r.AttackerType, r.AttackerOwner, r.AttackerHex, c.Defender, r.DefenderType, r.DefenderOwner, r.DefenderHex,
            r.Outcome, c.Winner, Loser(c), r.LoserDied, r.Rounds);

    /// <summary>Fallback for events published without a report: places the units from the last snapshot and infers the outcome.</summary>
    private BattlePlayback? FromPlacement(CombatResolved c)
    {
        if (Placement(c.Attacker) is not { } a || Placement(c.Defender) is not { } d) return null;
        int loser = Loser(c);
        return new BattlePlayback(c.Attacker, a.Type, a.Owner, a.Hex, c.Defender, d.Type, d.Owner, d.Hex,
            c.Winner == c.Attacker ? CombatOutcome.AttackerWon : CombatOutcome.DefenderWon, c.Winner, loser,
            Sim.State.FindUnit(loser) == null, []);
    }

    private static int Loser(CombatResolved c) => c.Winner == c.Attacker ? c.Defender : c.Attacker;
}
