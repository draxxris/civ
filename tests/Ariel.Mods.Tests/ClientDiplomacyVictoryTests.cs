using System.Collections.Concurrent;
using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Diplomacy;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The diplomacy and victory mods' screens, driven through the client's headless host.</summary>
public sealed class ClientDiplomacyVictoryTests
{
    private static readonly ConcurrentDictionary<(bool Diplomacy, bool Victory), Lazy<ContentDb>> Contents = new();
    private static ModSource Optional(string name) => ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name));

    /// <summary>One frozen content per optional-mod set; xunit builds a new test class instance for every test.</summary>
    private static ContentDb Content((bool Diplomacy, bool Victory) optional) => Contents.GetOrAdd(optional, key => new(() =>
    {
        var loaded = new LoadedMods();
        var sources = loaded.Sources.AsEnumerable();
        if (key.Diplomacy) sources = sources.Append(Optional("Diplomacy"));
        if (key.Victory) sources = sources.Append(Optional("ProjectsVictory"));
        return ModLoader.Build(sources);
    })).Value;

    private GameSession Session(bool diplomacy = true, bool victory = true)
    {
        var sim = GameFactory.Create(Content((diplomacy, victory)), 5, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar");
        if (diplomacy)
            sim.TeamOf(0).Techs.UnionWith(["research:writing", "research:currency", "research:alphabet", "research:political_philosophy"]);
        return new(sim);
    }

    private const string DiplomacyId = "diplomacy:screen", VictoryId = "projects_victory:victory";

    private static IReadOnlyList<ViewElement> Rival(GameSession session, int player) =>
        ScreenProbe.Section(ScreenProbe.Body(session, DiplomacyId), $"rival:{player}");

    private static IReadOnlyList<ViewElement> Deals(GameSession session) =>
        ViewWalk.Find<Section>(ScreenProbe.Body(session, DiplomacyId), "deals")?.Items ?? [];

    private static void Meet(GameSession session) =>
        session.Sim.Rules().Relations.EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);

    [Fact]
    public void Screens_are_empty_when_the_ruleset_has_no_such_rules()
    {
        var session = Session(false, false);
        Assert.False(ScreenProbe.Registered(session, DiplomacyId));
        Assert.False(ScreenProbe.Registered(session, VictoryId));
        // Only the main mods' screens remain.
        Assert.Equal(["religion:screen"], session.Screens.Entries().Select(e => e.Id));
    }

    [Fact]
    public void Diplomacy_lists_only_met_rivals_with_attitude_and_actions()
    {
        var session = Session();
        Assert.NotNull(ScreenProbe.Button(session, DiplomacyId));
        var before = ScreenProbe.Body(session, DiplomacyId);
        Assert.NotNull(ScreenProbe.Paragraph(before, "none"));
        Assert.Null(ViewWalk.Find<Section>(before, "propose"));
        Meet(session);
        var body = ScreenProbe.Body(session, DiplomacyId);
        Assert.Equal(["rival:1"], ViewWalk.All(body).OfType<Section>().Select(s => s.Id).Where(id => id.StartsWith("rival:", StringComparison.Ordinal)));
        var rival = Rival(session, 1);
        string leader = session.Sim.Content.Get<Ariel.Content.Schema.LeaderDef>(session.Sim.State.PlayerById(1).Leader).Name;
        Assert.StartsWith(leader + " of ", ScreenProbe.Paragraph(rival, "title"));
        Assert.EndsWith("At peace", ScreenProbe.Paragraph(rival, "title"));
        Assert.Equal(["declare_war", "make_peace", "open_borders", "defensive_pact"],
            ViewWalk.All(rival).OfType<ActionButton>().Select(b => b.Id));
        Assert.NotNull(ViewWalk.Find<ActionButton>(rival, "make_peace")!.Disabled);
        Assert.Null(ViewWalk.Find<ActionButton>(rival, "open_borders")!.Disabled);
        // Every attitude reason reads as words, never as a rules id.
        var reasons = ViewWalk.All(rival).OfType<Paragraph>().Where(p => p.Id.StartsWith("reason:", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(reasons);
        Assert.All(reasons, r => Assert.DoesNotContain("diplomacy:", r.Text));
        Assert.Contains(reasons, r => r.Text.Contains("Temperament"));
    }

    [Fact]
    public void Remembered_acts_are_named_by_their_memory_kind()
    {
        var session = Session();
        Meet(session);
        var kind = session.Sim.Content.Get<Ariel.Content.Schema.MemoryKindDef>().All.First();
        Assert.Equal(kind.Name, Labels.Source(session.Sim, "diplomacy:memory:" + kind.Id));
    }

    [Fact]
    public void Actions_run_through_commands_and_update_the_screen()
    {
        var session = Session();
        Meet(session);
        Assert.True(session.Screens.Press(ViewWalk.ActionOf(Rival(session, 1), "open_borders")));
        Assert.Contains("open borders", ScreenProbe.Paragraph(Rival(session, 1), "attitude"));
        Assert.True(session.Screens.Press(ViewWalk.ActionOf(Rival(session, 1), "declare_war")));
        var rival = Rival(session, 1);
        Assert.EndsWith("At war", ScreenProbe.Paragraph(rival, "title"));
        Assert.DoesNotContain("open borders", ScreenProbe.Paragraph(rival, "attitude"));
        // No one at war with us can be traded with.
        var partners = ViewWalk.Find<Picker>(ScreenProbe.Body(session, DiplomacyId), "partner")!;
        Assert.Empty(partners.Options);
    }

    [Fact]
    public void Active_deals_can_be_cancelled()
    {
        var session = Session();
        Meet(session);
        session.Me.Treasury = 100;
        Assert.True(session.Submit(new ProposeDealCommand { Player = GameSession.DefaultLocalPlayer, Target = 1, Items = [new(GameSession.DefaultLocalPlayer, TradeItemKind.Gold, "", 10)] }));
        var deal = Assert.IsType<Row>(Assert.Single(Deals(session)));
        Assert.Contains("You give 10 gold", ScreenProbe.Paragraph(deal.Items, "summary"));
        Assert.True(session.Screens.Press(ViewWalk.ActionOf(deal.Items, "cancel")));
        Assert.Empty(Deals(session));
    }

    [Fact]
    public void Proposed_deals_to_the_human_can_be_accepted()
    {
        var session = Session();
        Meet(session);
        var goldTrading = session.Sim.Content.Get<Ariel.Content.Schema.TechDef>().All.First(t => t.GoldTrading);
        session.Sim.TeamOf(GameSession.DefaultLocalPlayer).Techs.Add(goldTrading.Id);
        session.Sim.TeamOf(1).Techs.Add(goldTrading.Id);
        session.Sim.State.PlayerById(1).Treasury = 100;
        Assert.True(session.Submit(new ProposeDealCommand
        {
            Player = 1, Target = GameSession.DefaultLocalPlayer,
            Items = [new(1, TradeItemKind.GoldPerTurn, Amount: 1)],
        }));

        var deal = Assert.IsType<Row>(Assert.Single(Deals(session)));
        Assert.Contains(" Proposed: ", ScreenProbe.Paragraph(deal.Items, "summary"));
        Assert.True(session.Screens.Press(ViewWalk.ActionOf(deal.Items, "accept")));
        var active = Assert.IsType<Row>(Assert.Single(Deals(session)));
        Assert.Contains(" Active: ", ScreenProbe.Paragraph(active.Items, "summary"));
        Assert.Null(ViewWalk.Find<ActionButton>(active.Items, "accept"));
    }

    [Theory]
    [InlineData("option", "technology trading is disabled")]
    [InlineData("unlock", "no team has researched the technology enabling technology trading")]
    public void Deal_screen_refreshes_rejection_reasons_and_stale_actions_cannot_bypass_them(string change, string reason)
    {
        var session = Session();
        var sim = session.Sim;
        Meet(session);
        session.MyTeam.Techs.Remove("research:woodworking");
        sim.TeamOf(1).Techs.Add("research:woodworking");
        session.Me.Treasury = 100;
        Assert.True(session.Submit(new ProposeDealCommand { Player = 1, Target = GameSession.DefaultLocalPlayer,
            Items = [new(1, TradeItemKind.Technology, "research:woodworking"), new(GameSession.DefaultLocalPlayer, TradeItemKind.Gold, Amount: 7)] }));
        ActionButton Accept() => ViewWalk.Find<ActionButton>(Deals(session), "accept")!;
        var stale = Accept();
        Assert.Null(stale.Disabled);
        if (change == "option") sim.Settings.GameOptions["foundation:gameoption_no_tech_trading"] = true;
        else foreach (var team in sim.State.Teams) team.Techs.Remove("research:alphabet");
        ulong hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        Assert.Equal(reason, Accept().Disabled);
        Assert.False(session.Screens.Press(stale.Action));
        Assert.Equal("Can't do that: " + reason, session.Message);
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
        Assert.Contains(" Proposed: ", ScreenProbe.Paragraph(Deals(session), "summary"));
        if (change == "option") sim.Settings.GameOptions["foundation:gameoption_no_tech_trading"] = false;
        else sim.TeamOf(1).Techs.Add("research:alphabet");
        var allowed = Accept();
        Assert.Null(allowed.Disabled);
        Assert.True(session.Screens.Press(allowed.Action));
        Assert.Empty(Deals(session));
        Assert.True(sim.KnowsTech(GameSession.DefaultLocalPlayer, "research:woodworking"));
        Assert.Equal(93, session.Me.Treasury);
    }

    [Fact]
    public void The_composer_builds_a_deal_both_ways_shows_the_answer_and_sends_it()
    {
        var session = Session();
        var sim = session.Sim;
        Meet(session);
        var goldTrading = sim.Content.Get<Ariel.Content.Schema.TechDef>().All.First(t => t.GoldTrading);
        foreach (var team in sim.State.Teams) team.Techs.Add(goldTrading.Id);
        session.Me.Treasury = 500;
        Assert.True(ScreenProbe.Press(session, DiplomacyId, "partner:1"));
        var composer = ScreenProbe.Section(ScreenProbe.Body(session, DiplomacyId), "propose");
        Assert.Equal("Add something to the deal", ViewWalk.Find<ActionButton>(composer, "propose")!.Disabled);
        Assert.Null(ViewWalk.Find<Paragraph>(composer, "verdict"));

        Assert.True(ScreenProbe.Press(session, DiplomacyId, "give:Gold:more"));
        Assert.True(ScreenProbe.Press(session, DiplomacyId, "give:Gold:more"));
        // The partner's technologies are offered on the other side, by name.
        var tech = sim.TeamOf(1).Techs.Where(t => !session.MyTeam.Techs.Contains(t)).Order(StringComparer.Ordinal).First();
        string add = Assert.Single(ScreenProbe.ButtonIds(ScreenProbe.Body(session, DiplomacyId), $"get:add:Technology:{tech}:"));
        Assert.True(ScreenProbe.Press(session, DiplomacyId, add));
        // A human partner answers on their own turn; a computer player's answer is shown before sending.
        Assert.Contains("human leader", ScreenProbe.Paragraph(ScreenProbe.Body(session, DiplomacyId), "verdict"));
        sim.State.PlayerById(1).Ai = sim.Content.PlayerAIs.All.First().Id;
        ulong hash = sim.ComputeHash(); var rng = sim.Rng.State;
        composer = ScreenProbe.Section(ScreenProbe.Body(session, DiplomacyId), "propose");
        Assert.Equal("✕ 20 gold", ViewWalk.Find<ActionButton>(ScreenProbe.Section(composer, "give"), "give:remove:0")!.Label);
        Assert.Equal("✕ " + sim.Content.Get<Ariel.Content.Schema.TechDef>(tech).Name,
            ViewWalk.Find<ActionButton>(ScreenProbe.Section(composer, "get"), "get:remove:0")!.Label);
        Assert.Matches(@" would (accept|refuse): worth -?\d+ gold to them; at \w+ they want at least -?\d+\.$", ScreenProbe.Paragraph(composer, "verdict"));
        // Asking the computer player for its answer changes nothing.
        Assert.Equal(hash, sim.ComputeHash()); Assert.Equal(rng, sim.Rng.State);

        Assert.True(ScreenProbe.Press(session, DiplomacyId, "give:remove:0"));
        Assert.True(ScreenProbe.Press(session, DiplomacyId, "give:Gold:more"));
        Assert.True(ScreenProbe.Press(session, DiplomacyId, "propose"));
        var service = sim.Service<IDiplomacyService>()!;
        Assert.Contains(service.Deals(sim), d => d.Proposer == 0 && d.Recipient == 1
            && d.Items.Any(i => i.FromPlayer == 0 && i.Kind == TradeItemKind.Gold && i.Amount == 10)
            && d.Items.Any(i => i.FromPlayer == 1 && i.Kind == TradeItemKind.Technology && i.ContentId == tech));
        // Clearing empties the draft; the sent deal stays.
        Assert.True(ScreenProbe.Press(session, DiplomacyId, "clear"));
        Assert.Null(ViewWalk.Find<ActionButton>(ScreenProbe.Body(session, DiplomacyId), "give:remove:0"));
    }

    [Fact]
    public void Victory_screen_reports_scores_and_the_running_game()
    {
        var session = Session();
        Assert.NotNull(ScreenProbe.Button(session, VictoryId));
        var body = ScreenProbe.Body(session, VictoryId);
        Assert.StartsWith("Game in progress", ScreenProbe.Paragraph(body, "state"));
        Assert.StartsWith("Victory: ", ScreenProbe.Paragraph(body, "conditions"));
        var table = ViewWalk.Find<Table>(body, "table")!;
        Assert.Equal(2, table.Rows.Count);
        Assert.Single(table.Rows, r => r.Tone == Tone.Accent && r.Id == "player:0");
    }
}
