using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Ai;

/// <summary>The computer player's diplomacy (<c>ai:diplomacy</c>, in <see cref="AiPlannerOrder.Diplomacy"/>, after war
/// and civics and before settlers). Declaring war and making peace on its own are the war planner's; this planner
/// negotiates. It answers every deal offered to this player, with a counter-offer where an ordinary offer falls short,
/// and every few turns proposes deals of its own: resource swaps and sales, agreements, peace for tribute when winning a
/// war, tribute from a much weaker rival it dislikes, help from a friend, and an embargo on an enemy. Agreements with
/// another computer player whose own attitude agrees are signed at once; a human is asked through a deal instead.
/// Every answer honours the leader's refusal thresholds (<see cref="LeaderDef"/>).</summary>
internal sealed class DiplomacyPlanner(AiContext ctx)
{
    internal const int ReviewInterval = 5, ProposalsPerTurn = 2, GoldPerTurnTurns = 10;
    /// <summary>Turns before the same kind of proposal goes to the same partner again.</summary>
    internal const int RepeatTurns = 20;
    /// <summary>War score from which a winning player offers peace for tribute.</summary>
    internal const int TributeForPeaceScore = 150;
    /// <summary>Strength (percent of the target's) from which a player demands tribute in peacetime.</summary>
    internal const int TributeStrengthPercent = 200;
    /// <summary>Strength (percent of ours) from which a demand is given in to.</summary>
    internal const int YieldToStrengthPercent = 150;
    /// <summary>The base worth of peace or war continuing, before the war score is added.</summary>
    internal const int PeaceWorth = 100;
    private IDiplomacyService Diplomacy => ctx.Sim.Rules().Diplomacy;
    private int _proposed;

    public void Plan()
    {
        var diplomacy = ctx.Sim.Rules().Diplomacy;
        if (ctx.Cities.Count == 0) return;
        Answer(diplomacy);
        if ((ctx.Turn + ctx.PlayerId) % ReviewInterval != 0) return;
        Agreements(diplomacy);
        DemandPeaceTribute(diplomacy);
        ProposeSwaps(diplomacy);
        DemandTribute(diplomacy);
        AskForHelp(diplomacy);
        AskForEmbargo(diplomacy);
    }

    private IEnumerable<Player> Rivals() => ctx.Sim.State.Players
        .Where(p => p.Id != ctx.PlayerId && p.TeamId != ctx.Self.TeamId && PlayerRules.Negotiates(ctx.Sim, p.Id)
            && ctx.Sim.State.Cities.Any(c => c.Owner == p.Id) && ctx.Sim.Rules().Relations.HasMet(ctx.Sim, ctx.Self.TeamId, p.TeamId))
        .OrderBy(p => p.Id);

    /// <summary>Met rivals at peace with this player and free to trade with it.</summary>
    private IEnumerable<Player> Partners(IDiplomacyService diplomacy) => Rivals()
        .Where(p => ctx.Sim.Rules().Relations.MayTrade(ctx.Sim, ctx.Self.TeamId, p.TeamId));

