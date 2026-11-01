using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Answers deals offered to this player, proposes resource swaps, and signs agreements with friendly
/// computer players. Agreements apply at once for both sides, so they are only signed with another computer player
/// whose own attitude agrees; a human is never signed up without asking. Deals are proposals, which anyone may answer.</summary>
internal sealed class DiplomacyPlanner(AiContext ctx)
{
    internal const int ReviewInterval = 5, ProposalsPerTurn = 1, GoldPerTurnTurns = 10;
    private IDiplomacyService? Diplomacy => ctx.Sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();

    public void Plan()
    {
        if (Diplomacy is not { } diplomacy || ctx.Cities.Count == 0) return;
        Answer(diplomacy);
        if ((ctx.Turn + ctx.PlayerId) % ReviewInterval != 0) return;
        Agreements(diplomacy);
        ProposeSwaps(diplomacy);
    }

    private IEnumerable<Player> Partners(IDiplomacyService diplomacy) => ctx.Sim.State.Players
        .Where(p => p.Id != ctx.PlayerId && p.TeamId != ctx.Self.TeamId && !WarRules.IsBarbarian(ctx.Sim, p.Id)
            && ctx.Sim.State.Cities.Any(c => c.Owner == p.Id)
            && diplomacy.HasMet(ctx.Sim, ctx.Self.TeamId, p.TeamId) && !ctx.Team.Wars.Contains(p.TeamId))
        .OrderBy(p => p.Id);

    private void Answer(IDiplomacyService diplomacy)
    {
        foreach (var deal in diplomacy.Deals(ctx.Sim).Where(d => d.Status == DiplomaticDealStatus.Proposed && d.Recipient == ctx.PlayerId)
            .OrderBy(d => d.Id).ToList())
        {
            if (Accepts(diplomacy, deal)) ctx.Issue(new ExecuteDealCommand { Player = ctx.PlayerId, Deal = deal.Id });
            else ctx.Issue(new CancelDealCommand { Player = ctx.PlayerId, Deal = deal.Id });
        }
    }

