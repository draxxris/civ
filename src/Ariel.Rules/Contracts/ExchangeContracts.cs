namespace Ariel.Game;

/// <summary>Running exchanges between players (the Diplomacy mod's deals): resources one player sends another and
/// gold paid every turn. Base rules ask through <see cref="GameRules.Exchanges"/>; without such a mod nothing is
/// exchanged, a city uses the copies its network connects, and no gold changes hands.</summary>
public interface IPlayerExchanges
{
    /// <summary>Resources named in the player's running exchanges, in either direction. Counts of any other resource
    /// are the connected copies alone.</summary>
    IReadOnlyCollection<string> ExchangedResources(Simulation sim, int player);
    /// <summary>Copies the city can use: its connected copies less what exports take from its group, plus imports when
    /// it is linked to the capital. Never below zero.</summary>
    long AvailableResourceCount(Simulation sim, int player, int city, string resource);
    /// <summary>The same answer for several resources, reusing one connectivity query.</summary>
    IReadOnlyDictionary<string, long> AvailableResourceCounts(Simulation sim, int player, int city, IReadOnlyCollection<string> resources);
    /// <summary>Gold per turn the player expects to receive and to pay, the settlement of which is the mod's own.</summary>
    (long Income, long Expenses) GoldPerTurn(Simulation sim, int player);
}
