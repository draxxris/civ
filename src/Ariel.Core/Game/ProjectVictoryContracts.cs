using Ariel.Content;

namespace Ariel.Game;

/// <summary>Score parts for one player. Total is the weighted sum.</summary>
public sealed record PlayerScore(int Population, int Land, int Techs, int Wonders, int Total);
/// <summary>Saved game-wide outcome. Ids are -1 while the game is running.</summary>
public sealed class VictoryGameState
{
    public bool IsGameOver { get; set; }
    public int WinningTeamId { get; set; } = -1;
    public int WinningPlayerId { get; set; } = -1;
    public string? VictoryId { get; set; }
    public int TurnWon { get; set; }
}
/// <summary>Read-only view of the outcome plus the effective turn limit.</summary>
public sealed record VictoryStatus(bool IsGameOver, int WinningTeamId, int WinningPlayerId, string? VictoryId, int TurnWon, int MaxTurns);
/// <summary>Saved per-team project counts.</summary>
public sealed class TeamProjectState
{
    public SortedDictionary<string, int> Completed { get; set; } = new(StringComparer.Ordinal);
}
/// <summary>Queries and rules for projects and victory. Queries never change state.</summary>
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
/// <summary>Completion rules for a queue kind that Cities does not own.</summary>
public interface IProductionCompletion
{
    string Kind { get; }
    void Complete(Simulation sim, int city, string content);
}
public sealed record ProjectCompleted(int City, int Player, int Team, string Project) : DomainEvent;
public sealed record VictoryAchieved(int Team, int Player, string Victory, int Turn) : DomainEvent;
