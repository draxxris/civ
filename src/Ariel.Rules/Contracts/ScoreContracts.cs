namespace Ariel.Game;

/// <summary>A player's score, answered by the feature that keeps score, or null without one
/// (<see cref="GameRules.Scores"/>). Research diffusion helps players who trail the score leader. Queries never
/// change state or use the RNG.</summary>
public interface IPlayerScores
{
    /// <summary>The player's total score.</summary>
    int Score(Simulation sim, int player);
}

/// <summary>Which victories this game can be won by, answered by the feature that runs victory conditions, or null
/// without one (<see cref="GameRules.VictoryConditions"/>). Content that belongs to one victory (the council buildings
/// to the diplomatic victory) asks it through the Foundation <c>victory</c> requirement leaf, which fails without an
/// answer. Queries never change state or use the RNG.</summary>
public interface IVictoryConditions
{
    /// <summary>Whether a victory with this <see cref="Ariel.Content.Schema.VictoryIdentityDef"/> id is on.</summary>
    bool IsEnabled(Simulation sim, string identity);
}
