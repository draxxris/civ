namespace Ariel.Game;

/// <summary>Rules shared by every mod that asks what a player is. The barbarian faction is not a civilization.</summary>
public static class PlayerRules
{
    /// <summary>True for the barbarian player, whose units include rebels. They trade, research, vote and rebel differently or not at all.</summary>
    public static bool IsBarbarian(Simulation sim, int player) => BarbarianPlayer(sim) == player;

    /// <summary>The one barbarian player, or null before the faction first appears.</summary>
    public static int? BarbarianPlayer(Simulation sim) =>
        sim.State.Extensions.TryGet<MilitaryGameState>(sim.Content)?.BarbarianPlayer;
}
