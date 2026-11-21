using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Score and the base game's victories, on the base modules alone: no feature mod is loaded, so the base game
/// keeps score and ends a game by itself.</summary>
public sealed class VictoryRulesTests
{
    private const string Speed = "foundation:gamespeed_normal";
    private static readonly Lazy<ContentDb> Content = new(() =>
        GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), []));
    private static IVictoryService Victory(Simulation sim) => sim.Rules().Victory;

    /// <summary>Two teams, one city each, half of the land each. No rule is satisfied at the start.</summary>
    private Simulation Scenario()
    {
        var sim = GameFactory.Scenario(Content.Value);
        GameFactory.NeutralPolitics(sim);
        for (int i = 0; i < sim.State.Plots.Length; i++) sim.State.Plots[i].Owner = i % 2;
        AddCity(sim, 0, 14); AddCity(sim, 1, 57);
        return sim;
    }
    private static City AddCity(Simulation sim, int owner, int tile, int population = 1)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = population };
        sim.State.AddCity(city);
        return city;
    }
    private static void Learn(Simulation sim, int player, params string[] techs) { foreach (var t in techs) sim.TeamOf(player).Techs.Add(t); }
    private static VictoryStatus Status(Simulation sim) => Victory(sim).Status(sim);

    [Fact]
    public void The_base_game_alone_runs_the_four_victories_and_keeps_score()
    {
        var content = Content.Value;
        Assert.DoesNotContain(content.Mods, m => !BaseGame.IsBase(m.Id));
        Assert.Equal(["victory:conquest", "victory:cultural", "victory:domination", "victory:time"],
            content.Get<S.VictoryDef>().All.Select(v => v.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["victory:conquest", "victory:domination", "victory:cultural", "victory:time"],
            content.Rules().VictoryConditions.Select(c => c.Id));
        var sim = Scenario();
        Assert.True(Victory(sim).IsEnabled(sim, "foundation:victory_conquest"));
        // The diplomatic victory and the space race belong to the mods that decide them.
        Assert.False(Victory(sim).IsEnabled(sim, "foundation:victory_diplomatic"));
        Assert.False(Victory(sim).IsEnabled(sim, "foundation:victory_space_race"));
        Assert.Equal(48 + 2, sim.Rules().Scores.Score(sim, 0));
    }

    [Fact]
    public void Score_breakdown_counts_population_land_techs_and_world_wonders()
    {
        var sim = Scenario();
        sim.State.Cities[0].Population = 3;
        sim.State.Cities[0].Buildings.Add("cities:abu_simbel");
        Learn(sim, 0, "research:fission", "research:laser");
        var score = sim.Rules().Scores.Breakdown(sim, 0);
        Assert.Equal(3, score.Population);
        Assert.Equal(48, score.Land);
        Assert.Equal(2, score.Techs);
        Assert.Equal(1, score.Wonders);
        Assert.Equal(3 * 2 + 48 * 1 + 2 * 4 + 1 * 5, score.Total);
        Assert.Equal(score.Total, sim.Rules().Scores.Score(sim, 0));
    }

    [Fact]
    public void Conquest_victory_when_every_rival_is_eliminated()
    {
        var sim = Scenario();
        sim.AdvanceTurn();
        Assert.False(Status(sim).IsGameOver);
        sim.State.RemoveCity(sim.State.Cities[1].Id);
        sim.AdvanceTurn();
        var status = Status(sim);
        Assert.True(status.IsGameOver);
        Assert.Equal("victory:conquest", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId); Assert.Equal(0, status.WinningPlayerId);
        Assert.Contains(sim.Events.PreviousTurn.Concat(sim.Events.Published).OfType<VictoryAchieved>(), e => e.Victory == "victory:conquest");
        int won = status.TurnWon;
        sim.AdvanceTurn();
        Assert.Equal(won, Status(sim).TurnWon); // the first outcome is final
    }

    [Fact]
    public void Play_continues_after_a_win_and_the_recorded_outcome_survives_reload()
    {
        var sim = Scenario();
        sim.State.RemoveCity(sim.State.Cities[1].Id);
        sim.AdvanceTurn();
        var won = Status(sim);
        Assert.True(won.IsGameOver);
        int turn = sim.State.Turn;

        foreach (var player in sim.State.Players.Where(p => !p.EndedTurn).ToList())
            Assert.True(sim.TrySubmit(new EndTurnCommand { Player = player.Id }, out var error), error);

        Assert.Equal(turn + 1, sim.State.Turn);
        Assert.Equal(won with { MaxTurns = Status(sim).MaxTurns }, Status(sim));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(Status(sim), Status(loaded));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.False(loaded.State.Extensions.Get<VictoryGameState>(loaded.Content)!.Record(1, 1, "victory:conquest", 99));
    }

    [Fact]
    public void Domination_victory_needs_both_land_and_population_shares()
    {
        var sim = Scenario();
        foreach (var plot in sim.State.Plots) plot.Owner = 0;
        sim.State.Cities[1].Population = 3; // the rival holds 75% of the people
        Assert.False(Victory(sim).Evaluate(sim, true).IsGameOver);
        sim.State.Cities[0].Population = 3; // 50% each
        Assert.False(Victory(sim).Evaluate(sim, true).IsGameOver);
        sim.State.Cities[0].Population = 10;
        var status = Victory(sim).Evaluate(sim, true);
        Assert.Equal("victory:domination", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId);
    }

    [Fact]
    public void Cultural_victory_needs_three_legendary_cities_in_one_team()
    {
        var sim = Scenario();
        long legendary = sim.Content.Get<S.CultureLevelDef>("foundation:culturelevel_legendary").SpeedThresholds[Speed];
        sim.State.Cities[1].Culture = legendary;
        var a = AddCity(sim, 0, 20); var b = AddCity(sim, 0, 30);
        sim.State.Cities[0].Culture = legendary; a.Culture = legendary; b.Culture = legendary - 1;
        Assert.False(Victory(sim).Evaluate(sim, true).IsGameOver);
        b.Culture = legendary;
        var status = Victory(sim).Evaluate(sim, true);
        Assert.Equal("victory:cultural", status.VictoryId);
        Assert.Equal(0, status.WinningPlayerId);
    }

    [Fact]
    public void Cultural_tie_goes_to_the_team_with_the_higher_combined_score()
    {
        var sim = Scenario();
        long legendary = sim.Content.Get<S.CultureLevelDef>("foundation:culturelevel_legendary").SpeedThresholds[Speed];
        sim.State.Cities[0].Culture = legendary; AddCity(sim, 0, 20).Culture = legendary; AddCity(sim, 0, 30).Culture = legendary;
        sim.State.Cities[1].Culture = legendary; AddCity(sim, 1, 40, population: 10).Culture = legendary; AddCity(sim, 1, 50, population: 10).Culture = legendary;
        var status = Victory(sim).Evaluate(sim, true);
        Assert.Equal("victory:cultural", status.VictoryId);
        Assert.Equal(1, status.WinningPlayerId);
    }

    [Fact]
    public void Team_victory_names_the_highest_scoring_member_not_the_lowest_id()
    {
        var sim = Scenario();
        // Both civilizations share team 0, so the team wins as a whole and only the member choice can pick the winner.
        sim.State.Players = sim.State.Players.Select(p => new Player
        {
            Id = p.Id, TeamId = 0, Civilization = p.Civilization, Name = p.Name,
            Treasury = p.Treasury, Ai = p.Ai, StartingPlot = p.StartingPlot,
        }).ToList();
        sim.State.RebuildIndexes();
        long legendary = sim.Content.Get<S.CultureLevelDef>("foundation:culturelevel_legendary").SpeedThresholds[Speed];
        sim.State.Cities[0].Culture = legendary;
        AddCity(sim, 1, 30, population: 10).Culture = legendary; AddCity(sim, 1, 40, population: 10).Culture = legendary;
        AddCity(sim, 1, 50, population: 10).Culture = legendary;
        var status = Victory(sim).Evaluate(sim, true);
        Assert.Equal("victory:cultural", status.VictoryId);
        Assert.Equal(1, status.WinningPlayerId);
    }

    [Fact]
    public void Time_victory_at_scaled_max_turns_goes_to_the_highest_score()
    {
        var sim = Scenario();
        Learn(sim, 1, "research:fission", "research:laser", "research:fusion");
        int max = Status(sim).MaxTurns;
        Assert.Equal(250 * sim.Content.Get<S.GameSpeedDef>(Speed).VictoryDelayPercent / 100, max);
        sim.State.Turn = max - 1;
        sim.AdvanceTurn();
        Assert.False(Status(sim).IsGameOver);
        Assert.Equal(max, sim.State.Turn);
        sim.AdvanceTurn();
        var status = Status(sim);
        Assert.Equal("victory:time", status.VictoryId);
        Assert.Equal(1, status.WinningPlayerId);
        Assert.Equal(max, status.TurnWon);
    }

    /// <summary>Adds a civilization to an existing team.</summary>
    internal static void JoinTeam(Simulation sim, int player, int team)
    {
        sim.State.Players.Add(new Player { Id = player, TeamId = team, Treasury = 0 });
        sim.State.Teams.Single(t => t.Id == team).Members.Add(player);
        sim.State.RebuildIndexes();
    }

    [Fact]
    public void Conquest_names_the_highest_scoring_survivor_of_the_winning_team()
    {
        var sim = Scenario();
        JoinTeam(sim, 2, 0);
        AddCity(sim, 2, 20, population: 30); // Player 2 scores 60 against player 0's 50.
        sim.State.RemoveCity(sim.State.Cities.Single(c => c.Owner == 1).Id);
        sim.AdvanceTurn();
        var status = Status(sim);
        Assert.Equal("victory:conquest", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId);
        Assert.Equal(2, status.WinningPlayerId);
    }

    [Fact]
    public void Time_victory_ranks_teams_by_combined_score_and_names_the_best_member()
    {
        var sim = Scenario();
        JoinTeam(sim, 2, 0);
        // Team 0 totals 70 (player 0 at 58, player 2 at 12) against team 1's 68, yet player 1 alone scores highest.
        sim.State.Cities[0].Population = 5;
        sim.State.Cities.Single(c => c.Owner == 1).Population = 10;
        AddCity(sim, 2, 20, population: 6);
        int max = Status(sim).MaxTurns;
        sim.State.Turn = max - 1;
        sim.AdvanceTurn();
        Assert.False(Status(sim).IsGameOver);
        Assert.Equal(max, sim.State.Turn);
        sim.AdvanceTurn();
        var status = Status(sim);
        Assert.Equal("victory:time", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId);
        Assert.Equal(0, status.WinningPlayerId);
    }

    [Fact]
    public void Team_score_counts_shared_technologies_once_per_team()
    {
        var sim = Scenario();
        // Alternating borders would add land to both teams, so only population and technology decide here.
        foreach (var plot in sim.State.Plots) plot.Owner = null;
        JoinTeam(sim, 2, 0);
        Learn(sim, 0, "research:fission", "research:laser", "research:fusion");
        sim.State.Cities.Single(c => c.Owner == 0).Population = 1; // 2 points
        sim.State.Cities.Single(c => c.Owner == 1).Population = 10; // 20 points
        // Team 0 scores 2 + 3 techs x 4 = 14. Counting its techs once per member would wrongly give 26.
        sim.State.Turn = Status(sim).MaxTurns;
        var status = Victory(sim).Evaluate(sim, true);
        Assert.Equal("victory:time", status.VictoryId);
        Assert.Equal(1, status.WinningTeamId);
        Assert.Equal(1, status.WinningPlayerId);
    }

    [Fact]
    public void Domination_victory_never_names_an_eliminated_teammate()
    {
        var sim = Scenario();
        JoinTeam(sim, 2, 0);
        foreach (var plot in sim.State.Plots) plot.Owner = 2;
        sim.State.Cities[0].Population = 10;
        sim.State.Cities[1].Population = 3;

        var status = Victory(sim).Evaluate(sim, true);
        Assert.Equal("victory:domination", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId);
        Assert.Equal(0, status.WinningPlayerId);
    }

    [Fact]
    public void Cultural_victory_never_names_an_eliminated_teammate()
    {
        var sim = Scenario();
        JoinTeam(sim, 2, 0);
        foreach (var plot in sim.State.Plots) plot.Owner = null;
        var land = sim.State.Plots.Where(p => !sim.Content.Get<S.TerrainDef>(p.Terrain).IsWater).ToArray();
        foreach (var plot in land.Take(land.Length * 55 / 100)) plot.Owner = 2;
        foreach (var plot in land.Skip(land.Length * 55 / 100)) plot.Owner = 1;

        long legendary = sim.Content.Get<S.CultureLevelDef>("foundation:culturelevel_legendary").SpeedThresholds[Speed];
        sim.State.Cities[0].Culture = legendary;
        AddCity(sim, 0, 20).Culture = legendary;
        AddCity(sim, 0, 30).Culture = legendary;

        var status = Victory(sim).Evaluate(sim, true);
        Assert.Equal("victory:cultural", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId);
        Assert.Equal(0, status.WinningPlayerId);
    }

    [Fact]
    public void Time_victory_never_names_an_eliminated_teammate()
    {
        var sim = Scenario();
        JoinTeam(sim, 3, 0); // Player 3 has no city or unit, but its land outscores its living teammate.
        foreach (var plot in sim.State.Plots) plot.Owner = 3;
        int max = Status(sim).MaxTurns;
        sim.State.Turn = max - 1;
        sim.AdvanceTurn();
        Assert.False(Status(sim).IsGameOver);
        sim.AdvanceTurn();
        var status = Status(sim);
        Assert.Equal("victory:time", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId);
        Assert.Equal(0, status.WinningPlayerId);
    }

    [Fact]
    public void Saved_games_resume_with_identical_victory_state_and_hash()
    {
        var sim = Scenario();
        sim.State.Turn = Status(sim).MaxTurns;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.True(Status(sim).IsGameOver);
        Assert.Equal(Status(sim), Status(loaded));
        var again = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), again.ComputeHash());
        Assert.Equal(Status(sim), Status(again));
    }

    [Fact]
    public void A_victory_naming_an_unregistered_condition_fails_to_load()
    {
        var rules = Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder);
        var probe = new Ariel.Modding.ModSource
        {
            Manifest = new() { Id = "victory_probe", Version = "1", References = ["foundation"] },
            Root = rules, Instance = new UnknownCondition(),
        };
        var error = Assert.ThrowsAny<Exception>(() => GameContent.Build(rules, [probe]));
        Assert.Contains("victory_probe:nothing", error.ToString());
    }

    private sealed class UnknownCondition : Ariel.Modding.IMod
    {
        public void Register(Ariel.Modding.IModContext context) =>
            context.Content<S.VictoryDef>().Register(new() { Id = "victory_probe:unknown", Name = "Unknown", Rule = "victory_probe:nothing" });
    }
}