    /// <summary>A deal passes when what it brings, less what it costs, clears a margin set by the attitude toward the proposer.
    /// A furious leader refuses everything.</summary>
    public bool Accepts(IDiplomacyService diplomacy, DiplomaticDeal deal)
    {
        var tier = diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, deal.Proposer).Tier;
        if (tier == AttitudeTier.Furious) return false;
        return Value(diplomacy, deal.Items) >= Margin(tier);
    }

    public static long Margin(AttitudeTier tier) => tier switch
    {
        AttitudeTier.Annoyed => 60,
        AttitudeTier.Cautious => 15,
        AttitudeTier.Pleased => 0,
        AttitudeTier.Friendly => -15,
        _ => long.MaxValue,
    };

    /// <summary>Gold-equivalent worth of a set of items to this player: received items count positive, given items negative.</summary>
    public long Value(IDiplomacyService diplomacy, IReadOnlyList<DiplomaticTradeItem> items)
    {
        long total = 0;
        foreach (var item in items)
        {
            bool giving = item.FromPlayer == ctx.PlayerId;
            total += giving ? -GiveCost(diplomacy, item) : ReceiveValue(diplomacy, item);
        }
        return total;
    }

    private long ReceiveValue(IDiplomacyService diplomacy, DiplomaticTradeItem item) => item.Kind switch
    {
        TradeItemKind.Gold => item.Amount,
        TradeItemKind.GoldPerTurn => item.Amount * GoldPerTurnTurns,
        TradeItemKind.Technology => ctx.Team.Techs.Contains(item.ContentId) ? 0 : TechCost(item.ContentId),
        TradeItemKind.Resource => Held(diplomacy, item.ContentId) > 0 ? 5 * item.Amount : ResourceWorth(item.ContentId) * item.Amount,
        _ => 0,
    };

    private long GiveCost(IDiplomacyService diplomacy, DiplomaticTradeItem item) => item.Kind switch
    {
        TradeItemKind.Gold => item.Amount,
        TradeItemKind.GoldPerTurn => item.Amount * GoldPerTurnTurns,
        // Knowledge given away also strengthens a rival, so it costs more than it would be worth to receive.
        TradeItemKind.Technology => TechCost(item.ContentId) * 5 / 4,
        // A spare copy costs little; the last copy costs its whole worth.
        TradeItemKind.Resource => Held(diplomacy, item.ContentId) > item.Amount ? 10 * item.Amount : ResourceWorth(item.ContentId) * item.Amount,
        _ => 0,
    };

    private long TechCost(string tech) => ctx.Sim.Content.Services.Get<IResearchService>().SingleOrDefault()
        ?.CalculateCost(ctx.Sim, ctx.Team.Id, tech).Cost ?? 0;

    public long ResourceWorth(string bonus)
    {
        var def = ctx.Sim.Content.Get<BonusDef>(bonus);
        return 30 + 15 * Math.Max(0, def.Happiness + def.Health) + Math.Max(0, def.AiTradeModifier) / 5;
    }

    /// <summary>Copies the empire can use at its capital, imports included.</summary>
    private long Held(IDiplomacyService diplomacy, string bonus) => Held(diplomacy, ctx.PlayerId, bonus);

    private long Held(IDiplomacyService diplomacy, int player, string bonus) => Capital(player) is { } city
        ? diplomacy.AvailableResourceCount(ctx.Sim, player, city.Id, bonus) : 0;

    private City? Capital(int player) => ctx.Sim.State.Cities.Where(c => c.Owner == player)
        .OrderByDescending(c => c.Buildings.Any(b => ctx.Sim.Content.Get<BuildingDef>(b).Capital)).ThenBy(c => c.Id).FirstOrDefault();

    /// <summary>Gold per turn asked for a spare copy. Over the deal's ten binding turns the buyer pays about half the
    /// copy's worth to it, so a buyer that values resources the same way gains from the trade.</summary>
    public long SalePrice(string bonus) => Math.Max(1, ResourceWorth(bonus) / (2 * GoldPerTurnTurns));

    /// <summary>One resource for one resource, a spare copy of ours for one they can spare and we lack, or, when they have
    /// nothing to spare, the copy for gold per turn. A partner this leader likes may receive the spare copy as a gift.</summary>
    private void ProposeSwaps(IDiplomacyService diplomacy)
    {
        var pending = diplomacy.Deals(ctx.Sim).Where(d => d.Status == DiplomaticDealStatus.Proposed && d.Proposer == ctx.PlayerId)
            .Select(d => d.Recipient).ToHashSet();
        var bonuses = ctx.Sim.Content.Get<BonusDef>().All.Where(b => b.Happiness + b.Health > 0).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
        var spare = bonuses.Where(b => Held(diplomacy, b.Id) >= 2).ToArray();
        var wanted = bonuses.Where(b => Held(diplomacy, b.Id) == 0).OrderByDescending(b => ResourceWorth(b.Id)).ToArray();
        if (spare.Length == 0) return;
        int proposed = 0;
        foreach (var partner in Partners(diplomacy))
        {
            if (proposed >= ProposalsPerTurn) return;
            if (pending.Contains(partner.Id) || diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, partner.Id).Tier == AttitudeTier.Furious) continue;
            var get = wanted.FirstOrDefault(b => Held(diplomacy, partner.Id, b.Id) >= 2);
            var give = spare.FirstOrDefault(b => Held(diplomacy, partner.Id, b.Id) == 0);
            if (give is null) continue;
            // A swap when both sides have something to spare; otherwise sell the spare copy for gold per turn.
            var items = get is not null
                ? new List<DiplomaticTradeItem> { new(ctx.PlayerId, TradeItemKind.Resource, give.Id), new(partner.Id, TradeItemKind.Resource, get.Id) }
                : new List<DiplomaticTradeItem> { new(ctx.PlayerId, TradeItemKind.Resource, give.Id), new(partner.Id, TradeItemKind.GoldPerTurn, Amount: SalePrice(give.Id)) };
            if (Value(diplomacy, items) > 0 && ctx.Issue(new ProposeDealCommand { Player = ctx.PlayerId, Target = partner.Id, Items = items }))
            { proposed++; continue; }
            // Without gold trading, a spare copy goes to a partner this leader likes: a running deal builds goodwill.
            if (diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, partner.Id).Tier >= AttitudeTier.Pleased
                && ctx.Issue(new ProposeDealCommand { Player = ctx.PlayerId, Target = partner.Id,
                    Items = [new(ctx.PlayerId, TradeItemKind.Resource, give.Id)] }))
                proposed++;
        }
    }

    /// <summary>Open borders at Pleased, a defensive pact at Friendly, both ways, with computer players only.</summary>
    private void Agreements(IDiplomacyService diplomacy)
    {
        foreach (var partner in Partners(diplomacy).Where(p => p.Ai is not null))
        {
            var mine = diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, partner.Id).Tier;
            var theirs = diplomacy.GetAttitude(ctx.Sim, partner.Id, ctx.PlayerId).Tier;
            var mutual = (AttitudeTier)Math.Min((int)mine, (int)theirs);
            if (mutual >= AttitudeTier.Pleased && !diplomacy.HasOpenBorders(ctx.Sim, ctx.Self.TeamId, partner.TeamId))
                ctx.Issue(new SignOpenBordersCommand { Player = ctx.PlayerId, Target = partner.Id });
            if (mutual >= AttitudeTier.Friendly && !diplomacy.HasDefensivePact(ctx.Sim, ctx.Self.TeamId, partner.TeamId))
                ctx.Issue(new SignDefensivePactCommand { Player = ctx.PlayerId, Target = partner.Id });
        }
    }
}
