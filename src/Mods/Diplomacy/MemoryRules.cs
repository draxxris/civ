using Ariel.Game;

namespace Ariel.Mods.Diplomacy;

/// <summary>What deals leave in the base game's memory ledger. The kinds, their decay and the gift limit are data
/// (<c>data/memory-kind.json</c>).</summary>
internal sealed partial class DiplomacyService
{
    private static void Remember(Simulation sim, int from, int toward, string kind) => Relations(sim).Remember(sim, from, toward, kind);

    /// <summary>A recipient remembers each technology a deal gave it, and a deal that gave without taking as help, up to
    /// the kind's limit so a stream of token gifts buys no unbounded goodwill.</summary>
    private static void RememberDeal(Simulation sim, DiplomaticDeal deal)
    {
        int Other(int player) => player == deal.Proposer ? deal.Recipient : deal.Proposer;
        foreach (var item in deal.Items.Where(i => i.Kind == TradeItemKind.Technology))
            Remember(sim, Other(item.FromPlayer), item.FromPlayer, DiplomacyMemories.TradedTechToUs);
        if (deal.Items.Count > 0 && deal.Items.Select(i => i.FromPlayer).Distinct().Count() == 1)
        {
            int giver = deal.Items[0].FromPlayer;
            Remember(sim, Other(giver), giver, DiplomacyMemories.GiveHelp);
        }
    }
    /// <summary>The other participant remembers a player who stopped an active trade.</summary>
    private static void RememberStoppedTrading(Simulation sim, DiplomaticDeal deal, int player) =>
        Remember(sim, player == deal.Proposer ? deal.Recipient : deal.Proposer, player, DiplomacyMemories.StoppedTrading);
}
