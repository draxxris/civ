using Ariel.Game;

namespace Ariel.Rules.Diplomacy;

/// <summary>What deals leave in the base game's memory ledger. The kinds, their decay and the gift limit are data
/// (<c>data/memory-kind.json</c>).</summary>
internal sealed partial class DiplomacyService
{
    private static void Remember(Simulation sim, int from, int toward, string kind) => Relations(sim).Remember(sim, from, toward, kind);

    /// <summary>A recipient remembers each technology a deal gave it, and a deal that gave without taking as help, up to
    /// the kind's limit so a stream of token gifts buys no unbounded goodwill. Tribute is not help: a demand given in to
    /// is remembered by the demander as such, and by the giver as a demand made of it. An embargo laid on request is
    /// remembered by its own rule.</summary>
    private static void RememberDeal(Simulation sim, DiplomaticDeal deal)
    {
        int Other(int player) => player == deal.Proposer ? deal.Recipient : deal.Proposer;
        foreach (var item in deal.Items.Where(i => i.Kind == TradeItemKind.Technology))
            Remember(sim, Other(item.FromPlayer), item.FromPlayer, DiplomacyMemories.TradedTechToUs);
        if (deal.Intent == DealIntent.Demand)
        {
            Remember(sim, deal.Proposer, deal.Recipient, DiplomacyMemories.AcceptDemand);
            Remember(sim, deal.Recipient, deal.Proposer, DiplomacyMemories.MadeDemand);
            return;
        }
        var given = deal.Items.Where(i => !i.Mutual && i.Kind != TradeItemKind.Embargo).ToList();
        if (given.Count > 0 && given.Count == deal.Items.Count && given.Select(i => i.FromPlayer).Distinct().Count() == 1)
        {
            int giver = given[0].FromPlayer;
            Remember(sim, Other(giver), giver, DiplomacyMemories.GiveHelp);
        }
    }

    /// <summary>A recipient's refusal of a demand, a request for help or a request to stop trading with someone is
    /// remembered by the proposer; a demand is also remembered by the one it was made of. Declining an ordinary offer
    /// is not remembered.</summary>
    private static void RememberRefusal(Simulation sim, DiplomaticDeal deal)
    {
        if (deal.Intent == DealIntent.Demand)
        {
            Remember(sim, deal.Proposer, deal.Recipient, DiplomacyMemories.RejectedDemand);
            Remember(sim, deal.Recipient, deal.Proposer, DiplomacyMemories.MadeDemand);
        }
        else if (deal.Intent == DealIntent.Request) Remember(sim, deal.Proposer, deal.Recipient, DiplomacyMemories.RefusedHelp);
        if (deal.Items.Any(i => i.Kind == TradeItemKind.Embargo && i.FromPlayer == deal.Recipient))
            Remember(sim, deal.Proposer, deal.Recipient, DiplomacyMemories.DeniedStopTrading);
    }

    /// <summary>The asker remembers the embargo as a favour; every member of the target team remembers who stopped
    /// trading with it and who paid for that.</summary>
    private static void RememberEmbargo(Simulation sim, int giver, int asker, int targetTeam)
    {
        Remember(sim, asker, giver, DiplomacyMemories.AcceptedStopTrading);
        foreach (var member in sim.State.Players.Where(p => p.TeamId == targetTeam).OrderBy(p => p.Id))
        {
            Remember(sim, member.Id, giver, DiplomacyMemories.StoppedTrading);
            Remember(sim, member.Id, asker, DiplomacyMemories.HiredTradeEmbargo);
        }
    }
    /// <summary>The other participant remembers a player who stopped an active trade.</summary>
    private static void RememberStoppedTrading(Simulation sim, DiplomaticDeal deal, int player) =>
        Remember(sim, player == deal.Proposer ? deal.Recipient : deal.Proposer, player, DiplomacyMemories.StoppedTrading);
}
