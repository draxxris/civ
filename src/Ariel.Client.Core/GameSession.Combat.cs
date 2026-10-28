using Ariel.Game;

namespace Ariel.Client;

/// <summary>One fight as the map should replay it. Hexes are where each unit stood before the fight.</summary>
public sealed record BattlePlayback(
    int AttackerId, string AttackerType, int AttackerOwner, Hex AttackerHex,
    int DefenderId, string DefenderType, int DefenderOwner, Hex DefenderHex,
    CombatOutcome Outcome, int WinnerId, int LoserId, bool LoserDied, IReadOnlyList<CombatRound> Rounds);

public sealed partial class GameSession
{
    private const int MaxBattles = 64;

    private readonly Queue<BattlePlayback> _battles = new();
    private Dictionary<int, (string Type, int Owner, Hex Hex)> _unitSnapshot = [];

    /// <summary>Take the oldest battle to animate, or null when none is waiting.</summary>
    public BattlePlayback? DequeueBattle() => _battles.Count > 0 ? _battles.Dequeue() : null;

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
        if (battle is null) return;
        _battles.Enqueue(battle);
        while (_battles.Count > MaxBattles) _battles.Dequeue();
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
