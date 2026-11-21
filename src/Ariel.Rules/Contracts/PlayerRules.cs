using Ariel.Content.Schema;

namespace Ariel.Game;

/// <summary>Rules shared by every mod that asks what a player is. A player is a nation unless its civilization carries
/// a <see cref="FactionDef"/>; a faction's restrictions are that row's data, so a rule asks for the restriction it
/// cares about (<see cref="Negotiates"/>, <see cref="AlwaysAtWar"/>, <see cref="IsHandicapFoe"/>) or whether the
/// player is a nation at all, never which faction it is. Each query is one dictionary lookup on frozen content: no
/// allocation, and safe from parallel scoring.</summary>
public static class PlayerRules
{
    /// <summary>The faction row of the player's civilization, or null for a nation.</summary>
    public static FactionDef? Faction(Simulation sim, int player) =>
        sim.Content.Attachment<FactionDef>(sim.State.PlayerById(player).Civilization);

    /// <summary>True for an ordinary civilization: it keeps an economy and a score, founds religions, sits on councils
    /// and can win. False for a faction's player (barbarians and their rebels, for one).</summary>
    public static bool IsNation(Simulation sim, int player) => Faction(sim, player) is null;

    /// <summary>Whether the player meets others, deals and is remembered. Nations always do.</summary>
    public static bool Negotiates(Simulation sim, int player) => Faction(sim, player)?.Negotiates ?? true;

    /// <summary>Whether the player is at war with every other team and can never make peace.</summary>
    public static bool AlwaysAtWar(Simulation sim, int player) => Faction(sim, player)?.AlwaysAtWar == true;

    /// <summary>Whether the difficulty level's rules against untamed enemies apply to fights with the player.</summary>
    public static bool IsHandicapFoe(Simulation sim, int player) => Faction(sim, player)?.HandicapFoe == true;

    /// <summary>Whether any member of the team negotiates; a team of factions that do not is never met.</summary>
    public static bool TeamNegotiates(Simulation sim, int team)
    {
        foreach (int member in sim.State.TeamById(team).Members)
            if (!Negotiates(sim, member)) return false;
        return true;
    }

    /// <summary>Whether any member of the team is always at war.</summary>
    public static bool TeamAlwaysAtWar(Simulation sim, int team)
    {
        foreach (int member in sim.State.TeamById(team).Members)
            if (AlwaysAtWar(sim, member)) return true;
        return false;
    }

    /// <summary>True while the player still has a city or a unit; a player with neither is out of the game.</summary>
    public static bool IsAlive(Simulation sim, int player) =>
        sim.State.Cities.Any(c => c.Owner == player) || sim.State.Units.Any(u => u.Owner == player);

    /// <summary>The player a faction plays as, or null before it first appears. A faction has one player for the whole
    /// game; it is found by its civilization, the lowest id first.</summary>
    public static int? FactionPlayer(Simulation sim, FactionDef faction)
    {
        foreach (var player in sim.State.Players)
            if (player.Civilization == faction.Target) return player.Id;
        return null;
    }

    /// <summary>The faction that fields uprisings (hostile villagers, rebels), or null when no loaded faction does.</summary>
    public static FactionDef? UprisingFaction(Simulation sim)
    {
        FactionDef? found = null;
        foreach (var faction in sim.Content.Get<FactionDef>().All)
            if (faction.Uprisings && (found is null || string.CompareOrdinal(faction.Id, found.Id) < 0)) found = faction;
        return found;
    }
}
