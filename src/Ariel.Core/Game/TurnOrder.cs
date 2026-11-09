namespace Ariel.Game;

/// <summary>How players share a turn.</summary>
public enum TurnMode
{
    /// <summary>Everyone moves at once.</summary>
    Simultaneous,
    /// <summary>Players at peace move together; teams at war with each other move one after the other.</summary>
    Dynamic,
    /// <summary>One player at a time.</summary>
    Sequential,
}

/// <summary>
/// The segments of the current turn: groups of players that move together, in play order, and which group is moving.
/// The order is planned when the turn starts and kept until it ends, so a war declared mid-turn changes the next turn.
/// It is authoritative state: every peer must agree on whose move it is.
/// </summary>
public sealed class TurnOrder
{
    /// <summary>Player ids per segment, in play order. Empty means everyone moves together, as in games saved before
    /// turns had segments.</summary>
    public List<int[]> Segments { get; set; } = [];
    /// <summary>Index of the segment that is moving; equal to the segment count once all have finished.</summary>
    public int Active { get; set; }

    /// <summary>Whether every segment has finished.</summary>
    public bool Finished => Active >= Segments.Count;

    /// <summary>Whether a player may move now: it belongs to the segment that is moving, or it joined the game after
    /// the turn was planned and so is bound by no segment (otherwise it could never end its turn).</summary>
    public bool IsActing(int player)
    {
        if (!Finished && Array.IndexOf(Segments[Active], player) >= 0) return true;
        foreach (var segment in Segments)
            if (Array.IndexOf(segment, player) >= 0) return false;
        return true;
    }

    internal TurnOrder Clone() => new() { Segments = Segments.ConvertAll(s => s.ToArray()), Active = Active };

    /// <summary>Split the players who still have a move to make into segments for <paramref name="mode"/>. Players
    /// that ended their turn before it began (barbarians, rebels) take no part. Teammates always move together, and
    /// ties break by ascending id, so every peer plans the same order.</summary>
    public static List<int[]> Plan(GameState state, TurnMode mode)
    {
        var moving = state.Players.Where(p => !p.EndedTurn).OrderBy(p => p.Id).ToList();
        if (moving.Count == 0) return [];
        switch (mode)
        {
            case TurnMode.Simultaneous:
                return [moving.Select(p => p.Id).ToArray()];
            case TurnMode.Sequential:
                return moving.Select(p => new[] { p.Id }).ToList();
            case TurnMode.Dynamic:
                var segments = new List<List<int>>();   // team ids per segment
                foreach (int team in moving.Select(p => p.TeamId).Distinct().Order())
                {
                    var wars = state.TeamById(team).Wars;
                    var home = segments.FirstOrDefault(s => !s.Any(other => wars.Contains(other) || state.TeamById(other).Wars.Contains(team)));
                    if (home is null) segments.Add(home = []);
                    home.Add(team);
                }
                return segments.Select(teams => moving.Where(p => teams.Contains(p.TeamId)).Select(p => p.Id).ToArray()).ToList();
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown turn mode");
        }
    }
}
