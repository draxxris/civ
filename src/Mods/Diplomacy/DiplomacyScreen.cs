using System.Globalization;
using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Diplomacy;

/// <summary>
/// Diplomacy with every rival the local team has met: attitude and its reasons, treaties and the actions against them
/// (war and peace are the base game's commands), the open and active deals, and a deal composer that shows a computer
/// player's answer before the proposal is sent. Local state: <c>partner</c> (a player id), and <c>give</c> and
/// <c>get</c>, the items of the deal being composed (see <see cref="DealDraft"/>).
/// </summary>
internal sealed class DiplomacyScreen : IGameScreen
{
    public string Id => "diplomacy:screen";
    public int Order => 40;

    public ScreenButton? Button(ScreenContext context) => context.Sim.Service<IDiplomacyService>() is null ? null
        : new("Diplomacy", "Rival leaders, attitudes, treaties and deals");

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        if (sim.Service<IDiplomacyService>() is not { } service)
            return new("Diplomacy", [new Paragraph("off", "Diplomacy is not part of this ruleset.", Tone.Dim)]);
        var rivals = MetRivals(context);
        var items = new List<ViewElement>();
        if (rivals.Count == 0) items.Add(new Paragraph("none", "You have not met any other leader yet.", Tone.Dim));
        foreach (var rival in rivals) items.Add(Rival(context, service, rival));
        var deals = service.Deals(sim)
            .Where(d => d.Status is DiplomaticDealStatus.Active or DiplomaticDealStatus.Proposed && (d.Proposer == context.Player || d.Recipient == context.Player))
            .OrderBy(d => d.Id).ToList();
        if (deals.Count > 0) items.Add(new Section("deals", "Active deals", deals.Select(d => Deal(context, service, d)).ToList()));
        if (rivals.Count > 0) items.Add(Composer(context, service, rivals));
        return new("Diplomacy", items);
    }

    private static List<Player> MetRivals(ScreenContext context)
    {
        var sim = context.Sim;
        int myTeam = context.MyTeam.Id;
        return sim.State.Players.Where(p => p.TeamId != myTeam && sim.Rules().Relations.HasMet(sim, myTeam, p.TeamId)).OrderBy(p => p.Id).ToList();
    }

    private static Section Rival(ScreenContext context, IDiplomacyService service, Player p)
    {
        var sim = context.Sim;
        var relations = sim.Rules().Relations;
        int me = context.Player, myTeam = context.MyTeam.Id;
        var attitude = service.GetAttitude(sim, p.Id, me);
        bool war = context.MyTeam.Wars.Contains(p.TeamId);
        bool treaty = !war && relations.ValidateWar(sim, me, p.Id) != null;
        var terms = new List<string>();
        if (service.HasOpenBorders(sim, myTeam, p.TeamId)) terms.Add("open borders");
        if (service.HasDefensivePact(sim, myTeam, p.TeamId)) terms.Add("defensive pact");
        string status = war ? "At war" : treaty ? "Peace treaty" : "At peace";
        string leader = Labels.Name<LeaderDef>(sim.Content, p.Leader), civilization = Labels.Name<CivilizationDef>(sim.Content, p.Civilization);
        var items = new List<ViewElement>
        {
            new Paragraph("title", $"{leader} of {civilization} ({p.Name})  -  {status}", war ? Tone.Bad : Tone.Normal, TextSize.Title),
            new Paragraph("attitude", $"Attitude: {attitude.Tier} ({attitude.Score:+#;-#;0})" + (terms.Count > 0 ? "  ·  " + string.Join(", ", terms) : ""),
                Tone.Accent, TextSize.Small),
        };
        int line = 0;
        foreach (var reason in attitude.Ledger.Lines.Where(l => l.Amount != 0))
            items.Add(new Paragraph($"reason:{line++}", $"    {Labels.Source(sim, reason.Source)}: {reason.Amount:+#;-#;0}", Tone.Dim, TextSize.Small));
        ActionButton Act(string id, string label, GameCommand command, string? blocked) => new(id, label, new SubmitCommand(command), blocked);
        items.Add(new Row("actions", [
            Act("declare_war", "Declare war", new DeclareWarCommand { Player = me, Target = p.Id }, relations.ValidateWar(sim, me, p.Id)),
            Act("make_peace", "Make peace", new MakePeaceCommand { Player = me, Target = p.Id }, relations.ValidatePeace(sim, me, p.Id)),
            Act("open_borders", "Open borders", new SignOpenBordersCommand { Player = me, Target = p.Id },
                service.ValidateAgreement(sim, me, p.Id, DiplomaticAgreement.OpenBorders)),
            Act("defensive_pact", "Defensive pact", new SignDefensivePactCommand { Player = me, Target = p.Id },
                service.ValidateAgreement(sim, me, p.Id, DiplomaticAgreement.DefensivePact)),
        ]));
        return new($"rival:{p.Id}", null, items);
    }

    private static Row Deal(ScreenContext context, IDiplomacyService service, DiplomaticDeal deal)
    {
        var sim = context.Sim;
        int me = context.Player;
        string summary = string.Join("; ", deal.Items.Select(i => $"{(i.FromPlayer == me ? "You give" : "You get")} {DealDraft.Describe(sim, i)}"));
        var items = new List<ViewElement> { new Paragraph("summary", $"#{deal.Id} {deal.Status}: {summary}") };
        if (deal.Status == DiplomaticDealStatus.Proposed && deal.Recipient == me)
            items.Add(new ActionButton("accept", "Accept", new SubmitCommand(new ExecuteDealCommand { Player = me, Deal = deal.Id }),
                service.ValidateExecution(sim, me, deal.Id)));
        items.Add(new ActionButton("cancel", "Cancel deal", new SubmitCommand(new CancelDealCommand { Player = me, Deal = deal.Id }),
            service.ValidateCancellation(sim, me, deal.Id)));
        return new Row($"deal:{deal.Id}", items);
    }

    /// <summary>Compose a deal with one partner, item by item both ways, see what the partner would answer, and send it.</summary>
    private static Section Composer(ScreenContext context, IDiplomacyService service, List<Player> rivals)
    {
        var sim = context.Sim;
        var local = context.Local;
        var partners = rivals.Where(p => !context.MyTeam.Wars.Contains(p.TeamId)).ToList();
        var partner = partners.FirstOrDefault(p => p.Id == local.Int("partner"));
        var items = new List<ViewElement>
        {
            new Picker("partner", partners.Select(p => new PickerOption($"partner:{p.Id}", p.Name,
                new ChangeState(local.With("partner", p.Id).Without(DealDraft.Give, DealDraft.Get)), p == partner)).ToList(),
                partners.Count == 0 ? "No one to trade with" : "Choose a leader"),
        };
        if (partner is null) return new("propose", "Propose a deal", items);
        var draft = DealDraft.Read(local, context.Player, partner.Id);
        items.Add(Side(context, draft, DealDraft.Give, "You give", context.Player, partner.Id));
        items.Add(Side(context, draft, DealDraft.Get, "You receive", partner.Id, context.Player));
        var deal = draft.Items;
        string? blocked = deal.Count == 0 ? "Add something to the deal" : service.ValidateProposal(sim, context.Player, partner.Id, deal);
        if (deal.Count > 0) items.Add(Verdict(context, service, partner, deal));
        items.Add(new Row("send", [
            new ActionButton("propose", "Propose deal",
                new SubmitCommand(new ProposeDealCommand { Player = context.Player, Target = partner.Id, Items = deal.ToList() }), blocked),
            new ActionButton("clear", "Clear", new ChangeState(local.Without(DealDraft.Give, DealDraft.Get)),
                deal.Count == 0 ? "The deal is empty" : null),
        ]));
        return new("propose", "Propose a deal", items);
    }

    /// <summary>One side of the deal: what is on it (click to take it off), and what could be added.</summary>
    private static Section Side(ScreenContext context, DealDraft draft, string key, string title, int from, int to)
    {
        var sim = context.Sim;
        var mine = draft.Side(key);
        var on = mine.Select((item, i) => (ViewElement)new ActionButton($"{key}:remove:{i}", "✕ " + DealDraft.Describe(sim, item),
            new ChangeState(draft.Without(context.Local, key, i)), Tooltip: "Take it off the deal")).ToList();
        if (on.Count == 0) on.Add(new Paragraph("empty", "Nothing yet.", Tone.Dim, TextSize.Small));
        var add = new List<ViewElement>();
        foreach (var item in DealDraft.Candidates(sim, from, to).Where(c => !mine.Any(m => m.Kind == c.Kind && m.ContentId == c.ContentId)))
            add.Add(new ActionButton($"{key}:add:{DealDraft.Encode(item)}", "+ " + DealDraft.Describe(sim, item),
                new ChangeState(draft.With(context.Local, key, item))));
        foreach (var (kind, step, label) in new[] { (TradeItemKind.Gold, 10L, "gold"), (TradeItemKind.GoldPerTurn, 1L, "gold per turn") })
        {
            long amount = mine.FirstOrDefault(i => i.Kind == kind)?.Amount ?? 0;
            add.Add(new ActionButton($"{key}:{kind}:less", $"−{step} {label}", new ChangeState(draft.WithAmount(context.Local, key, from, kind, amount - step)),
                amount <= 0 ? $"No {label} on this side" : null));
            add.Add(new ActionButton($"{key}:{kind}:more", $"+{step} {label}", new ChangeState(draft.WithAmount(context.Local, key, from, kind, amount + step))));
        }
        return new(key, title, [new Row("on", on), new Row("add", add)]);
    }

    /// <summary>What the partner would answer. A computer player's answer is its own rule (<see cref="DiplomacyPlanner.Accepts"/>)
    /// asked of an observing context; a human answers on their own turn.</summary>
    private static Paragraph Verdict(ScreenContext context, IDiplomacyService service, Player partner, IReadOnlyList<DiplomaticTradeItem> items)
    {
        if (partner.Ai is null) return new("verdict", $"{partner.Name} is a human leader and answers on their own turn.", Tone.Dim);
        var sim = context.Sim;
        var planner = new DiplomacyPlanner(AiContext.Observe(sim, partner.Id));
        var deal = new DiplomaticDeal { Proposer = context.Player, Recipient = partner.Id, Items = items.ToList() };
        var tier = service.GetAttitude(sim, partner.Id, context.Player).Tier;
        if (tier == Disposition.Furious) return new("verdict", $"{partner.Name} is furious with you and would refuse anything.", Tone.Bad);
        long value = planner.Value(items), margin = DiplomacyPlanner.Margin(tier);
        bool accepts = planner.Accepts(service, deal);
        string reason = $"worth {value} gold to them; at {tier} they want at least {margin}";
        return accepts ? new("verdict", $"{partner.Name} would accept: {reason}.", Tone.Good)
            : new("verdict", $"{partner.Name} would refuse: {reason}.", Tone.Bad);
    }
}

