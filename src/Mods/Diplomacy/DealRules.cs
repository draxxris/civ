using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Diplomacy;

internal sealed partial class DiplomacyService
{
    private static bool Ongoing(DiplomaticTradeItem item) => item.Kind is TradeItemKind.Resource or TradeItemKind.GoldPerTurn;
    private static bool Live(Simulation sim, DiplomaticDeal deal) => deal.Status == DiplomaticDealStatus.Active &&
        !sim.TeamOf(deal.Proposer).Wars.Contains(sim.TeamOf(deal.Recipient).Id);
    private static DiplomaticDeal? Deal(Simulation sim, int id) => Read(sim).Deals.FirstOrDefault(d => d.Id == id);
    public IReadOnlyList<DiplomaticDeal> Deals(Simulation sim) => Read(sim).Deals.OrderBy(d => d.Id).Select(d => new DiplomaticDeal
    {
        Id = d.Id, Proposer = d.Proposer, Recipient = d.Recipient, StartedTurn = d.StartedTurn,
        LastSettledTurn = d.LastSettledTurn, Status = d.Status, Items = d.Items.ToList(),
    }).ToArray();
    public string? ValidateProposal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        if (!HasMet(sim, sim.TeamOf(player).Id, sim.TeamOf(target).Id)) return "teams have not met";
        if (sim.TeamOf(player).Wars.Contains(sim.TeamOf(target).Id)) return "teams are at war";
        if (items.Count is 0 or > 64) return "deal requires between one and 64 items";
        int ta = sim.TeamOf(player).Id, tb = sim.TeamOf(target).Id;
        if (items.Any(i => i.Kind is TradeItemKind.Gold or TradeItemKind.GoldPerTurn) && TradeTech(sim, ta, tb, t => t.GoldTrading, "gold trading") is { } goldError) return goldError;
        if (items.Any(i => i.Kind == TradeItemKind.Technology) && TradeTech(sim, ta, tb, t => t.TechTrading, "technology trading") is { } techError) return techError;
        foreach (var item in items)
        {
            if (item.FromPlayer != player && item.FromPlayer != target) return "item giver is not a participant";
            if (!Enum.IsDefined(item.Kind) || item.Amount <= 0 || item.Amount > int.MaxValue) return "invalid trade item amount or kind";
            var itemError = ValidateItem(sim, item, item.FromPlayer == player ? target : player);
            if (itemError is not null) return itemError;
        }
        return ValidateAssets(sim, items);
    }
    private static string? ValidateItem(Simulation sim, DiplomaticTradeItem item, int recipient)
    {
        if (item.Kind == TradeItemKind.Resource)
            return sim.Content.TryGet<S.BonusDef>(item.ContentId, out _) ? null : "unknown resource";
        if (item.Kind != TradeItemKind.Technology) return item.ContentId.Length == 0 ? null : "gold items cannot specify content";
        if (item.Amount != 1 || !sim.Content.TryGet<S.TechDef>(item.ContentId, out var tech) || tech.Disable || tech.Repeat) return "technology is not tradable";
        if (!sim.KnowsTech(item.FromPlayer, item.ContentId)) return "giver does not know technology";
        return sim.KnowsTech(recipient, item.ContentId) ? "recipient already knows technology" : null;
    }
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
        var network = sim.Content.Services.Get<IResourceNetwork>().SingleOrDefault();
        var connections = sim.Content.Services.Get<ICityConnectionService>().SingleOrDefault();
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
        return root is not null && (root.Id == city.Id || sim.Content.Services.Get<ICityConnectionService>().SingleOrDefault()?.Connected(sim, root.Id, city.Id) == true);
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
    public int ProposeDeal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items)
    {
        var state = Edit(sim);
        int id = state.NextDealId++;
        state.Deals.Add(new() { Id = id, Proposer = player, Recipient = target, Items = items.ToList(), Status = DiplomaticDealStatus.Proposed });
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
        d.StartedTurn = sim.State.Turn;
        d.Status = d.Items.Any(Ongoing) ? DiplomaticDealStatus.Active : DiplomaticDealStatus.Completed;
        TransferGold(sim, d, TradeItemKind.Gold);
        foreach (var item in d.Items.Where(i => i.Kind == TradeItemKind.Technology))
        {
            var recipient = sim.TeamOf(item.FromPlayer == d.Proposer ? d.Recipient : d.Proposer);
            if (recipient.Techs.Add(item.ContentId)) sim.Events.Publish(new TechDiscovered(recipient.Id, item.ContentId));
        }
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
    public void CancelDeal(Simulation sim, int deal) => Deal(sim, deal)!.Status = DiplomaticDealStatus.Cancelled;
    public long ResourceBalance(Simulation sim, int player, string resource)
    {
        long balance = 0;
        foreach (var d in Read(sim).Deals.Where(d => Live(sim, d) && (d.Proposer == player || d.Recipient == player)))
            foreach (var i in d.Items.Where(i => i.Kind == TradeItemKind.Resource && i.ContentId == resource)) balance += i.FromPlayer == player ? -i.Amount : i.Amount;
        return balance;
    }
    /// <summary>The city's own network copies, capped by what exports leave in its group, plus imports when the city is linked to the capital.</summary>
    public long AvailableResourceCount(Simulation sim, int player, int city, string resource)
    {
        var target = sim.State.FindCity(city);
        if (target is null || target.Owner != player) return 0;
        long own = sim.Content.Services.Get<IResourceNetwork>().SingleOrDefault()?.ConnectedCount(sim, player, city, resource) ?? 0;
        long physical = Exports(sim, player, resource) == 0 ? own : Math.Min(own, Remaining(sim, player, target, resource));
        long imports = Imports(sim, player, resource);
        return Math.Max(0, physical + (imports > 0 && ConnectedToRoot(sim, player, target) ? imports : 0));
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
            if (!Live(sim, d) || !CanSettle(sim, d))
            {
                d.Status = DiplomaticDealStatus.Cancelled;
                continue;
            }
            TransferGold(sim, d, TradeItemKind.GoldPerTurn);
            d.LastSettledTurn = sim.State.Turn;
        }
    }
}
