using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.ProjectsVictory;

/// <summary>Saved per-team project counts.</summary>
public sealed class TeamProjectState
{
    public SortedDictionary<string, int> Completed { get; set; } = new(StringComparer.Ordinal);
}
/// <summary>Queries and rules for team and world projects. Queries never change state. Score and the game's outcome
/// are the base game's (<see cref="GameRules.Scores"/>, <see cref="GameRules.Victory"/>); this mod adds the space race
/// to them as the victory condition <see cref="ProjectVictories.Spaceship"/>.</summary>
public interface IProjectService
{
    RequirementResult ValidateProject(Simulation sim, int city, string project);
    int TeamCount(Simulation sim, int team, string project);
    int WorldCount(Simulation sim, string project);
    /// <summary>Add a completed project to the city's team, publish ProjectCompleted and check for a victory.</summary>
    void CompleteProject(Simulation sim, int city, string project);
}
/// <summary>The victory conditions this mod registers.</summary>
public static class ProjectVictories
{
    /// <summary>Won by the first team to complete every spaceship project linked to the victory.</summary>
    public const string Spaceship = "projects_victory:spaceship";
}
public sealed record ProjectCompleted(int City, int Player, int Team, string Project) : DomainEvent;