/// <summary>
/// The deal being composed, kept in the screen's local state as two lists, one per side: items separated by
/// <c>|</c>, each <c>kind:content:amount</c>. Item ids are left to the rules, which number a proposal's items.
/// </summary>
internal sealed record DealDraft(IReadOnlyList<DiplomaticTradeItem> Giving, IReadOnlyList<DiplomaticTradeItem> Getting)
{
    public const string Give = "give", Get = "get";

    public IReadOnlyList<DiplomaticTradeItem> Items => [.. Giving, .. Getting];

    public IReadOnlyList<DiplomaticTradeItem> Side(string key) => key == Give ? Giving : Getting;

    public static DealDraft Read(ScreenState state, int me, int partner) => new(Parse(state.Get(Give), me), Parse(state.Get(Get), partner));

    public ScreenState With(ScreenState state, string key, DiplomaticTradeItem item) => Write(state, key, [.. Side(key), item]);

    public ScreenState Without(ScreenState state, string key, int index) => Write(state, key, Side(key).Where((_, i) => i != index).ToList());

    /// <summary>Sets the side's amount of gold or gold per turn; zero or less takes it off.</summary>
    public ScreenState WithAmount(ScreenState state, string key, int from, TradeItemKind kind, long amount)
    {
        var items = Side(key).Where(i => i.Kind != kind).ToList();
        if (amount > 0) items.Add(new(from, kind, "", amount));
        return Write(state, key, items);
    }

