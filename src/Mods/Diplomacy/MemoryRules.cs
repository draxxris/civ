using Ariel.Game;

namespace Ariel.Mods.Diplomacy;

/// <summary>Records what each player remembers about another. Barbarians and teammates remember nothing of each other.</summary>
internal sealed partial class DiplomacyService
{
    private static void Remember(Simulation sim, int from, int toward, string memory, int limit = int.MaxValue)
    {
        if (sim.TeamOf(from).Id == sim.TeamOf(toward).Id || PlayerRules.IsBarbarian(sim, from) || PlayerRules.IsBarbarian(sim, toward)) return;
        var history = Edit(sim).GetOrCreateHistory(from, toward);
        if (history.Memories.GetValueOrDefault(memory) < limit) history.RecordMemory(memory);
    }
    private static IEnumerable<int> Members(Simulation sim, int team) =>
        sim.State.Players.Where(p => p.TeamId == team).OrderBy(p => p.Id).Select(p => p.Id);

    /// <summary>Players outside both teams who are at least Pleased with someone on the defending team, read before the
    /// war changes any attitude.</summary>
    private int[] FriendsOf(Simulation sim, int aggressor, int defender) => sim.State.Players
        .Where(p => p.TeamId != aggressor && p.TeamId != defender && HasMet(sim, p.TeamId, aggressor)
            && Members(sim, defender).Any(d => GetAttitude(sim, p.Id, d).Tier >= AttitudeTier.Pleased))
        .OrderBy(p => p.Id).Select(p => p.Id).ToArray();
    private static void RememberWar(Simulation sim, int aggressor, int defender, int[] friends)
    {
        foreach (int attacker in Members(sim, aggressor))
        {
            foreach (int victim in Members(sim, defender)) Remember(sim, victim, attacker, DiplomaticMemories.DeclaredWar);
            foreach (int friend in friends) Remember(sim, friend, attacker, DiplomaticMemories.DeclaredWarOnFriend);
        }
    }
    /// <summary>The player it was captured from remembers a razed city; followers of a religion remember its holy city.</summary>
    internal static void RememberRaze(Simulation sim, CityRazed razed)
    {
        if (razed.CapturedFrom is { } victim && sim.State.Players.Any(p => p.Id == victim))
            Remember(sim, victim, razed.Player, DiplomaticMemories.RazedCity);
        foreach (string religion in razed.HolyCityOf ?? [])
            foreach (var player in sim.State.Players.OrderBy(p => p.Id).Where(p => Religion(sim, p.Id) == religion))
                Remember(sim, player.Id, razed.Player, DiplomaticMemories.RazedHolyCity);
    }
    internal static void RememberSpy(Simulation sim, EspionageMissionResult result)
    {
        if (!result.Success && sim.State.FindCity(result.City) is { } city) Remember(sim, city.Owner, result.Player, DiplomaticMemories.SpyCaught);
    }
    /// <summary>A recipient remembers each technology a deal gave it, and a deal that gave without taking as help, up to
    /// the rules' limit so a stream of token gifts buys no unbounded goodwill.</summary>
    private static void RememberDeal(Simulation sim, DiplomaticDeal deal)
    {
        int Other(int player) => player == deal.Proposer ? deal.Recipient : deal.Proposer;
        foreach (var item in deal.Items.Where(i => i.Kind == TradeItemKind.Technology))
            Remember(sim, Other(item.FromPlayer), item.FromPlayer, DiplomaticMemories.TradedTechToUs);
        if (deal.Items.Count > 0 && deal.Items.Select(i => i.FromPlayer).Distinct().Count() == 1)
        {
            int giver = deal.Items[0].FromPlayer;
            Remember(sim, Other(giver), giver, DiplomaticMemories.GiveHelp, sim.Content.Get<DiplomacyRulesDef>().All.Single().GiveHelpMemoryLimit);
        }
    }
    /// <summary>The other participant remembers a player who stopped an active trade.</summary>
    private static void RememberStoppedTrading(Simulation sim, DiplomaticDeal deal, int player) =>
        Remember(sim, player == deal.Proposer ? deal.Recipient : deal.Proposer, player, DiplomaticMemories.StoppedTrading);
}
