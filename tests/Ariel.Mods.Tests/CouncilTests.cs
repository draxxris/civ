using Ariel.Client;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.ProjectsVictory;
using Ariel.Mods.Votes;
using Xunit;
using TechDef = Ariel.Content.Schema.TechDef;

namespace Ariel.Mods.Tests;

/// <summary>World councils: founding, elections, resolutions and their effects, defiance and sanctions, and the
/// computer player's votes.</summary>
public sealed class CouncilTests
{
    private const string Un = "votes:diplovote_un";
    private static readonly Lazy<ContentDb> Content = new(() => Build("Votes", "ProjectsVictory"));
    /// <summary>Votes without ProjectsVictory: no victory to award.</summary>
    private static readonly Lazy<ContentDb> Alone = new(() => Build("Votes"));

    private static ContentDb Build(params string[] optional) => ModLoader.Build(new LoadedMods().Sources
        .Concat(optional.Select(n => ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", n)))));

    [Fact]
    public void The_council_buildings_belong_to_the_diplomatic_victory()
    {
        var sim = Scenario(seat: false);
        Assert.True(sim.Rules().VictoryConditions!.IsEnabled(sim, "foundation:victory_diplomatic"));
        var city = sim.State.Cities.First(c => c.Owner == 0);
        var requirements = sim.Content.Get<BuildingDef>("cities:united_nations").Requirements;
        var failures = sim.Content.Requirements.Evaluate(requirements, new(sim, 0, city, sim.State.Grid.TileAt(city.Position))).Failures;
        Assert.DoesNotContain(failures, f => f.Explanation.Contains("victory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_council_sits_once_its_building_stands_and_elects_a_secretary()
    {
        var sim = Scenario();
        Assert.Empty(Votes(sim).Councils(sim));
        sim.AdvanceTurn();

        Assert.Equal([Un], Votes(sim).Councils(sim));
        var session = Votes(sim).Council(sim, Un)!.Session!;
        Assert.Equal("votes:vote_secretary_general", session.Resolution.Vote);
        Assert.Equal([0, 1, 2], Votes(sim).Candidates(sim, Un).Select(t => Player(sim, t)));
        Assert.Equal(sim.State.Cities.First(c => c.Owner == 0).Population, Votes(sim).Votes(sim, 0, Un));
        Assert.Contains("does not stand", Reject(sim, Ballot(1, VoteChoice.Yes, 99)));

        ElectFirstTeam(sim);
        Assert.Equal(sim.TeamOf(0).Id, Votes(sim).Council(sim, Un)!.Secretary);
    }

    [Fact]
    public void Only_the_secretary_puts_resolutions_and_never_one_the_game_does_not_run()
    {
        var sim = Seated();
        Assert.Contains("only the secretary", Reject(sim, Propose(1, "votes:vote_free_trade")));
        Assert.Contains("not held in this game", Reject(sim, Propose(0, "votes:vote_no_nukes")));
        Assert.DoesNotContain(Votes(sim).Proposals(sim, Un), r => r.Vote is "votes:vote_no_nukes" or "votes:vote_un_assign_city");
        Assert.Contains(Votes(sim).Proposals(sim, Un), r => r.Vote == "votes:vote_free_trade");
    }

    [Fact]
    public void Free_trade_opens_every_city_to_trade_without_open_borders()
    {
        var sim = Seated();
        Assert.False(sim.Rules().Relations.FreeTrade(sim, sim.TeamOf(1).Id, sim.TeamOf(2).Id));
        Pass(sim, "votes:vote_free_trade");
        Assert.True(sim.Rules().Relations.FreeTrade(sim, sim.TeamOf(1).Id, sim.TeamOf(2).Id));
        Assert.DoesNotContain(Votes(sim).Proposals(sim, Un), r => r.Vote == "votes:vote_free_trade");
    }

    [Fact]
    public void A_forced_civic_moves_every_member_onto_it_and_rules_out_its_rivals()
    {
        var sim = Seated();
        Pass(sim, "votes:vote_universal_suffrage");
        Assert.All(sim.State.Players, p => Assert.Contains("government:democracy", p.Civics.Values));
        var democracy = sim.Content.Get<CivicDef>("government:democracy");
        var rival = sim.Content.Get<CivicDef>().All.First(c => c.Option == democracy.Option && c.Id != democracy.Id);
        Assert.Contains("requires", sim.Rules().Government.Validate(sim, 1, [rival.Id]));
    }

    [Fact]
    public void Defiance_blocks_a_resolution_and_costs_full_membership_until_the_member_obeys()
    {
        var sim = Seated();
        Open(sim, "votes:vote_universal_suffrage");
        Cast(sim, 0, VoteChoice.Yes); Cast(sim, 1, VoteChoice.Yes); Cast(sim, 2, VoteChoice.Defy);
        Close(sim);
        Assert.DoesNotContain("government:democracy", sim.State.PlayerById(0).Civics.Values);
        Assert.False(Votes(sim).IsFullMember(sim, 2, Un));
        Assert.True(Votes(sim).IsVotingMember(sim, 2, Un));

        // Backing a resolution that passes makes the member whole again.
        AwaitProposal(sim);
        Pass(sim, "votes:vote_free_trade");
        Assert.True(Votes(sim).IsFullMember(sim, 2, Un));
    }

    [Fact]
    public void Sanctions_on_a_defiant_member_stop_its_trade_with_every_member_until_it_obeys()
    {
        var sim = Seated();
        Assert.Contains("full member", Reject(sim, Propose(0, "votes:vote_un_force_no_trade", 2)));
        CouncilState(sim).Defiant.Add(2);
        Pass(sim, "votes:vote_un_force_no_trade", 2, voters: [0, 1]);

        int team0 = sim.TeamOf(0).Id, team1 = sim.TeamOf(1).Id, team2 = sim.TeamOf(2).Id;
        Assert.False(sim.Rules().Relations.MayTrade(sim, team0, team2));
        Assert.False(sim.Rules().Relations.MayTrade(sim, team1, team2));
        Assert.True(sim.Rules().Relations.MayTrade(sim, team0, team1));
        Assert.Equal(1, sim.Rules().Relations.Memories(sim, 2, 0).GetValueOrDefault(DiplomacyMemories.StoppedTrading));
        Assert.Contains("embargo", Reject(sim, new ProposeDealCommand { Player = 0, Target = 2, Items = [new(0, TradeItemKind.Gold, Amount: 5)] }));

        CouncilState(sim).Defiant.Remove(2);
        sim.AdvanceTurn();
        Assert.True(sim.Rules().Relations.MayTrade(sim, team0, team2));
    }

    [Fact]
    public void Peacekeeping_ends_every_member_war_on_the_named_player()
    {
        var sim = Seated();
        sim.Rules().Relations.DeclareWar(sim, 1, 2);
        Pass(sim, "votes:vote_un_force_peace", 2, voters: [0, 1, 2]);
        Assert.Empty(sim.TeamOf(2).Wars);
    }

    [Fact]
    public void A_victory_vote_wins_the_game_for_the_elected_team()
    {
        var sim = Seated();
        Open(sim, "votes:vote_victory");
        Cast(sim, 0, VoteChoice.Yes, sim.TeamOf(0).Id); Cast(sim, 1, VoteChoice.Yes, sim.TeamOf(0).Id);
        Close(sim);
        var status = sim.Service<IProjectVictoryService>()!.GetVictoryStatus(sim);
        Assert.True(status.IsGameOver);
        Assert.Equal(sim.TeamOf(0).Id, status.WinningTeamId);
        Assert.Equal("projects_victory:diplomatic", status.VictoryId);
    }

    [Fact]
    public void Computer_players_elect_a_secretary_who_puts_a_resolution_that_passes()
    {
        var sim = Scenario();
        var decided = new List<ResolutionDecided>();
        for (int turn = 0; turn < 40 && decided.Count(d => d.Resolution.Vote != "votes:vote_secretary_general") == 0; turn++)
        {
            foreach (var player in sim.State.Players) sim.Content.PlayerAIs.Get("ai:standard").Decide(sim, player.Id).ToArray();
            sim.AdvanceTurn();
            decided.AddRange(sim.Events.PreviousTurn.OfType<ResolutionDecided>());
        }
        Assert.Contains(decided, d => d.Resolution.Vote == "votes:vote_secretary_general" && d.Passed);
        Assert.NotNull(Votes(sim).Council(sim, Un)!.Secretary);
        Assert.Contains(decided, d => d.Resolution.Vote != "votes:vote_secretary_general" && d.Passed);
    }

    [Fact]
    public void Without_victories_a_council_still_elects_and_sanctions()
    {
        var sim = Seated(Alone.Value);
        Assert.Null(sim.Rules().VictoryConditions);
        Assert.DoesNotContain(Votes(sim).Proposals(sim, Un), r => r.Vote == "votes:vote_victory");
        Assert.Contains("cannot be won by vote", Reject(sim, Propose(0, "votes:vote_victory")));
        Pass(sim, "votes:vote_free_trade");
        Assert.True(sim.Rules().Relations.FreeTrade(sim, sim.TeamOf(1).Id, sim.TeamOf(2).Id));

        AwaitProposal(sim);
        CouncilState(sim).Defiant.Add(2);
        Pass(sim, "votes:vote_un_force_no_trade", 2, voters: [0, 1]);
        Assert.False(sim.Rules().Relations.MayTrade(sim, sim.TeamOf(0).Id, sim.TeamOf(2).Id));
    }

    [Fact]
    public void A_council_mid_session_survives_a_save_and_reload()
    {
        var sim = Seated();
        Pass(sim, "votes:vote_free_trade");
        AwaitProposal(sim);
        CouncilState(sim).Defiant.Add(2);
        Pass(sim, "votes:vote_un_force_no_trade", 2, voters: [0, 1]);
        AwaitProposal(sim);
        Open(sim, "votes:vote_universal_suffrage");
        Cast(sim, 0, VoteChoice.Yes);

        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        var council = Votes(loaded).Council(loaded, Un)!;
        Assert.Equal(sim.TeamOf(0).Id, council.Secretary);
        Assert.Equal(["votes:vote_free_trade"], council.Standing);
        Assert.Equal([sim.TeamOf(2).Id], council.Sanctioned);
        Assert.Equal([2], council.Defiant);
        Assert.Equal(new Ballot(VoteChoice.Yes), council.Session!.Ballots[0]);

        foreach (var game in new[] { sim, loaded })
        {
            Cast(game, 1, VoteChoice.Yes);
            Close(game);
        }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Contains("government:democracy", loaded.State.PlayerById(1).Civics.Values);
    }

    [Fact]
    public void The_councils_screen_takes_the_players_ballot_and_the_secretarys_resolution()
    {
        var sim = Scenario();
        var session = new GameSession(sim);
        Assert.Null(ScreenProbe.Button(session, Screen));
        sim.AdvanceTurn();
        Assert.NotNull(ScreenProbe.Button(session, Screen));

        int myTeam = sim.TeamOf(0).Id;
        string elect = $"{Un}:elect:{myTeam}";
        Assert.True(ScreenProbe.Press(session, Screen, elect));
        Assert.Equal(new Ballot(VoteChoice.Yes, myTeam), Votes(sim).Council(sim, Un)!.Session!.Ballots[0]);
        Assert.True(ViewWalk.Find<ActionButton>(ScreenProbe.Body(session, Screen), elect)!.Selected);
        Cast(sim, 1, VoteChoice.Yes, myTeam);
        sim.AdvanceTurn();
        AwaitProposal(sim);
        foreach (var city in sim.State.Cities) city.Population = 3;

        Assert.Contains($"{Un}:propose:votes:vote_free_trade:-1", ScreenProbe.ButtonIds(ScreenProbe.Body(session, Screen), $"{Un}:propose:"));
        Assert.True(ScreenProbe.Press(session, Screen, $"{Un}:propose:votes:vote_free_trade:-1"));
        Assert.Equal("votes:vote_free_trade", Votes(sim).Council(sim, Un)!.Session!.Resolution.Vote);
        Assert.NotNull(ViewWalk.Find<ActionButton>(ScreenProbe.Body(session, Screen), $"{Un}:defy"));
    }

    private const string Screen = "votes:councils";

    private static IVotesService Votes(Simulation sim) => sim.Service<IVotesService>()!;
    private static CouncilState CouncilState(Simulation sim) => sim.State.Extensions.Get<VotesGameState>(sim.Content)!.Councils[Un];
    private static int Player(Simulation sim, int team) => sim.State.TeamById(team).Members.Min();

    private static CastVoteCommand Ballot(int player, VoteChoice choice, int candidate = -1) =>
        new() { Player = player, Source = Un, Choice = choice, Candidate = candidate };
    private static ProposeResolutionCommand Propose(int player, string vote, int target = -1) =>
        new() { Player = player, Source = Un, Vote = vote, Target = target };
    private static void Cast(Simulation sim, int player, VoteChoice choice, int candidate = -1) => Apply(sim, Ballot(player, choice, candidate));

    private static void ElectFirstTeam(Simulation sim)
    {
        Cast(sim, 0, VoteChoice.Yes, sim.TeamOf(0).Id);
        Cast(sim, 1, VoteChoice.Yes, sim.TeamOf(0).Id);
        Cast(sim, 2, VoteChoice.Abstain);
        sim.AdvanceTurn();
    }

    /// <summary>A resolution put during a turn is voted on through the next one and closes at its end.</summary>
    private static void Close(Simulation sim)
    {
        int opened = Votes(sim).Council(sim, Un)!.Session!.OpenedTurn;
        while (Votes(sim).Council(sim, Un)!.Session is { } session && session.OpenedTurn == opened) sim.AdvanceTurn();
    }

    /// <summary>A sitting council whose secretary (player 0's team) has just been asked for a resolution.</summary>
    private static Simulation Seated(ContentDb? content = null)
    {
        var sim = Scenario(content: content);
        sim.AdvanceTurn();
        ElectFirstTeam(sim);
        AwaitProposal(sim);
        // Unworked cities shrank while the council waited; equal members keep every vote open.
        foreach (var city in sim.State.Cities) city.Population = 3;
        return sim;
    }

    /// <summary>Advances until the council next asks its secretary for a resolution.</summary>
    private static void AwaitProposal(Simulation sim)
    {
        for (int turn = 0; turn < 40 && !Votes(sim).Council(sim, Un)!.AwaitingProposal; turn++) sim.AdvanceTurn();
        Assert.True(Votes(sim).Council(sim, Un)!.AwaitingProposal);
    }

    private static void Open(Simulation sim, string vote, int target = -1) => Apply(sim, Propose(0, vote, target));

    private static void Pass(Simulation sim, string vote, int target = -1, int[]? voters = null)
    {
        Open(sim, vote, target);
        foreach (int player in voters ?? [0, 1, 2]) Cast(sim, player, VoteChoice.Yes);
        Close(sim);
    }

    private static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);
    private static string Reject(Simulation sim, GameCommand command)
    {
        Assert.False(sim.TryApply(command, out var error));
        return error!;
    }

    /// <summary>Three civilizations without leaders that have met, with a city of three citizens each and agreement
    /// trading unlocked; player 0's city holds the United Nations unless <paramref name="seat"/> is false.</summary>
    private static Simulation Scenario(bool seat = true, ContentDb? content = null)
    {
        var sim = Simulation.CreateScenario(content ?? Content.Value, GameFactory.Settings(73, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar", "peoples:leader_bismarck"), new(24, 10, true),
            Enumerable.Range(0, 240).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
            {
                foreach (var plot in game.State.Plots) foreach (var team in game.State.Teams) plot.Revealed.Add(team.Id);
            });
        GameFactory.NeutralPolitics(sim);
        foreach (var player in sim.State.Players) player.Treasury = 100;
        foreach (var team in sim.State.Teams)
            foreach (var tech in sim.Content.Get<TechDef>().All.Where(t => t.GoldTrading || t.OpenBordersTrading)) team.Techs.Add(tech.Id);
        foreach (var (owner, column) in new[] { (0, 4), (1, 9), (2, 13) })
        {
            var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = HexGrid.FromOffset(column, 4), Population = 3 };
            if (seat && owner == 0) city.Buildings.Add("cities:united_nations");
            sim.State.AddCity(city);
            sim.State.PlotAt(city.Position).Owner = owner;
        }
        foreach (var (a, b) in new[] { (0, 1), (0, 2), (1, 2) })
            sim.Rules().Relations.EstablishContact(sim, sim.TeamOf(a).Id, sim.TeamOf(b).Id);
        return sim;
    }
}