    private Disposition Attitude(int toward) => Diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, toward).Tier;

    // ---- Answering ----

    private void Answer(IDiplomacyService diplomacy)
    {
        foreach (var deal in diplomacy.Deals(ctx.Sim).Where(d => d.Status == DiplomaticDealStatus.Proposed && d.Recipient == ctx.PlayerId)
            .OrderBy(d => d.Id).ToList())
        {
            if (Accepts(diplomacy, deal)) ctx.Issue(new ExecuteDealCommand { Player = ctx.PlayerId, Deal = deal.Id });
            else if (!Counter(diplomacy, deal)) ctx.Issue(new CancelDealCommand { Player = ctx.PlayerId, Deal = deal.Id });
        }
    }

    /// <summary>Whether this player would accept the deal. A demand is given in to out of fear, a request for help out of
    /// goodwill, and anything else when what it brings, less what it costs, clears a margin set by the attitude toward
    /// the proposer. Items the leader refuses at this attitude (<see cref="Refusal"/>) sink any deal, and a furious
    /// leader refuses everything but a peace it needs.</summary>
    public bool Accepts(IDiplomacyService diplomacy, DiplomaticDeal deal)
    {
        var tier = diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, deal.Proposer).Tier;
        if (Refusal(deal.Items, tier) is not null) return false;
        return deal.Intent switch
        {
            DealIntent.Demand => YieldsTo(deal),
            DealIntent.Request => Helps(deal, tier),
            _ => tier == Disposition.Furious
                ? NeedsPeace(deal) && Value(deal.Items, deal.Proposer) >= 0
                : Value(deal.Items, deal.Proposer) >= Margin(tier),
        };
    }

    public static long Margin(Disposition tier) => tier switch
    {
        Disposition.Annoyed => 60,
        Disposition.Cautious => 15,
        Disposition.Pleased => 0,
        Disposition.Friendly => -15,
        _ => long.MaxValue,
    };

    /// <summary>Why the leader would refuse one of these items whatever else the deal holds, or null. The thresholds are
    /// the leader's (<see cref="LeaderDef"/>): it refuses at or below the named attitude toward the proposer, and an
    /// embargo also when it likes the target more than its <see cref="LeaderDef.StopTradingThemRefuseAttitudeThreshold"/>.</summary>
    public string? Refusal(IReadOnlyList<DiplomaticTradeItem> items, Disposition tier)
    {
        var leader = ctx.Leader;
        foreach (var item in items)
        {
            bool giving = item.FromPlayer == ctx.PlayerId;
            switch (item.Kind)
            {
                case TradeItemKind.OpenBorders when AtOrBelow(tier, leader?.OpenBordersRefuseAttitudeThreshold):
                    return "will not open borders to you";
                case TradeItemKind.DefensivePact when AtOrBelow(tier, leader?.DefensivePactRefuseAttitudeThreshold):
                    return "will not sign a defensive pact with you";
                case TradeItemKind.Technology when giving && AtOrBelow(tier, leader?.TechRefuseAttitudeThreshold):
                    return "will not share technology with you";
                case TradeItemKind.Embargo when giving && AtOrBelow(tier, leader?.StopTradingRefuseAttitudeThreshold):
                    return "will not stop trading at your request";
                case TradeItemKind.Embargo when giving && !AtOrBelow(Attitude(item.Against), leader?.StopTradingThemRefuseAttitudeThreshold):
                    return "will not stop trading with a partner it likes";
            }
        }
        return null;
    }

    /// <summary>Whether <paramref name="tier"/> is at or below a leader threshold such as <c>"cautious"</c>. An empty
    /// threshold or <c>"none"</c> refuses nothing.</summary>
    internal static bool AtOrBelow(Disposition tier, string? threshold) => DispositionThresholds.AtOrBelow(tier, threshold);

    /// <summary>A demand is given in to when the demander is much the stronger and it asks for no more than the
    /// player can spare; a furious leader gives in to nothing.</summary>
    private bool YieldsTo(DiplomaticDeal deal)
    {
        if (Attitude(deal.Proposer) == Disposition.Furious) return false;
        if (ctx.Strength(deal.Proposer) * 100 < ctx.Strength(ctx.PlayerId) * YieldToStrengthPercent) return false;
        return -Value(deal.Items, deal.Proposer) <= Spare();
    }

    /// <summary>Help goes to a player this leader regards above its <see cref="LeaderDef.NoGiveHelpAttitudeThreshold"/>,
    /// up to what it can spare.</summary>
    private bool Helps(DiplomaticDeal deal, Disposition tier) =>
        !AtOrBelow(tier, ctx.Leader?.NoGiveHelpAttitudeThreshold ?? nameof(Disposition.Cautious)) && -Value(deal.Items, deal.Proposer) <= Spare();

    /// <summary>Gold-equivalent this player will part with for nothing: a share of the treasury above its reserve.</summary>
    private long Spare() => Math.Max(0, (ctx.Self.Treasury - ctx.GoldReserve) / 3) + 20;

    /// <summary>The deal ends a war this player wants over.</summary>
    private bool NeedsPeace(DiplomaticDeal deal) => deal.Items.Any(i => i.Kind == TradeItemKind.PeaceTreaty)
        && ctx.WantsPeace(ctx.Sim.TeamOf(deal.Proposer).Id);

    /// <summary>An ordinary offer that falls short is answered with the same offer made worth it: the proposer adds gold
    /// (or gold per turn) to cover the shortfall, or else this player's costliest items come off. A counter-offer is
    /// never countered again, and a deal this leader refuses outright gets none.</summary>
    private bool Counter(IDiplomacyService diplomacy, DiplomaticDeal deal)
    {
        if (deal.Intent != DealIntent.Trade || deal.CounterTo is not null) return false;
        var tier = diplomacy.GetAttitude(ctx.Sim, ctx.PlayerId, deal.Proposer).Tier;
        if (tier == Disposition.Furious || Refusal(deal.Items, tier) is not null) return false;
        long margin = Margin(tier);
        foreach (var items in CounterTerms(deal, margin))
        {
            if (Value(items, deal.Proposer) < margin || diplomacy.ValidateProposal(ctx.Sim, ctx.PlayerId, deal.Proposer, items, DealIntent.Trade, deal.Id) is not null) continue;
            if (ctx.Issue(new ProposeDealCommand { Player = ctx.PlayerId, Target = deal.Proposer, Items = items, CounterTo = deal.Id })) return true;
        }
        return false;
    }

    private IEnumerable<List<DiplomaticTradeItem>> CounterTerms(DiplomaticDeal deal, long margin)
    {
        var items = deal.Items.Select(i => i with { Id = 0 }).ToList();
        long shortfall = margin - Value(items, deal.Proposer);
        long gold = (shortfall + 9) / 10 * 10;
        long lump = items.Where(i => i.FromPlayer == deal.Proposer && i.Kind == TradeItemKind.Gold).Sum(i => i.Amount);
        yield return [.. items.Where(i => !(i.FromPlayer == deal.Proposer && i.Kind == TradeItemKind.Gold)),
            new(deal.Proposer, TradeItemKind.Gold, Amount: lump + gold)];
        long perTurn = items.Where(i => i.FromPlayer == deal.Proposer && i.Kind == TradeItemKind.GoldPerTurn).Sum(i => i.Amount);
        yield return [.. items.Where(i => !(i.FromPlayer == deal.Proposer && i.Kind == TradeItemKind.GoldPerTurn)),
            new(deal.Proposer, TradeItemKind.GoldPerTurn, Amount: perTurn + (shortfall + GoldPerTurnTurns - 1) / GoldPerTurnTurns)];
        var trimmed = items.ToList();
        foreach (var item in items.Where(i => i.FromPlayer == ctx.PlayerId && !i.Mutual).OrderByDescending(GiveCost).ThenBy(i => i.Kind)
            .ThenBy(i => i.ContentId, StringComparer.Ordinal))
        {
            trimmed.Remove(item);
            if (trimmed.Count == 0) yield break;
            if (Value(trimmed, deal.Proposer) >= margin) { yield return trimmed; yield break; }
        }
    }

    // ---- Valuation ----

    /// <summary>Gold-equivalent worth to this player of a set of items exchanged with <paramref name="partner"/>:
    /// received items count positive, given items negative, and treaties binding both sides by what they mean to this
    /// player.</summary>
    public long Value(IReadOnlyList<DiplomaticTradeItem> items, int partner)
    {
        long total = 0;
        foreach (var item in items)
        {
            if (item.Mutual) total += TreatyValue(item, ctx.Sim.TeamOf(partner).Id);
            else total += item.FromPlayer == ctx.PlayerId ? -GiveCost(item) : ReceiveValue(item);
        }
        return total;
    }

    private long TreatyValue(DiplomaticTradeItem item, int enemy) => item.Kind switch
    {
        // Peace is worth having when the war should end, and costs what a winning war would still bring otherwise.
        TradeItemKind.PeaceTreaty => ctx.WantsPeace(enemy)
            ? PeaceWorth + Math.Max(0, -ctx.WarScore(enemy)) : -(PeaceWorth + Math.Max(0, ctx.WarScore(enemy))),
        TradeItemKind.OpenBorders => 10,
        TradeItemKind.DefensivePact => 20,
        _ => 0,
    };

    private long ReceiveValue(DiplomaticTradeItem item) => item.Kind switch
    {
        TradeItemKind.Gold => item.Amount,
        TradeItemKind.GoldPerTurn => item.Amount * GoldPerTurnTurns,
        TradeItemKind.Technology => ctx.Team.Techs.Contains(item.ContentId) ? 0 : TechCost(item.ContentId),
        TradeItemKind.Resource => Held(item.ContentId) > 0 ? 5 * item.Amount : ResourceWorth(item.ContentId, held: 0) * item.Amount,
        TradeItemKind.Embargo => EmbargoWorth(item.Against),
        _ => 0,
    };

    private long GiveCost(DiplomaticTradeItem item) => item.Kind switch
    {
        TradeItemKind.Gold => item.Amount,
        TradeItemKind.GoldPerTurn => item.Amount * GoldPerTurnTurns,
        // Knowledge given away also strengthens a rival, so it costs more than it would be worth to receive.
        TradeItemKind.Technology => TechCost(item.ContentId) * 5 / 4,
        // A spare copy costs little; the last copy costs its whole worth, to a player holding what would be left.
        TradeItemKind.Resource => Held(item.ContentId) is var held && held > item.Amount ? 10 * item.Amount
            : ResourceWorth(item.ContentId, Math.Max(0, held - item.Amount)) * item.Amount,
        // Stopping trade costs the trade, and a little goodwill.
        TradeItemKind.Embargo => 20 + TradeWith(ctx.Sim.TeamOf(item.Against).Id),
        _ => 0,
    };

    /// <summary>What it is worth to see a third party cut off: much against an enemy or a rival this leader is furious
    /// with, something against one it is annoyed with, nothing otherwise.</summary>
    private long EmbargoWorth(int against)
    {
        if (ctx.Team.Wars.Contains(ctx.Sim.TeamOf(against).Id)) return 60;
        return Attitude(against) switch { Disposition.Furious => 60, Disposition.Annoyed => 30, _ => 0 };
    }

    /// <summary>The worth, over the binding turns, of what this player's live deals bring it from a team.</summary>
    private long TradeWith(int team) => Diplomacy.Deals(ctx.Sim)
        .Where(d => d.Status == DiplomaticDealStatus.Active && (d.Proposer == ctx.PlayerId || d.Recipient == ctx.PlayerId))
        .Where(d => ctx.Sim.TeamOf(d.Proposer == ctx.PlayerId ? d.Recipient : d.Proposer).Id == team)
        .SelectMany(d => d.Items).Where(i => i.FromPlayer != ctx.PlayerId && !i.Mutual && i.Kind != TradeItemKind.Embargo)
        .Sum(ReceiveValue);

    private long TechCost(string tech) => ctx.Sim.Rules().Research
        ?.CalculateCost(ctx.Sim, ctx.Team.Id, tech).Cost ?? 0;

    /// <summary>Gold-equivalent worth of one more copy of <paramref name="bonus"/> to this player, which holds
    /// <paramref name="held"/> copies without it: its happiness and health, and what the computer player's resource
    /// valuers add (<see cref="ResourceSubject"/>, whose points this planner takes as gold). One valuer registration so
    /// answers both the plots the AI settles and improves and the resources it trades.</summary>
    public long ResourceWorth(string bonus, long held)
    {
        var def = ctx.Sim.Content.Get<BonusDef>(bonus);
        return 30 + 15 * Math.Max(0, def.Happiness + def.Health) + Math.Max(0, def.AiTradeModifier) / 5
            + ctx.Adjustment(new ResourceSubject(def, held, Plot: null));
    }

    /// <summary>Copies the empire holds for trade at its capital, imports included.</summary>
    private long Held(string bonus) => Held(ctx.PlayerId, bonus);

    private long Held(int player, string bonus) => Capital(player) is { } city
        ? ResourceAccess.TradeCount(ctx.Sim, player, city.Id, bonus) : 0;

    private City? Capital(int player) => ctx.Sim.State.Cities.Where(c => c.Owner == player)
        .OrderByDescending(c => c.Buildings.Any(b => ctx.Sim.Content.Get<BuildingDef>(b).Capital)).ThenBy(c => c.Id).FirstOrDefault();

    /// <summary>Gold per turn asked for a spare copy. Over the deal's ten binding turns the buyer pays about half the
    /// copy's worth to it, so a buyer that values resources the same way gains from the trade.</summary>
    public long SalePrice(string bonus) => Math.Max(1, ResourceWorth(bonus, held: 0) / (2 * GoldPerTurnTurns));

    // ---- Proposing ----

    private bool CanPropose => _proposed < ProposalsPerTurn;

    private bool Propose(int target, List<DiplomaticTradeItem> items, DealIntent intent = DealIntent.Trade)
    {
        if (!CanPropose || !ctx.Issue(new ProposeDealCommand { Player = ctx.PlayerId, Target = target, Items = items, Intent = intent })) return false;
        _proposed++;
        return true;
    }

    /// <summary>Whether this player has a proposal to <paramref name="partner"/> waiting, or proposed one like it lately.</summary>
    private bool Pending(IDiplomacyService diplomacy, int partner, Func<DiplomaticDeal, bool>? like = null) => diplomacy.Deals(ctx.Sim)
        .Any(d => d.Proposer == ctx.PlayerId && d.Recipient == partner
            && (d.Status == DiplomaticDealStatus.Proposed || like is not null && like(d) && ctx.Turn - d.ProposedTurn < RepeatTurns));

    /// <summary>One resource for one resource, a spare copy of ours for one they can spare and we lack, or, when they have
    /// nothing to spare, the copy for gold per turn. A partner this leader likes may receive the spare copy as a gift.</summary>
    private void ProposeSwaps(IDiplomacyService diplomacy)
    {
        var bonuses = ctx.Sim.Content.Get<BonusDef>().All.Where(b => b.Happiness + b.Health > 0).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
        var spare = bonuses.Where(b => Held(b.Id) >= 2).ToArray();
        var wanted = bonuses.Where(b => Held(b.Id) == 0).OrderByDescending(b => ResourceWorth(b.Id, held: 0)).ToArray();
        if (spare.Length == 0) return;
        foreach (var partner in Partners(diplomacy))
        {
            if (!CanPropose) return;
            if (Pending(diplomacy, partner.Id) || Attitude(partner.Id) == Disposition.Furious) continue;
            var get = wanted.FirstOrDefault(b => Held(partner.Id, b.Id) >= 2);
            var give = spare.FirstOrDefault(b => Held(partner.Id, b.Id) == 0);
            if (give is null) continue;
            // A swap when both sides have something to spare; otherwise sell the spare copy for gold per turn.
            var items = get is not null
                ? new List<DiplomaticTradeItem> { new(ctx.PlayerId, TradeItemKind.Resource, give.Id), new(partner.Id, TradeItemKind.Resource, get.Id) }
                : new List<DiplomaticTradeItem> { new(ctx.PlayerId, TradeItemKind.Resource, give.Id), new(partner.Id, TradeItemKind.GoldPerTurn, Amount: SalePrice(give.Id)) };
            if (Value(items, partner.Id) > 0 && Propose(partner.Id, items)) continue;
            // Without gold trading, a spare copy goes to a partner this leader likes: a running deal builds goodwill.
            if (Attitude(partner.Id) >= Disposition.Pleased)
                Propose(partner.Id, [new(ctx.PlayerId, TradeItemKind.Resource, give.Id)]);
        }
    }

    /// <summary>Open borders at Pleased, a defensive pact at Friendly, unless the leader's thresholds refuse them. With
    /// a computer player whose own attitude agrees they are signed at once; a human is asked through a deal.</summary>
    private void Agreements(IDiplomacyService diplomacy)
    {
        foreach (var partner in Partners(diplomacy))
        {
            var mine = Attitude(partner.Id);
            bool border = mine >= Disposition.Pleased && !AtOrBelow(mine, ctx.Leader?.OpenBordersRefuseAttitudeThreshold)
                && !diplomacy.HasOpenBorders(ctx.Sim, ctx.Self.TeamId, partner.TeamId);
            bool pact = mine >= Disposition.Friendly && !AtOrBelow(mine, ctx.Leader?.DefensivePactRefuseAttitudeThreshold)
                && !diplomacy.HasDefensivePact(ctx.Sim, ctx.Self.TeamId, partner.TeamId);
            if (!border && !pact) continue;
            if (partner.Ai is not null)
            {
                var mutual = (Disposition)Math.Min((int)mine, (int)diplomacy.GetAttitude(ctx.Sim, partner.Id, ctx.PlayerId).Tier);
                if (border && mutual >= Disposition.Pleased) ctx.Issue(new SignOpenBordersCommand { Player = ctx.PlayerId, Target = partner.Id });
                if (pact && mutual >= Disposition.Friendly) ctx.Issue(new SignDefensivePactCommand { Player = ctx.PlayerId, Target = partner.Id });
                continue;
            }
            if (Pending(diplomacy, partner.Id, d => d.Items.Any(i => i.Kind is TradeItemKind.OpenBorders or TradeItemKind.DefensivePact))) continue;
            var items = new List<DiplomaticTradeItem>();
            if (border) items.Add(new(ctx.PlayerId, TradeItemKind.OpenBorders));
            if (pact) items.Add(new(ctx.PlayerId, TradeItemKind.DefensivePact));
            if (diplomacy.ValidateProposal(ctx.Sim, ctx.PlayerId, partner.Id, items) is null) Propose(partner.Id, items);
        }
    }

    /// <summary>A player winning a war (score at least <see cref="TributeForPeaceScore"/>, and not wanting peace on its
    /// own account) offers peace for tribute: gold, or else a technology the loser knows and it lacks.</summary>
    private void DemandPeaceTribute(IDiplomacyService diplomacy)
    {
        foreach (var enemy in Rivals().Where(p => ctx.Team.Wars.Contains(p.TeamId)).GroupBy(p => p.TeamId).Select(g => g.First()))
        {
            if (!CanPropose) return;
            int score = ctx.WarScore(enemy.TeamId);
            if (score < TributeForPeaceScore || ctx.WantsPeace(enemy.TeamId)) continue;
            if (Pending(diplomacy, enemy.Id, d => d.Items.Any(i => i.Kind == TradeItemKind.PeaceTreaty))) continue;
            foreach (var tribute in Tribute(enemy, Math.Min(enemy.Treasury / 2, 20 + score / 2)))
            {
                var items = new List<DiplomaticTradeItem> { new(ctx.PlayerId, TradeItemKind.PeaceTreaty), tribute };
                if (diplomacy.ValidateProposal(ctx.Sim, ctx.PlayerId, enemy.Id, items) is null && Propose(enemy.Id, items)) break;
            }
        }
    }

    /// <summary>In peacetime, a player at least twice as strong as a rival it regards at or below its
    /// <see cref="LeaderDef.DemandTributeAttitudeThreshold"/> demands tribute, unless that rival still remembers its last
    /// demand. A refusal may move it to prepare a war (<see cref="LeaderDef.DemandRebukedWarProbability"/>).</summary>
    private void DemandTribute(IDiplomacyService diplomacy)
    {
        Rebuked(diplomacy);
        foreach (var rival in Partners(diplomacy))
        {
            if (!CanPropose) return;
            if (!AtOrBelow(Attitude(rival.Id), ctx.Leader?.DemandTributeAttitudeThreshold)) continue;
            if (ctx.Strength(ctx.PlayerId) * 100 < ctx.Strength(rival.Id) * TributeStrengthPercent) continue;
            if (ctx.Sim.Rules().Relations.Memories(ctx.Sim, rival.Id, ctx.PlayerId).GetValueOrDefault(DiplomacyMemories.MadeDemand) > 0) continue;
            if (Pending(diplomacy, rival.Id, d => d.Intent == DealIntent.Demand)) continue;
            foreach (var tribute in Tribute(rival, rival.Treasury / 3))
                if (diplomacy.ValidateProposal(ctx.Sim, ctx.PlayerId, rival.Id, [tribute], DealIntent.Demand) is null
                    && Propose(rival.Id, [tribute], DealIntent.Demand)) break;
        }
    }

    /// <summary>A demand of ours refused since the last review may move this leader to prepare a war on the refuser.</summary>
    private void Rebuked(IDiplomacyService diplomacy)
    {
        int chance = ctx.Leader?.DemandRebukedWarProbability ?? 0;
        if (chance <= 0 || ctx.Team.Wars.Count > 0) return;
        var refused = diplomacy.Deals(ctx.Sim).Where(d => d.Proposer == ctx.PlayerId && d.Intent == DealIntent.Demand
            && d.Status == DiplomaticDealStatus.Cancelled && ctx.Turn - d.ProposedTurn <= ReviewInterval).OrderBy(d => d.Id).FirstOrDefault();
        if (refused is null || ctx.Sim.Rng.Next(100) >= chance) return;
        ctx.Issue(new PrepareWarCommand { Player = ctx.PlayerId, Target = refused.Recipient });
    }

    /// <summary>A player at war with another civilization, or short of gold, asks a rival that likes it (Pleased or
    /// better toward it) for gold or a technology, unless that rival refused it lately.</summary>
    private void AskForHelp(IDiplomacyService diplomacy)
    {
        bool war = ctx.Team.Wars.Any(t => ctx.Sim.State.TeamById(t).Members.Any(m => PlayerRules.IsNation(ctx.Sim, m)));
        if (!war && ctx.Self.Treasury >= ctx.GoldReserve / 2) return;
        foreach (var friend in Partners(diplomacy))
        {
            if (!CanPropose) return;
            if (diplomacy.GetAttitude(ctx.Sim, friend.Id, ctx.PlayerId).Tier < Disposition.Pleased) continue;
            if (ctx.Sim.Rules().Relations.Memories(ctx.Sim, ctx.PlayerId, friend.Id).GetValueOrDefault(DiplomacyMemories.RefusedHelp) > 0) continue;
            if (Pending(diplomacy, friend.Id, d => d.Intent == DealIntent.Request)) continue;
            foreach (var help in Tribute(friend, friend.Treasury / 5))
                if (diplomacy.ValidateProposal(ctx.Sim, ctx.PlayerId, friend.Id, [help], DealIntent.Request) is null
                    && Propose(friend.Id, [help], DealIntent.Request)) return;
        }
    }

    /// <summary>At war, a player asks a rival that likes it to stop trading with the enemy.</summary>
    private void AskForEmbargo(IDiplomacyService diplomacy)
    {
        var enemy = Rivals().Where(p => ctx.Team.Wars.Contains(p.TeamId)).OrderBy(p => p.Id).FirstOrDefault();
        if (enemy is null) return;
        foreach (var friend in Partners(diplomacy))
        {
            if (!CanPropose) return;
            if (diplomacy.GetAttitude(ctx.Sim, friend.Id, ctx.PlayerId).Tier < Disposition.Pleased) continue;
            if (Pending(diplomacy, friend.Id, d => d.Items.Any(i => i.Kind == TradeItemKind.Embargo))) continue;
            List<DiplomaticTradeItem> items = [new(friend.Id, TradeItemKind.Embargo, Against: enemy.Id)];
            if (diplomacy.ValidateProposal(ctx.Sim, ctx.PlayerId, friend.Id, items) is null && Propose(friend.Id, items)) return;
        }
    }

    /// <summary>What to ask of <paramref name="from"/>, best first: up to <paramref name="gold"/> gold (in tens), then the
    /// cheapest technology it knows that this player's team lacks.</summary>
    private IEnumerable<DiplomaticTradeItem> Tribute(Player from, long gold)
    {
        long amount = gold / 10 * 10;
        if (amount >= 10) yield return new(from.Id, TradeItemKind.Gold, Amount: amount);
        var tech = ctx.Sim.TeamOf(from.Id).Techs.Where(t => !ctx.Team.Techs.Contains(t))
            .OrderBy(TechCost).ThenBy(t => t, StringComparer.Ordinal).FirstOrDefault();
        if (tech is not null) yield return new(from.Id, TradeItemKind.Technology, tech);
    }
}

/// <summary>The computer player's answer to a deal, for the diplomacy screen: the same rule its planner applies on its
/// own turn, asked of an observing context so nothing is issued.</summary>
internal sealed class DealAppraiser : IDealAppraiser
{
    public DealAppraisal Appraise(Simulation sim, int leader, int proposer, IReadOnlyList<DiplomaticTradeItem> items, DealIntent intent)
    {
        var diplomacy = sim.Rules().Diplomacy;
        var planner = new DiplomacyPlanner(AiContext.Observe(sim, leader));
        var deal = new DiplomaticDeal { Proposer = proposer, Recipient = leader, Items = items.ToList(), Intent = intent };
        var tier = diplomacy.GetAttitude(sim, leader, proposer).Tier;
        return new(planner.Value(items, proposer), DiplomacyPlanner.Margin(tier), planner.Accepts(diplomacy, deal), planner.Refusal(items, tier));
    }
}
