using Civ.Game;

namespace Civ.Client;

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

    /// <summary>Battles waiting to be animated, oldest first.</summary>
    public IReadOnlyCollection<BattlePlayback> PendingBattles => _battles;

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
        if (Placement(c.Attacker) is not { } a || Placement(c.Defender) is not { } d) return;
        var report = OptionalService<ICombatService>()?.History(Sim)
            .LastOrDefault(r => r.Attacker == c.Attacker && r.Defender == c.Defender);
        int loser = c.Winner == c.Attacker ? c.Defender : c.Attacker;
        var outcome = report?.Outcome ?? (c.Winner == c.Attacker ? CombatOutcome.AttackerWon : CombatOutcome.DefenderWon);
        _battles.Enqueue(new BattlePlayback(c.Attacker, a.Type, a.Owner, a.Hex, c.Defender, d.Type, d.Owner, d.Hex,
            outcome, c.Winner, loser, Sim.State.FindUnit(loser) == null, report?.Rounds ?? []));
        while (_battles.Count > MaxBattles) _battles.Dequeue();
    }
}
