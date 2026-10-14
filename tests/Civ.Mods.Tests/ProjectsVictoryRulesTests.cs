using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

/// <summary>Loads src/Mods/ProjectsVictory directly from its own build output, so it needs no test project reference.</summary>
public sealed class ProjectsVictoryRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Speed = "foundation:gamespeed_normal";
    private static readonly Lazy<ModSource> Mine = new(() =>
        ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "ProjectsVictory")));
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static IProjectVictoryService Pv(Simulation sim) => Service<IProjectVictoryService>(sim);

    /// <summary>Two teams, one city each, half of the land each. No rule is satisfied at the start.</summary>
    private Simulation Scenario()
    {
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(Mine.Value)));
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
    private static VictoryStatus Status(Simulation sim) => Pv(sim).GetVictoryStatus(sim);

    [Fact]
    public void Project_eligibility_follows_tech_prerequisites_and_team_and_world_limits()
    {
        var sim = Scenario(); var pv = Pv(sim);
        var city0 = sim.State.Cities[0]; var city1 = sim.State.Cities[1];
        const string manhattan = "projects_victory:manhattan_project", sdi = "projects_victory:sdi";
        Assert.Contains(pv.ValidateProject(sim, city0.Id, manhattan).Failures, f => f.Explanation.StartsWith("Requires technology"));
        Learn(sim, 0, "research:fission", "research:laser"); Learn(sim, 1, "research:fission", "research:laser");
        Assert.True(pv.ValidateProject(sim, city0.Id, manhattan).Success);
        Assert.Contains(pv.ValidateProject(sim, city0.Id, sdi).Failures, f => f.Explanation.StartsWith("Requires project built"));
        pv.CompleteProject(sim, city0.Id, manhattan);
        Assert.Equal(1, pv.TeamCount(sim, 0, manhattan)); Assert.Equal(1, pv.WorldCount(sim, manhattan));
        Assert.Contains(pv.ValidateProject(sim, city1.Id, manhattan).Failures, f => f.Explanation == "WorldLimitReached");
        Assert.True(pv.ValidateProject(sim, city1.Id, sdi).Success);
        pv.CompleteProject(sim, city1.Id, sdi);
        Assert.Contains(pv.ValidateProject(sim, city1.Id, sdi).Failures, f => f.Explanation == "TeamLimitReached");
        Assert.True(pv.ValidateProject(sim, city0.Id, sdi).Success); // the limit is per team
        Assert.Single(sim.Events.Published.OfType<ProjectCompleted>().Where(e => e.Project == manhattan));
    }

    [Fact]
    public void Project_is_listed_as_a_production_kind_and_completes_through_the_city_queue()
    {
        var sim = Scenario(); var city = sim.State.Cities[0];
        const string internet = "projects_victory:the_internet";
        var kind = Service<IProductionKind>(sim, "projects_victory:project");
        Assert.Contains(kind.Candidates(sim.Content), d => d.Id == internet);
        Learn(sim, 0, "research:computers");
        Assert.True(kind.Validate(sim, city.Id, internet).Success);
        Assert.Equal(2000L * sim.Content.Get<S.GameSpeedDef>(Speed).CreatePercent / 100, kind.Calculate(sim, city.Id, internet).Cost);
        city.ProductionQueue.Add(new("projects_victory:project", internet, 100000));
        sim.AdvanceTurn();
        Assert.Equal(1, Pv(sim).TeamCount(sim, 0, internet));
        Assert.Empty(city.ProductionQueue);
        Assert.False(kind.Validate(sim, city.Id, internet).Success);
    }
    private static IProductionKind Service<T>(Simulation sim, string id) where T : class => sim.Content.Services.Get<IProductionKind>().Single(k => k.Id == id);
    private static IProductionKind Service(Simulation sim, string id) => Service<IProductionKind>(sim, id);

    [Fact]
    public void Score_breakdown_counts_population_land_techs_and_world_wonders()
    {
        var sim = Scenario();
        sim.State.Cities[0].Population = 3;
        sim.State.Cities[0].Buildings.Add("cities:abu_simbel");
        Learn(sim, 0, "research:fission", "research:laser");
        var score = Pv(sim).GetScore(sim, 0);
        Assert.Equal(3, score.Population);
        Assert.Equal(48, score.Land);
        Assert.Equal(2, score.Techs);
        Assert.Equal(1, score.Wonders);
        Assert.Equal(3 * 2 + 48 * 1 + 2 * 4 + 1 * 5, score.Total);
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
        Assert.Equal("projects_victory:conquest", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId); Assert.Equal(0, status.WinningPlayerId);
        Assert.Contains(sim.Events.PreviousTurn.Concat(sim.Events.Published).OfType<VictoryAchieved>(), e => e.Victory == "projects_victory:conquest");
        int won = status.TurnWon;
        sim.AdvanceTurn();
        Assert.Equal(won, Status(sim).TurnWon); // the first outcome is final
    }

    [Fact]
    public void Domination_victory_needs_both_land_and_population_shares()
    {
        var sim = Scenario();
        foreach (var plot in sim.State.Plots) plot.Owner = 0;
        sim.State.Cities[1].Population = 3; // the rival holds 75% of the people
        Assert.False(Pv(sim).Evaluate(sim, true).IsGameOver);
        sim.State.Cities[0].Population = 3; // 50% each
        Assert.False(Pv(sim).Evaluate(sim, true).IsGameOver);
        sim.State.Cities[0].Population = 10;
        var status = Pv(sim).Evaluate(sim, true);
        Assert.Equal("projects_victory:domination", status.VictoryId);
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
        Assert.False(Pv(sim).Evaluate(sim, true).IsGameOver);
        b.Culture = legendary;
        var status = Pv(sim).Evaluate(sim, true);
        Assert.Equal("projects_victory:cultural", status.VictoryId);
        Assert.Equal(0, status.WinningPlayerId);
    }

    [Fact]
    public void Cultural_tie_goes_to_the_team_with_the_higher_combined_score()
    {
        var sim = Scenario();
        long legendary = sim.Content.Get<S.CultureLevelDef>("foundation:culturelevel_legendary").SpeedThresholds[Speed];
        sim.State.Cities[0].Culture = legendary; AddCity(sim, 0, 20).Culture = legendary; AddCity(sim, 0, 30).Culture = legendary;
        sim.State.Cities[1].Culture = legendary; AddCity(sim, 1, 40, population: 10).Culture = legendary; AddCity(sim, 1, 50, population: 10).Culture = legendary;
        var status = Pv(sim).Evaluate(sim, true);
        Assert.Equal("projects_victory:cultural", status.VictoryId);
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
        var status = Pv(sim).Evaluate(sim, true);
        Assert.Equal("projects_victory:cultural", status.VictoryId);
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
        Assert.Equal("projects_victory:time", status.VictoryId);
        Assert.Equal(1, status.WinningPlayerId);
        Assert.Equal(max, status.TurnWon);
    }

    /// <summary>Adds a civilization to an existing team.</summary>
    private static void JoinTeam(Simulation sim, int player, int team)
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
        Assert.Equal("projects_victory:conquest", status.VictoryId);
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
        Assert.Equal("projects_victory:time", status.VictoryId);
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
        var status = Pv(sim).Evaluate(sim, true);
        Assert.Equal("projects_victory:time", status.VictoryId);
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

        var status = Pv(sim).Evaluate(sim, true);
        Assert.Equal("projects_victory:domination", status.VictoryId);
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

        var status = Pv(sim).Evaluate(sim, true);
        Assert.Equal("projects_victory:cultural", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId);
        Assert.Equal(0, status.WinningPlayerId);
    }

    [Fact]
    public void Space_race_victory_never_names_an_eliminated_teammate()
    {
        var sim = Scenario();
        JoinTeam(sim, 2, 0);
        foreach (var plot in sim.State.Plots) plot.Owner = 2;
        sim.State.Cities[1].Population = 10; // Keep domination from ending the game first.
        var city = sim.State.Cities[0];
        var parts = new[] { "apollo_program", "ss_cockpit", "ss_life_support", "ss_stasis_chamber", "ss_docking_bay", "ss_engine", "ss_casing", "ss_thrusters" };
        foreach (var part in parts) Pv(sim).CompleteProject(sim, city.Id, "projects_victory:" + part);

        var status = Pv(sim).Evaluate(sim, true);
        Assert.Equal("projects_victory:space_race", status.VictoryId);
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
        Assert.Equal("projects_victory:time", status.VictoryId);
        Assert.Equal(0, status.WinningTeamId);
        Assert.Equal(0, status.WinningPlayerId);
    }

    [Fact]
    public void Space_race_victory_when_a_team_completes_the_required_spaceship_projects()
    {
        var sim = Scenario(); var pv = Pv(sim); var city = sim.State.Cities[1];
        var parts = new[] { "apollo_program", "ss_cockpit", "ss_life_support", "ss_stasis_chamber", "ss_docking_bay", "ss_engine", "ss_casing" };
        foreach (var part in parts) pv.CompleteProject(sim, city.Id, "projects_victory:" + part);
        Assert.False(Status(sim).IsGameOver);
        pv.CompleteProject(sim, city.Id, "projects_victory:ss_thrusters");
        var status = Status(sim);
        Assert.Equal("projects_victory:space_race", status.VictoryId);
        Assert.Equal(1, status.WinningTeamId);
        Assert.Contains(pv.ValidateProject(sim, city.Id, "projects_victory:ss_cockpit").Failures, f => f.Explanation == "The game is over");
    }

    [Fact]
    public void Saved_games_resume_with_identical_project_victory_state_and_hash()
    {
        var sim = Scenario();
        Pv(sim).CompleteProject(sim, sim.State.Cities[0].Id, "projects_victory:apollo_program");
        sim.State.Turn = Status(sim).MaxTurns;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(1, Pv(loaded).TeamCount(loaded, 0, "projects_victory:apollo_program"));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.True(Status(sim).IsGameOver);
        Assert.Equal(Status(sim), Status(loaded));
        var again = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), again.ComputeHash());
        Assert.Equal(Status(sim), Status(again));
    }
}
