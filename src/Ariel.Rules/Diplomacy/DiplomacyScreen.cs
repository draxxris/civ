using System.Globalization;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Rules.Diplomacy;

/// <summary>
/// Diplomacy with every rival the local team has met: attitude and its reasons, treaties and the actions against them
/// (war and peace are the base game's commands), the open and active deals, and a deal composer that shows a computer
/// player's answer before the proposal is sent. Local state: <c>partner</c> (a player id), <c>intent</c> (a
/// <see cref="DealIntent"/>), and <c>give</c> and <c>get</c>, the items of the deal being composed (see
/// <see cref="DealDraft"/>).
/// </summary>
internal sealed class DiplomacyScreen : IGameScreen
{
    public string Id => "diplomacy:screen";
    public int Order => 40;

    public ScreenButton? Button(ScreenContext context) => new("Diplomacy", "Rival leaders, attitudes, treaties and deals");

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        var service = sim.Rules().Diplomacy;
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
        string summary = string.Join("; ", deal.Items.Select(i =>
            $"{(i.Mutual ? "Both sign" : i.FromPlayer == me ? "You give" : "You get")} {DealDraft.Describe(sim, i)}"));
        string other = sim.State.PlayerById(deal.Proposer == me ? deal.Recipient : deal.Proposer).Name;
        string from = deal.Proposer == me ? "You" : other;
        string kind = deal.Intent switch
        {
            DealIntent.Demand => $"{from} demand{(deal.Proposer == me ? "" : "s")}",
            DealIntent.Request => $"{from} ask{(deal.Proposer == me ? "" : "s")} for help",
            _ => deal.CounterTo is { } answered ? $"{from} counter{(deal.Proposer == me ? "" : "s")} #{answered}" : $"With {other}",
        };
        var tone = deal.Intent == DealIntent.Demand && deal.Recipient == me ? Tone.Bad : Tone.Normal;
        var items = new List<ViewElement> { new Paragraph("summary", $"#{deal.Id} {deal.Status} · {kind}: {summary}", tone) };
        bool answering = deal.Status == DiplomaticDealStatus.Proposed && deal.Recipient == me;
        if (answering)
            items.Add(new ActionButton("accept", "Accept", new SubmitCommand(new ExecuteDealCommand { Player = me, Deal = deal.Id }),
                service.ValidateExecution(sim, me, deal.Id)));
        string cancel = answering ? "Refuse" : deal.Status == DiplomaticDealStatus.Proposed ? "Withdraw" : "Cancel deal";
        items.Add(new ActionButton("cancel", cancel, new SubmitCommand(new CancelDealCommand { Player = me, Deal = deal.Id }),
            service.ValidateCancellation(sim, me, deal.Id)));
        return new Row($"deal:{deal.Id}", items);
    }

    /// <summary>Compose a deal with one partner, item by item both ways, as a trade, a demand or a request for help, see
    /// what the partner would answer, and send it. An enemy is a partner too: a deal with it must hold a peace treaty.</summary>
    private static Section Composer(ScreenContext context, IDiplomacyService service, List<Player> rivals)
    {
        var sim = context.Sim;
        var local = context.Local;
        var partners = rivals.Where(p => PlayerRules.IsAlive(sim, p.Id) && PlayerRules.Negotiates(sim, p.Id)).ToList();
        var partner = partners.FirstOrDefault(p => p.Id == local.Int("partner"));
        var items = new List<ViewElement>
        {
            new Picker("partner", partners.Select(p => new PickerOption($"partner:{p.Id}", p.Name,
                new ChangeState(local.With("partner", p.Id).Without(DealDraft.Give, DealDraft.Get)), p == partner)).ToList(),
                partners.Count == 0 ? "No one to trade with" : "Choose a leader"),
        };
        if (partner is null) return new("propose", "Propose a deal", items);
        var draft = DealDraft.Read(local, context.Player, partner.Id);
        var intent = Enum.TryParse<DealIntent>(local.Get("intent"), out var chosen) ? chosen : DealIntent.Trade;
        items.Add(new Picker("intent", Enum.GetValues<DealIntent>().Select(i => new PickerOption($"intent:{i}", IntentLabel(i),
            new ChangeState(local.With("intent", i.ToString())), i == intent)).ToList(), "Ask as"));
        if (intent == DealIntent.Trade) items.Add(Side(context, draft, DealDraft.Give, "You give", context.Player, partner.Id));
        items.Add(Side(context, draft, DealDraft.Get, "You receive", partner.Id, context.Player));
        var deal = intent == DealIntent.Trade ? draft.Items : draft.Getting;
        string? blocked = deal.Count == 0 ? "Add something to the deal" : service.ValidateProposal(sim, context.Player, partner.Id, deal, intent);
        if (deal.Count > 0) items.Add(Verdict(context, service, partner, deal, intent));
        items.Add(new Row("send", [
            new ActionButton("propose", intent switch { DealIntent.Demand => "Demand", DealIntent.Request => "Ask for help", _ => "Propose deal" },
                new SubmitCommand(new ProposeDealCommand { Player = context.Player, Target = partner.Id, Items = deal.ToList(), Intent = intent }), blocked),
            new ActionButton("clear", "Clear", new ChangeState(local.Without(DealDraft.Give, DealDraft.Get)),
                deal.Count == 0 ? "The deal is empty" : null),
        ]));
        return new("propose", "Propose a deal", items);
    }

    private static string IntentLabel(DealIntent intent) => intent switch
    {
        DealIntent.Demand => "Demand tribute",
        DealIntent.Request => "Ask for help",
        _ => "Trade",
    };

    /// <summary>One side of the deal: what is on it (click to take it off), and what could be added. Treaties binding
    /// both sides are offered on the giving side only.</summary>
    private static Section Side(ScreenContext context, DealDraft draft, string key, string title, int from, int to)
    {
        var sim = context.Sim;
        var mine = draft.Side(key);
        var on = mine.Select((item, i) => (ViewElement)new ActionButton($"{key}:remove:{i}", "✕ " + DealDraft.Describe(sim, item),
            new ChangeState(draft.Without(context.Local, key, i)), Tooltip: "Take it off the deal")).ToList();
        if (on.Count == 0) on.Add(new Paragraph("empty", "Nothing yet.", Tone.Dim, TextSize.Small));
        var add = new List<ViewElement>();
        foreach (var item in DealDraft.Candidates(sim, from, to, treaties: key == DealDraft.Give)
            .Where(c => !mine.Any(m => m.Kind == c.Kind && m.ContentId == c.ContentId && m.Against == c.Against)))
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

    /// <summary>What the partner would answer. A computer player's answer is the computer player's own rule, asked through
    /// <see cref="IDealAppraiser"/>; a human answers on their own turn.</summary>
    private static Paragraph Verdict(ScreenContext context, IDiplomacyService service, Player partner, IReadOnlyList<DiplomaticTradeItem> items,
        DealIntent intent)
    {
        if (partner.Ai is null) return new("verdict", $"{partner.Name} is a human leader and answers on their own turn.", Tone.Dim);
        var sim = context.Sim;
        if (sim.Rules().DealAppraiser is not { } appraiser)
            return new("verdict", $"{partner.Name} answers on their own turn.", Tone.Dim);
        var tier = service.GetAttitude(sim, partner.Id, context.Player).Tier;
        var (value, margin, accepts, refusal) = appraiser.Appraise(sim, partner.Id, context.Player, items, intent);
        if (refusal is not null) return new("verdict", $"{partner.Name} {refusal}.", Tone.Bad);
        if (tier == Disposition.Furious && !accepts) return new("verdict", $"{partner.Name} is furious with you and would refuse.", Tone.Bad);
        string reason = intent switch
        {
            DealIntent.Demand => $"it costs them {-value} gold; they give in only to a much stronger leader",
            DealIntent.Request => $"it costs them {-value} gold; they help only leaders they like",
            _ => $"worth {value} gold to them; at {tier} they want at least {margin}",
        };
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

    /// <summary>An embargo keeps the player it is laid against where other kinds keep their content id.</summary>
    public static string Encode(DiplomaticTradeItem item) =>
        $"{item.Kind}:{(item.Kind == TradeItemKind.Embargo ? item.Against.ToString(CultureInfo.InvariantCulture) : item.ContentId)}:{item.Amount.ToString(CultureInfo.InvariantCulture)}";

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
            string content = part[(first + 1)..last];
            if (kind != TradeItemKind.Embargo) items.Add(new(from, kind, content, amount));
            else if (int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out int against))
                items.Add(new(from, kind, Amount: amount, Against: against));
        }
        return items;
    }

    /// <summary>What one side could put on the table: resources it holds, technologies the other side lacks, an embargo
    /// on any third civilization it has met, and, with <paramref name="treaties"/>, the treaties the two could sign (a
    /// peace treaty at war, otherwise open borders and a defensive pact). The rules still judge the whole deal, so a
    /// candidate may yet be refused.</summary>
    public static IEnumerable<DiplomaticTradeItem> Candidates(Simulation sim, int from, int to, bool treaties = false)
    {
        var relations = sim.Rules().Relations;
        int fromTeam = sim.TeamOf(from).Id, toTeam = sim.TeamOf(to).Id;
        bool war = sim.TeamOf(from).Wars.Contains(toTeam);
        if (treaties && war) yield return new(from, TradeItemKind.PeaceTreaty);
        if (treaties && !war)
        {
            yield return new(from, TradeItemKind.OpenBorders);
            yield return new(from, TradeItemKind.DefensivePact);
        }
        // At war only gold and technology can go with the peace: resources and embargoes need trade between the two.
        if (!war && Capital(sim, from) is { } capital)
            foreach (var bonus in sim.Content.Get<BonusDef>().All.OrderBy(b => b.Name, StringComparer.Ordinal))
                if (ResourceAccess.Count(sim, from, capital.Id, bonus.Id) > 0) yield return new(from, TradeItemKind.Resource, bonus.Id);
        var giver = sim.TeamOf(from);
        var taker = sim.TeamOf(to);
        foreach (var tech in giver.Techs.Where(t => !taker.Techs.Contains(t)).Order(StringComparer.Ordinal))
            yield return new(from, TradeItemKind.Technology, tech);
        if (war) yield break;
        foreach (var third in sim.State.Players.Where(p => p.TeamId != fromTeam && p.TeamId != toTeam && PlayerRules.Negotiates(sim, p.Id)
            && sim.State.Cities.Any(c => c.Owner == p.Id) && relations.HasMet(sim, fromTeam, p.TeamId)).OrderBy(p => p.Id))
            yield return new(from, TradeItemKind.Embargo, Against: third.Id);
    }

    private static City? Capital(Simulation sim, int player) => sim.State.Cities.Where(c => c.Owner == player)
        .OrderByDescending(c => c.Buildings.Any(b => sim.Content.Get<BuildingDef>(b).Capital)).ThenBy(c => c.Id).FirstOrDefault();

    public static string Describe(Simulation sim, DiplomaticTradeItem item) => item.Kind switch
    {
        TradeItemKind.Gold => $"{item.Amount} gold",
        TradeItemKind.GoldPerTurn => $"{item.Amount} gold per turn",
        TradeItemKind.Technology => Labels.Name<TechDef>(sim.Content, item.ContentId),
        TradeItemKind.Embargo => sim.State.Players.FirstOrDefault(p => p.Id == item.Against) is { } target
            ? $"an embargo on {target.Name}" : "an embargo",
        TradeItemKind.PeaceTreaty => "a peace treaty",
        TradeItemKind.OpenBorders => "open borders",
        TradeItemKind.DefensivePact => "a defensive pact",
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
