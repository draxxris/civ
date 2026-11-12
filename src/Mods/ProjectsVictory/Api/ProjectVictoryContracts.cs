using System.Text.Json.Serialization;
using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.ProjectsVictory;

/// <summary>Score parts for one player. Total is the weighted sum.</summary>
public sealed record PlayerScore(int Population, int Land, int Techs, int Wonders, int Total);
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
/// <summary>Read-only view of the outcome plus the effective turn limit.</summary>
public sealed record VictoryStatus(bool IsGameOver, int WinningTeamId, int WinningPlayerId, string? VictoryId, int TurnWon, int MaxTurns);
/// <summary>Saved per-team project counts.</summary>
public sealed class TeamProjectState
{
    public SortedDictionary<string, int> Completed { get; set; } = new(StringComparer.Ordinal);
}
/// <summary>Queries and rules for projects and victory. Queries never change state. The mod also keeps the score the
/// base game asks for (<see cref="IPlayerScores"/>): a player's score is <see cref="PlayerScore.Total"/>.</summary>
public interface IProjectVictoryService
{
    RequirementResult ValidateProject(Simulation sim, int city, string project);
    int TeamCount(Simulation sim, int team, string project);
    int WorldCount(Simulation sim, string project);
    /// <summary>Add a completed project to the city's team and publish ProjectCompleted.</summary>
    void CompleteProject(Simulation sim, int city, string project);
    PlayerScore GetScore(Simulation sim, int playerId);
    VictoryStatus GetVictoryStatus(Simulation sim);
    /// <summary>Check every enabled victory in priority order. Records and publishes the first one reached.</summary>
    VictoryStatus Evaluate(Simulation sim, bool turnEnd);
}
public sealed record ProjectCompleted(int City, int Player, int Team, string Project) : DomainEvent;
public sealed record VictoryAchieved(int Team, int Player, string Victory, int Turn) : DomainEvent;