    private static ScreenState Write(ScreenState state, string key, IReadOnlyList<DiplomaticTradeItem> items) =>
        state.With(key, items.Count == 0 ? null : string.Join("|", items.Select(Encode)));

    public static string Encode(DiplomaticTradeItem item) =>
        $"{item.Kind}:{item.ContentId}:{item.Amount.ToString(CultureInfo.InvariantCulture)}";

    private static List<DiplomaticTradeItem> Parse(string? text, int from)
    {
        var items = new List<DiplomaticTradeItem>();
        if (string.IsNullOrEmpty(text)) return items;
        foreach (var part in text.Split('|'))
        {
            // Content ids carry a colon of their own, so the kind is the first field and the amount the last.
            int first = part.IndexOf(':'), last = part.LastIndexOf(':');
            if (first < 0 || last <= first || !Enum.TryParse<TradeItemKind>(part[..first], out var kind)
                || !long.TryParse(part[(last + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out long amount)) continue;
            items.Add(new(from, kind, part[(first + 1)..last], amount));
        }
        return items;
    }

    /// <summary>What one side could put on the table: resources it holds and technologies the other side lacks. The
    /// rules still judge the whole deal, so a candidate may yet be refused.</summary>
    public static IEnumerable<DiplomaticTradeItem> Candidates(Simulation sim, int from, int to)
    {
        if (Capital(sim, from) is { } capital)
            foreach (var bonus in sim.Content.Get<BonusDef>().All.OrderBy(b => b.Name, StringComparer.Ordinal))
                if (ResourceAccess.Count(sim, from, capital.Id, bonus.Id) > 0) yield return new(from, TradeItemKind.Resource, bonus.Id);
        var giver = sim.TeamOf(from);
        var taker = sim.TeamOf(to);
        foreach (var tech in giver.Techs.Where(t => !taker.Techs.Contains(t)).Order(StringComparer.Ordinal))
            yield return new(from, TradeItemKind.Technology, tech);
    }

    private static City? Capital(Simulation sim, int player) => sim.State.Cities.Where(c => c.Owner == player)
        .OrderByDescending(c => c.Buildings.Any(b => sim.Content.Get<BuildingDef>(b).Capital)).ThenBy(c => c.Id).FirstOrDefault();

    public static string Describe(Simulation sim, DiplomaticTradeItem item) => item.Kind switch
    {
        TradeItemKind.Gold => $"{item.Amount} gold",
        TradeItemKind.GoldPerTurn => $"{item.Amount} gold per turn",
        TradeItemKind.Technology => Labels.Name<TechDef>(sim.Content, item.ContentId),
        _ => (item.Amount == 1 ? "" : item.Amount + " ") + Labels.Name<BonusDef>(sim.Content, item.ContentId),
    };
}

/// <summary>Readable names for the reasons in an attitude: the remembered acts by their memory kind's name, the rest
/// by what they mean.</summary>
internal sealed class AttitudeLabels : ILedgerSourceLabel
{
    private const string Prefix = "diplomacy:";

    public string? Label(Simulation sim, string source)
    {
        if (!source.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        string reason = source[Prefix.Length..];
        if (reason.StartsWith("memory:", StringComparison.Ordinal))
            return Labels.Name<MemoryKindDef>(sim.Content, reason["memory:".Length..]);
        if (reason.StartsWith("civic_pair:", StringComparison.Ordinal) && reason.Split(':') is { Length: >= 5 } pair)
            return $"{Labels.Name<CivicDef>(sim.Content, pair[1] + ":" + pair[2])} against {Labels.Name<CivicDef>(sim.Content, pair[3] + ":" + pair[4])}";
        if (reason.StartsWith("civic:", StringComparison.Ordinal))
            return Labels.Name<CivicDef>(sim.Content, reason["civic:".Length..]);
        return reason switch
        {
            "base" => "Base attitude",
            "peace_weight" => "Temperament",
            "warmonger_respect" => "Respect for warmongers",
            "favorite_civic" => "Our favourite civic",
            "close_borders" => "Our borders are too close",
            "active_deals" => "Our trade",
            "war" => "We are at war",
            "years_of_peace" => "Years of peace",
            "war_history" => "Past wars against us",
            "open_borders" => "Open borders",
            "defensive_pact" => "Defensive pact",
            "same_religion" => "Shared religion",
            "different_religion" => "Different religion",
            _ => null,
        };
    }
}
