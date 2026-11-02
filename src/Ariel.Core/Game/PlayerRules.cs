namespace Ariel.Game;

/// <summary>Rules shared by every mod that asks what a player is. Barbarians and rebels are factions, not civilizations.</summary>
public static class PlayerRules
{
    /// <summary>True for barbarians and rebel factions. They trade, research, vote and rebel differently or not at all.</summary>
    public static bool IsBarbarian(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true;
}
