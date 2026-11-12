namespace Ariel.Game;

/// <summary>A player's score, answered by the feature that keeps score, or null without one
/// (<see cref="GameRules.Scores"/>). Research diffusion helps players who trail the score leader. Queries never
/// change state or use the RNG.</summary>
public interface IPlayerScores
{
    /// <summary>The player's total score.</summary>
    int Score(Simulation sim, int player);
}
