using System.Text.Json.Serialization;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Game;

/// <summary>Score parts for one player. Total is the weighted sum.</summary>
public sealed record PlayerScore(int Population, int Land, int Techs, int Wonders, int Total);

/// <summary>Players' scores, kept by the Victory module (<see cref="GameRules.Scores"/>). Research diffusion helps
/// players who trail the score leader, and the score decides the time victory and every tie between teams. Queries
/// never change state or use the RNG.</summary>
public interface IPlayerScores
{
    /// <summary>The player's total score.</summary>
    int Score(Simulation sim, int player);
    /// <summary>The player's score by part.</summary>
    PlayerScore Breakdown(Simulation sim, int player);
}

/// <summary>Saved game-wide outcome: whether a team has won, and who, how and when. The first win stands and is
/// recorded only through <see cref="Record"/>, so the fields always agree: ids are -1 and the victory null until then.
/// A win does not stop play; the game carries on after it, and later events never rewrite it.</summary>
public sealed class VictoryGameState
{
    [JsonInclude] public bool IsGameOver { get; private set; }
    [JsonInclude] public int WinningTeamId { get; private set; } = -1;
    [JsonInclude] public int WinningPlayerId { get; private set; } = -1;
    [JsonInclude] public string? VictoryId { get; private set; }
    [JsonInclude] public int TurnWon { get; private set; }

    /// <summary>Record the win. Returns false, changing nothing, when a win is already recorded.</summary>
    public bool Record(int team, int player, string victory, int turn)
    {
        ArgumentException.ThrowIfNullOrEmpty(victory);
        if (IsGameOver) return false;
        (IsGameOver, WinningTeamId, WinningPlayerId, VictoryId, TurnWon) = (true, team, player, victory, turn);
        return true;
    }
}

/// <summary>Read-only view of the outcome plus the effective turn limit (0 without a time victory).</summary>
public sealed record VictoryStatus(bool IsGameOver, int WinningTeamId, int WinningPlayerId, string? VictoryId, int TurnWon, int MaxTurns);

/// <summary>The game's victories, run by the Victory module (<see cref="GameRules.Victory"/>): which are on, who has
/// won, and the check that decides it. Each <see cref="VictoryDef"/> names the <see cref="IVictoryCondition"/> that
/// decides it. Content that belongs to one victory (the council buildings to the diplomatic victory) asks
/// <see cref="IsEnabled"/> through the Foundation <c>victory</c> requirement leaf. Queries never change state or use
/// the RNG.</summary>
public interface IVictoryService
{
    VictoryStatus Status(Simulation sim);
    /// <summary>Whether an enabled victory has this <see cref="VictoryIdentityDef"/> id.</summary>
    bool IsEnabled(Simulation sim, string identity);
    /// <summary>Whether an enabled victory is decided by this condition.</summary>
    bool IsDecidedBy(Simulation sim, string condition);
    /// <summary>Ask every enabled victory's condition in priority order. Records and publishes the first one reached.
    /// <paramref name="turnEnd"/> is true for the check at the end of a turn, false for one an event prompts (a city
    /// captured, a project completed).</summary>
    VictoryStatus Evaluate(Simulation sim, bool turnEnd);
    /// <summary>Record a win a feature decided itself (a council's victory resolution): the first enabled victory whose
    /// condition is <paramref name="condition"/>. Changes nothing when the game is already won or no such victory is
    /// on.</summary>
    VictoryStatus Award(Simulation sim, string condition, int player);
}

/// <summary>A way to win, registered as a service by the module that runs it (the base game's conquest, domination,
/// cultural and time victories, the space race of the projects mod, a council's vote). A <see cref="VictoryDef"/>
/// names its condition by <see cref="IHasId.Id"/> in <see cref="VictoryDef.Rule"/>; content naming an unregistered
/// condition fails to load. Asking must not change state or use the RNG.</summary>
public interface IVictoryCondition : IHasId
{
    /// <summary>The player who has won this victory now, or null. A condition decided elsewhere (by a vote, through
    /// <see cref="IVictoryService.Award"/>) always answers null.</summary>
    int? Winner(Simulation sim, VictoryDef victory, IVictoryStandings standings, bool turnEnd);
}

/// <summary>The civilizations as one victory check sees them, worked out once per check and shared by every
/// condition, so ties between teams always break the same way.</summary>
public interface IVictoryStandings
{
    /// <summary>Teams with a civilization (never the barbarians), highest combined score first; ties go to the lower
    /// team id. A team's score adds its members' population, land and wonders and counts its technologies once.</summary>
    IReadOnlyList<int> Teams { get; }
    /// <summary>The team's civilizations in id order.</summary>
    IReadOnlyList<int> Members(int team);
    /// <summary>Whether a civilization still has a city or a unit.</summary>
    bool IsAlive(int player);
    /// <summary>The member who wins for the team: the highest-scoring living member, or the highest-scoring member
    /// when none lives. Ties go to the lower player id.</summary>
    int Winner(int team);
}

public sealed record VictoryAchieved(int Team, int Player, string Victory, int Turn) : DomainEvent;
