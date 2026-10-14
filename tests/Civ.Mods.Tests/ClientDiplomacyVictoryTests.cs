using Civ.Client;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;

namespace Civ.Mods.Tests;

/// <summary>Client.Core diplomacy, score and victory screens against the optional diplomacy and victory mods.</summary>
public sealed class ClientDiplomacyVictoryTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static ModSource Optional(string name) => ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", name));
    private GameSession Session(bool diplomacy = true, bool victory = true)
    {
        var sources = mods.Sources.AsEnumerable();
        if (diplomacy) sources = sources.Append(Optional("Diplomacy"));
        if (victory) sources = sources.Append(Optional("ProjectsVictory"));
        var sim = GameFactory.Create(ModLoader.Build(sources), 5, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar");
        if (diplomacy)
            sim.TeamOf(0).Techs.UnionWith(["research:writing", "research:currency", "research:alphabet", "research:political_philosophy"]);
        return new(sim);
    }

    [Fact]
    public void Screens_are_empty_when_the_ruleset_has_no_such_rules()
    {
        var session = Session(false, false);
        Assert.False(session.DescribeDiplomacy().Available);
        var victory = session.DescribeVictoryAndScores();
        Assert.False(victory.Available); Assert.Null(victory.Status); Assert.Empty(victory.Scores);
    }

    [Fact]
    public void Diplomacy_lists_only_met_rivals_with_attitude_and_actions()
    {
        var session = Session();
        Assert.True(session.DescribeDiplomacy().Available);
        Assert.Empty(session.DescribeDiplomacy().Rivals);
        var service = session.Sim.Content.Services.Get<IDiplomacyService>().Single();
        service.EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);
        var rival = Assert.Single(session.DescribeDiplomacy().Rivals);
        Assert.Equal(1, rival.PlayerId);
        Assert.False(string.IsNullOrEmpty(rival.Leader));
        Assert.False(rival.AtWar);
        Assert.Equal(["declare_war", "make_peace", "open_borders", "defensive_pact"], rival.Actions.Select(a => a.Kind));
        Assert.NotNull(rival.Actions.Single(a => a.Kind == "make_peace").Blocked);
        Assert.Null(rival.Actions.Single(a => a.Kind == "open_borders").Blocked);
    }

    [Fact]
    public void Actions_run_through_commands_and_update_the_screen()
    {
        var session = Session();
        var service = session.Sim.Content.Services.Get<IDiplomacyService>().Single();
        service.EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);
        Assert.True(session.DoDiplomacy(session.DescribeDiplomacy().Rivals[0].Actions.Single(a => a.Kind == "open_borders")));
        Assert.True(session.DescribeDiplomacy().Rivals[0].OpenBorders);
        Assert.True(session.DoDiplomacy(session.DescribeDiplomacy().Rivals[0].Actions.Single(a => a.Kind == "declare_war")));
        var rival = session.DescribeDiplomacy().Rivals[0];
        Assert.True(rival.AtWar); Assert.Equal("At war", rival.Status); Assert.False(rival.OpenBorders);
    }

    [Fact]
    public void Active_deals_can_be_cancelled()
    {
        var session = Session();
        var service = session.Sim.Content.Services.Get<IDiplomacyService>().Single();
        service.EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);
        session.Me.Treasury = 100;
        Assert.True(session.Submit(new ProposeDealCommand { Player = GameSession.Human, Target = 1, Items = [new(GameSession.Human, TradeItemKind.Gold, "", 10)] }));
        var deal = Assert.Single(session.DescribeDiplomacy().Deals);
        Assert.Contains("You give 10 gold", deal.Summary);
        Assert.True(session.DoDiplomacy(deal.Cancel!));
        Assert.Empty(session.DescribeDiplomacy().Deals);
    }

    [Fact]
    public void Proposed_deals_to_the_human_can_be_accepted()
    {
        var session = Session();
        var service = session.Sim.Content.Services.Get<IDiplomacyService>().Single();
        service.EstablishContact(session.Sim, session.MyTeam.Id, session.Sim.TeamOf(1).Id);
        var goldTrading = session.Sim.Content.Get<Civ.Content.Schema.TechDef>().All.First(t => t.GoldTrading);
        session.Sim.TeamOf(GameSession.Human).Techs.Add(goldTrading.Id);
        session.Sim.TeamOf(1).Techs.Add(goldTrading.Id);
        session.Sim.State.PlayerById(1).Treasury = 100;
        Assert.True(session.Submit(new ProposeDealCommand
        {
            Player = 1, Target = GameSession.Human,
            Items = [new(1, TradeItemKind.GoldPerTurn, Amount: 1)],
        }));

        var deal = Assert.Single(session.DescribeDiplomacy().Deals);
        Assert.Equal("Proposed", deal.Status);
        Assert.NotNull(deal.Accept);
        Assert.True(session.DoDiplomacy(deal.Accept));
        Assert.Equal("Active", Assert.Single(session.DescribeDiplomacy().Deals).Status);
    }

    [Fact]
    public void Victory_screen_reports_scores_and_the_running_game()
    {
        var session = Session();
        var screen = session.DescribeVictoryAndScores();
        Assert.True(screen.Available);
        Assert.False(screen.Status!.IsGameOver);
        Assert.Equal(2, screen.Scores.Count);
        Assert.Single(screen.Scores, s => s.IsLocal);
        Assert.NotEmpty(screen.Conditions);
        Assert.Null(screen.WinnerName);
    }
}
