using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Diplomacy;

internal sealed partial class DiplomacyService
{
    private static bool Ongoing(DiplomaticTradeItem item) => item.Kind is TradeItemKind.Resource or TradeItemKind.GoldPerTurn or TradeItemKind.Embargo;
    private static bool Live(Simulation sim, DiplomaticDeal deal) => deal.Status == DiplomaticDealStatus.Active &&
        !sim.TeamOf(deal.Proposer).Wars.Contains(sim.TeamOf(deal.Recipient).Id);
    private static DiplomaticDeal? Deal(Simulation sim, int id) => Read(sim).Deals.FirstOrDefault(d => d.Id == id);
    public IReadOnlyList<DiplomaticDeal> Deals(Simulation sim) => Read(sim).Deals.OrderBy(d => d.Id).Select(d => new DiplomaticDeal
    {
        Id = d.Id, Proposer = d.Proposer, Recipient = d.Recipient, StartedTurn = d.StartedTurn,
        LastSettledTurn = d.LastSettledTurn, Status = d.Status, Items = d.Items.ToList(),
        ProposedTurn = d.ProposedTurn, Intent = d.Intent, CounterTo = d.CounterTo,
    }).ToArray();
    public string? ValidateProposal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items,
        DealIntent intent = DealIntent.Trade, int? counterTo = null)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (!HasMet(sim, a, b)) return "teams have not met";
        if (items.Count is 0 or > 64) return "deal requires between one and 64 items";
        if (!Enum.IsDefined(intent)) return "unknown deal intent";
        if (intent != DealIntent.Trade && items.Any(i => i.FromPlayer != target || i.Mutual))
            return "a demand or a request for help asks only for what the other side gives";
        if (counterTo is { } answered && !Answers(Deal(sim, answered), player, target))
            return "a counter-offer answers a proposal the other side made to you";
        var peace = items.Count(i => i.Kind == TradeItemKind.PeaceTreaty);
        if (peace > 1) return "duplicate peace treaty";
        if (AtWar(sim, a, b))
        {
            if (peace == 0) return "teams are at war";
            if (Relations(sim).ValidatePeace(sim, player, target) is { } peaceError) return peaceError;
            if (items.Any(i => i.Kind is TradeItemKind.OpenBorders or TradeItemKind.DefensivePact)) return "agreements are signed after peace";
        }
        else
        {
            if (peace > 0) return "teams are not at war";
            if (!Relations(sim).MayTrade(sim, a, b)) return "an embargo forbids trade between these teams";
        }
        var unlockError = ValidateTradeUnlocks(sim, a, b, items);
        if (unlockError is not null) return unlockError;
        return ValidateItems(sim, player, target, items) ?? ValidateAssets(sim, items);
    }
    private static bool Answers(DiplomaticDeal? original, int player, int target) =>
        original is { Status: DiplomaticDealStatus.Proposed } && original.Proposer == target && original.Recipient == player;
    private static string? ValidateTradeUnlocks(Simulation sim, int teamA, int teamB, IReadOnlyList<DiplomaticTradeItem> items)
    {
        bool technology = items.Any(i => i.Kind == TradeItemKind.Technology);
        if (technology && sim.IsOptionEnabled("foundation:gameoption_no_tech_trading")) return "technology trading is disabled";
        if (items.Any(i => i.Kind is TradeItemKind.Gold or TradeItemKind.GoldPerTurn) &&
            TradeTech(sim, teamA, teamB, t => t.GoldTrading, "gold trading") is { } goldError) return goldError;
        return technology ? TradeTech(sim, teamA, teamB, t => t.TechTrading, "technology trading") : null;
    }
    private static string? ValidateItems(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items)
    {
        foreach (var item in items)
        {
            if (item.FromPlayer != player && item.FromPlayer != target) return "item giver is not a participant";
            if (!Enum.IsDefined(item.Kind) || item.Amount <= 0 || item.Amount > int.MaxValue) return "invalid trade item amount or kind";
            if (item.Kind != TradeItemKind.Embargo && item.Against != -1) return "only an embargo names a third party";
            var itemError = ValidateItem(sim, item, item.FromPlayer == player ? target : player);
            if (itemError is not null) return itemError;
        }
        foreach (var kind in new[] { TradeItemKind.OpenBorders, TradeItemKind.DefensivePact })
            if (items.Count(i => i.Kind == kind) > 1) return "duplicate agreement";
        return null;
    }
    private static string? ValidateItem(Simulation sim, DiplomaticTradeItem item, int recipient)
    {
        if (item.Mutual)
        {
            if (item.Amount != 1 || item.ContentId.Length != 0) return "a treaty carries no content or amount";
            return item.Kind switch
            {
                TradeItemKind.OpenBorders => AgreementError(sim, item.FromPlayer, recipient, DiplomaticAgreement.OpenBorders),
                TradeItemKind.DefensivePact => AgreementError(sim, item.FromPlayer, recipient, DiplomaticAgreement.DefensivePact),
                _ => null,
            };
        }
        if (item.Kind == TradeItemKind.Embargo) return ValidateEmbargo(sim, item, recipient);
        if (item.Kind == TradeItemKind.Resource)
            return sim.Content.TryGet<S.BonusDef>(item.ContentId, out _) ? null : "unknown resource";
        if (item.Kind != TradeItemKind.Technology) return item.ContentId.Length == 0 ? null : "gold items cannot specify content";
        if (item.Amount != 1 || !sim.Content.TryGet<S.TechDef>(item.ContentId, out var tech) || tech.Disable || tech.Repeat) return "technology is not tradable";
        if (!sim.KnowsTech(item.FromPlayer, item.ContentId)) return "giver does not know technology";
        return sim.KnowsTech(recipient, item.ContentId) ? "recipient already knows technology" : null;
    }
    /// <summary>An embargo names a living civilization outside both participants' teams that the giver's team has met
    /// and does not already embargo. Giving or taking it takes the same technology as trading gold.</summary>
    private static string? ValidateEmbargo(Simulation sim, DiplomaticTradeItem item, int recipient)
    {
        if (item.Amount != 1 || item.ContentId.Length != 0) return "an embargo carries no content or amount";
        int against = item.Against;
        if (!sim.State.Players.Any(p => p.Id == against) || PlayerRules.IsBarbarian(sim, against)) return "unknown embargo target";
        int giver = sim.TeamOf(item.FromPlayer).Id, target = sim.TeamOf(against).Id;
        if (target == giver || target == sim.TeamOf(recipient).Id) return "an embargo targets a third team";
        if (!sim.State.Cities.Any(c => sim.TeamOf(c.Owner).Id == target)) return "embargo target has no cities";
        if (!HasMet(sim, giver, target)) return "giver has not met the embargo target";
        if (EmbargoBy(sim, giver, target)) return "embargo already in force";
        return TradeTech(sim, giver, sim.TeamOf(recipient).Id, t => t.GoldTrading, "embargoes");
    }
    /// <summary>A live deal has the first team embargo the second.</summary>
    private static bool EmbargoBy(Simulation sim, int team, int target) => Read(sim).Deals.Any(d => Live(sim, d)
        && d.Items.Any(i => i.Kind == TradeItemKind.Embargo && sim.TeamOf(i.FromPlayer).Id == team && sim.TeamOf(i.Against).Id == target));
    public bool HasEmbargo(Simulation sim, int teamA, int teamB) => EmbargoBy(sim, teamA, teamB) || EmbargoBy(sim, teamB, teamA);
    // The base game's question about trade terms: an embargo forbids trade both ways, and deals grant no free trade.
    public bool Embargoed(Simulation sim, int teamA, int teamB) => HasEmbargo(sim, teamA, teamB);
    public bool FreeTrade(Simulation sim, int teamA, int teamB) => false;
    /// <summary>One connected group of the player's cities, counted once.</summary>
    private sealed class CityGroup(City representative, long copies)
    {
        public City Representative { get; } = representative;
        public long Copies { get; private set; } = copies;
        public List<City> Members { get; } = [representative];
        /// <summary>Adds a city that the peaceful-passage connection links to the group. Members may sit on different tiles, so the largest count stands in for the group.</summary>
        public void Add(City city, long count) { Members.Add(city); Copies = Math.Max(Copies, count); }
    }
    /// <summary>The player's connected city groups. The capital's group is last, so exports drain the other groups first.</summary>
    private static List<CityGroup> Groups(Simulation sim, int player, string resource)
    {
        var network = sim.Rules().Resources;
        var connections = sim.Rules().CityConnections;
        if (network is null || connections is null) return [];
        var groups = new List<CityGroup>();
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id))
        {
            long count = network.ConnectedCount(sim, player, city.Id, resource);
            var group = groups.FirstOrDefault(g => connections.Connected(sim, g.Representative.Id, city.Id));
            if (group is null) groups.Add(new(city, count));
            else group.Add(city, count);
        }
        var capital = Root(sim, player);
        return groups.OrderBy(g => capital is not null && g.Members.Contains(capital) ? 1 : 0).ToList();
    }
    /// <summary>Distinct physical copies across all of the player's networks. Cities that share a network count once.</summary>
    private static long PhysicalCopies(Simulation sim, int player, string resource) => Groups(sim, player, resource).Sum(g => g.Copies);
    /// <summary>Imports arrive from the other side of each live deal the player takes part in.</summary>
    private static long Imports(Simulation sim, int player, string resource) => Read(sim).Deals
        .Where(d => Live(sim, d) && (d.Proposer == player || d.Recipient == player)).SelectMany(d => d.Items)
        .Where(i => i.FromPlayer != player && i.Kind == TradeItemKind.Resource && i.ContentId == resource).Sum(i => i.Amount);
    /// <summary>The capital, or the oldest city when the player has no capital building.</summary>
    private static City? Root(Simulation sim, int player)
    {
        var cities = sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id).ToArray();
        return cities.FirstOrDefault(c => c.Buildings.Any(b => sim.Content.Get<S.BuildingDef>(b).Capital)) ?? cities.FirstOrDefault();
    }
    private static bool ConnectedToRoot(Simulation sim, int player, City city)
    {
        var root = Root(sim, player);
        return root is not null && (root.Id == city.Id || sim.Rules().CityConnections?.Connected(sim, root.Id, city.Id) == true);
    }
    private static long Exports(Simulation sim, int player, string resource, int excludedDeal = 0) => Read(sim).Deals
        .Where(d => d.Id != excludedDeal && Live(sim, d)).SelectMany(d => d.Items)
        .Where(i => i.FromPlayer == player && i.Kind == TradeItemKind.Resource && i.ContentId == resource).Sum(i => i.Amount);
    private static string? ValidateAssets(Simulation sim, IReadOnlyList<DiplomaticTradeItem> items, int excludedDeal = 0)
    {
        foreach (var group in items.Where(i => i.Kind is TradeItemKind.Gold or TradeItemKind.GoldPerTurn).GroupBy(i => i.FromPlayer))
            if (sim.State.PlayerById(group.Key).Treasury < group.Sum(i => i.Amount)) return "insufficient gold";
        foreach (var group in items.Where(i => i.Kind == TradeItemKind.Resource).GroupBy(i => (i.FromPlayer, i.ContentId)))
            if (PhysicalCopies(sim, group.Key.FromPlayer, group.Key.ContentId) - Exports(sim, group.Key.FromPlayer, group.Key.ContentId, excludedDeal) < group.Sum(i => i.Amount)) return "insufficient unreserved resource copies";
        if (items.Where(i => i.Kind == TradeItemKind.Technology).GroupBy(i => (sim.TeamOf(i.FromPlayer).Id, i.ContentId)).Any(g => g.Count() > 1)) return "duplicate technology item";
        return null;
    }
    /// <summary>A counter-offer replaces the proposal it answers, which is withdrawn without being remembered as refused.</summary>
    public int ProposeDeal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items,
        DealIntent intent = DealIntent.Trade, int? counterTo = null)
    {
        var state = Edit(sim);
        int id = state.NextDealId++;
        int nextItemId = 1;
        var canonicalItems = items.Select(i => i.Id > 0 ? i : i with { Id = nextItemId++ }).ToList();
        if (counterTo is { } answered) Deal(sim, answered)!.Status = DiplomaticDealStatus.Cancelled;
        state.Deals.Add(new()
        {
            Id = id, Proposer = player, Recipient = target, Items = canonicalItems, Status = DiplomaticDealStatus.Proposed,
            ProposedTurn = sim.State.Turn, Intent = intent, CounterTo = counterTo,
        });
        return id;
    }
    public string? ValidateExecution(Simulation sim, int player, int deal)
    {
        var d = Deal(sim, deal);
        if (d is null || d.Status != DiplomaticDealStatus.Proposed) return "unknown or non-proposed deal";
        if (player != d.Recipient) return "only the recipient can accept a deal";
        return ValidateProposal(sim, d.Proposer, d.Recipient, d.Items);
    }
    public void ExecuteDeal(Simulation sim, int deal)
    {
        var d = Deal(sim, deal)!;
        // Peace first: the rest of the deal is between teams at peace.
        if (d.Items.Any(i => i.Kind == TradeItemKind.PeaceTreaty)) Relations(sim).MakePeace(sim, d.Proposer, d.Recipient);
        d.StartedTurn = sim.State.Turn;
        d.Status = d.Items.Any(Ongoing) ? DiplomaticDealStatus.Active : DiplomaticDealStatus.Completed;
        TransferGold(sim, d, TradeItemKind.Gold);
        foreach (var item in d.Items.Where(i => i.Kind is TradeItemKind.OpenBorders or TradeItemKind.DefensivePact))
            Sign(sim, d.Proposer, d.Recipient,
                item.Kind == TradeItemKind.OpenBorders ? DiplomaticAgreement.OpenBorders : DiplomaticAgreement.DefensivePact);
        foreach (var item in d.Items.Where(i => i.Kind == TradeItemKind.Embargo)) LayEmbargo(sim, d, item);
        foreach (var item in d.Items.Where(i => i.Kind == TradeItemKind.Technology))
        {
            var recipient = sim.TeamOf(item.FromPlayer == d.Proposer ? d.Recipient : d.Proposer);
            if (recipient.Techs.Add(item.ContentId)) sim.Events.Publish(new TechDiscovered(recipient.Id, item.ContentId));
        }
        RememberDeal(sim, d);
    }
    private static void TransferGold(Simulation sim, DiplomaticDeal deal, TradeItemKind kind)
    {
        foreach (var item in deal.Items.Where(i => i.Kind == kind))
        {
            var giver = sim.State.PlayerById(item.FromPlayer);
            var receiver = sim.State.PlayerById(item.FromPlayer == deal.Proposer ? deal.Recipient : deal.Proposer);
            giver.Treasury -= item.Amount; receiver.Treasury += item.Amount;
        }
    }
    public string? ValidateCancellation(Simulation sim, int player, int deal)
    {
        var d = Deal(sim, deal);
        if (d is null || d.Status is DiplomaticDealStatus.Completed or DiplomaticDealStatus.Cancelled) return "unknown or finished deal";
        if (player != d.Proposer && player != d.Recipient) return "not a deal participant";
        return d.Status == DiplomaticDealStatus.Active && sim.State.Turn - d.StartedTurn < BindingTurns ? "deal is binding for ten turns" : null;
    }
    /// <summary>Withdrawing a proposal is not remembered. Declining one is when it was a demand, a request for help or a
    /// request to stop trading with someone; stopping an active trade always is.</summary>
    public void CancelDeal(Simulation sim, int player, int deal)
    {
        var d = Deal(sim, deal)!;
        if (d.Status == DiplomaticDealStatus.Active) RememberStoppedTrading(sim, d, player);
        else if (player == d.Recipient) RememberRefusal(sim, d);
        d.Status = DiplomaticDealStatus.Cancelled;
    }
    /// <summary>The giver's team stops trading with the target's: every deal between them, running or proposed, ends,
    /// and the target remembers both who stopped and who asked for it.</summary>
    private static void LayEmbargo(Simulation sim, DiplomaticDeal deal, DiplomaticTradeItem item)
    {
        int giver = sim.TeamOf(item.FromPlayer).Id, target = sim.TeamOf(item.Against).Id;
        int asker = item.FromPlayer == deal.Proposer ? deal.Recipient : deal.Proposer;
        foreach (var other in Read(sim).Deals.Where(o => o.Id != deal.Id && o.Status is DiplomaticDealStatus.Active or DiplomaticDealStatus.Proposed))
            if (Pair(sim.TeamOf(other.Proposer).Id, sim.TeamOf(other.Recipient).Id) == Pair(giver, target))
                other.Status = DiplomaticDealStatus.Cancelled;
        RememberEmbargo(sim, item.FromPlayer, asker, target);
    }
    public string? ValidateCancelTradeItem(Simulation sim, int player, int dealId, int itemId)
    {
        var d = Deal(sim, dealId);
        if (d is null || d.Status is DiplomaticDealStatus.Completed or DiplomaticDealStatus.Cancelled) return "unknown or finished deal";
        if (player != d.Proposer && player != d.Recipient) return "not a deal participant";
        if (d.Status != DiplomaticDealStatus.Active) return "deal is not active";
        if (sim.State.Turn - d.StartedTurn < BindingTurns) return "deal is binding for ten turns";
        var item = d.FindItem(itemId);
        if (item is null) return "unknown trade item";
        if (!Ongoing(item)) return "only ongoing items can be cancelled individually";
        return null;
    }
    public void CancelTradeItem(Simulation sim, int player, int dealId, int itemId)
    {
        if (ValidateCancelTradeItem(sim, player, dealId, itemId) is { } error)
            throw new InvalidOperationException(error);
        var d = Deal(sim, dealId)!;
        d.RemoveItem(itemId);
        RememberStoppedTrading(sim, d, player);
        if (!d.Items.Any(Ongoing))
            d.Status = DiplomaticDealStatus.Completed;
        sim.Events.Publish(new TradeItemCancelled(dealId, itemId, player));
    }
    /// <summary>Resources in the player's active deals, whether or not a war suspends them: their counts need the full
    /// answer of <see cref="AvailableResourceCount"/>.</summary>
    public IReadOnlyCollection<string> ExchangedResources(Simulation sim, int player) => Read(sim).Deals
        .Where(d => d.Status == DiplomaticDealStatus.Active && (d.Proposer == player || d.Recipient == player))
        .SelectMany(d => d.Items).Where(i => i.Kind == TradeItemKind.Resource).Select(i => i.ContentId).ToHashSet(StringComparer.Ordinal);
    /// <summary>Gold per turn the player's live deals promise; <see cref="SettleDeals"/> pays it after every player's
    /// Gold phase.</summary>
    public (long Income, long Expenses) GoldPerTurn(Simulation sim, int player)
    {
        long income = 0, expenses = 0;
        foreach (var deal in Read(sim).Deals.OrderBy(d => d.Id))
        {
            if (deal.Status != DiplomaticDealStatus.Active) continue;
            if (deal.Proposer != player && deal.Recipient != player) continue;
            if (sim.TeamOf(deal.Proposer).Wars.Contains(sim.TeamOf(deal.Recipient).Id)) continue;
            foreach (var item in deal.Items.Where(i => i.Kind == TradeItemKind.GoldPerTurn))
            {
                if (item.FromPlayer == player) expenses += item.Amount;
                else income += item.Amount;
            }
        }
        return (income, expenses);
    }
    public long ResourceBalance(Simulation sim, int player, string resource)
    {
        long balance = 0;
        foreach (var d in Read(sim).Deals.Where(d => Live(sim, d) && (d.Proposer == player || d.Recipient == player)))
            foreach (var i in d.Items.Where(i => i.Kind == TradeItemKind.Resource && i.ContentId == resource)) balance += i.FromPlayer == player ? -i.Amount : i.Amount;
        return balance;
    }
    /// <summary>The city's own network copies, capped by what exports leave in its group, plus imports when the city is linked to the capital.</summary>
    public long AvailableResourceCount(Simulation sim, int player, int city, string resource) =>
        AvailableResourceCounts(sim, player, city, [resource]).GetValueOrDefault(resource);
    public IReadOnlyDictionary<string, long> AvailableResourceCounts(Simulation sim, int player, int city, IReadOnlyCollection<string> resources)
    {
        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var target = sim.State.FindCity(city);
        if (target is null || target.Owner != player) return result;
        // One connectivity query covers every resource; only the export/import arithmetic is per resource.
        var connected = sim.Rules().Resources?.ConnectedCounts(sim, player, city)
            ?? new Dictionary<string, int>();
        bool rooted = false;
        foreach (var resource in resources.Distinct(StringComparer.Ordinal))
        {
            long own = connected.GetValueOrDefault(resource);
            long physical = Exports(sim, player, resource) == 0 ? own : Math.Min(own, Remaining(sim, player, target, resource));
            long imports = Imports(sim, player, resource);
            if (imports == 0) { result[resource] = physical; continue; }
            if (!rooted) rooted = ConnectedToRoot(sim, player, target);
            result[resource] = rooted ? physical + imports : physical;
        }
        return result;
    }
    /// <summary>Exports leave the empire pool in group order. Returns the copies that stay in the target's group.</summary>
    private static long Remaining(Simulation sim, int player, City target, string resource)
    {
        long pending = Exports(sim, player, resource);
        foreach (var group in Groups(sim, player, resource))
        {
            long taken = Math.Min(group.Copies, pending);
            pending -= taken;
            if (group.Members.Contains(target)) return group.Copies - taken;
        }
        return 0;
    }
    private static bool CanSettle(Simulation sim, DiplomaticDeal deal)
    {
        var resources = deal.Items.Where(i => i.Kind == TradeItemKind.Resource).ToArray();
        if (ValidateAssets(sim, resources, deal.Id) is not null) return false;
        var gold = deal.Items.Where(i => i.Kind == TradeItemKind.GoldPerTurn).ToArray();
        // Both sides transfer together. Incoming gold can fund the other side of this deal.
        foreach (int player in new[] { deal.Proposer, deal.Recipient })
        {
            long net = gold.Sum(i => i.FromPlayer == player ? -i.Amount : i.Amount);
            if (sim.State.PlayerById(player).Treasury + net < 0) return false;
        }
        return true;
    }
    internal void SettleDeals(Simulation sim)
    {
        foreach (var d in Read(sim).Deals.Where(d => d.Status == DiplomaticDealStatus.Active).OrderBy(d => d.Id))
        {
            if (d.LastSettledTurn == sim.State.Turn) continue;
            // Terms laid since the deal was signed (an embargo, a council's sanctions) end it like a war does.
            if (!Live(sim, d) || !CanSettle(sim, d)
                || !Relations(sim).MayTrade(sim, sim.TeamOf(d.Proposer).Id, sim.TeamOf(d.Recipient).Id))
            {
                d.Status = DiplomaticDealStatus.Cancelled;
                continue;
            }
            TransferGold(sim, d, TradeItemKind.GoldPerTurn);
            d.LastSettledTurn = sim.State.Turn;
        }
    }
}
