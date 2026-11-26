using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.ProjectsVictory;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Team and world projects and the space race they win, which the mod adds to the base game's victories.
/// Loads src/Mods/ProjectsVictory directly from its own build output, so it needs no test project reference.</summary>
public sealed class ProjectsVictoryRulesTests
{
    private const string Speed = "foundation:gamespeed_normal";
    private static readonly Lazy<ContentDb> Content = new(() =>
    {
        var loaded = new LoadedMods();
        var source = ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "ProjectsVictory"));
        return ModLoader.Build(loaded.Sources.Append(source));
    });
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static IProjectService Pv(Simulation sim) => Service<IProjectService>(sim);

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
    private static VictoryStatus Status(Simulation sim) => sim.Rules().Victory.Status(sim);

    [Fact]
    public void A_single_copy_project_leaves_the_choices_while_a_multi_copy_one_does_not()
    {
        var sim = Scenario();
        var session = new GameSession(sim);
        var city = sim.State.Cities[0];
        Learn(sim, 0, "research:fission", "research:composites");
        Learn(sim, 1, "research:fission");
        Pv(sim).CompleteProject(sim, city.Id, "projects_victory:apollo_program");
        const string kind = "projects_victory:project";

        // The Manhattan Project can be built once in the world, so a queued copy uses up the choice.
        Assert.True(session.Enqueue(city, kind, "projects_victory:manhattan_project", append: true));
        Assert.Equal(["Already in queue"], Assert.Single(session.ProductionChoices(city, kind), c => c.Id == "projects_victory:manhattan_project").Blockers);

        // Space Casing allows five copies per team, so copies may be queued in parallel; the team may queue every copy
        // it still has room for, and not one more.
        for (int i = 0; i < 5; i++) Assert.True(session.Enqueue(city, kind, "projects_victory:ss_casing", append: true));
        Assert.Equal(5, city.ProductionQueue.Count(o => o.ContentId == "projects_victory:ss_casing"));
        Assert.False(session.Enqueue(city, kind, "projects_victory:ss_casing", append: true));
        Assert.Equal(["Already in queue"], Choice("projects_victory:ss_casing").Blockers);

        // A single-copy project queued in one city also covers the player's other cities; another player still races.
        var samePlayer = AddCity(sim, 0, 40); var rival = AddCity(sim, 1, 20);
        Assert.Equal(["Already queued in another city"],
            Assert.Single(session.ProductionChoices(samePlayer, kind), c => c.Id == "projects_victory:manhattan_project").Blockers);
        Assert.True(Assert.Single(session.ProductionChoices(rival, kind), c => c.Id == "projects_victory:manhattan_project").Available);

        // Four completed copies leave room for the fifth only, so an empty queue offers it again and a queued copy uses
        // that last room up.
        for (int i = 0; i < 4; i++) Pv(sim).CompleteProject(sim, city.Id, "projects_victory:ss_casing");
        while (city.ProductionQueue.Count > 0) Assert.True(session.RemoveFromQueue(city, 0));
        Assert.True(Assert.Single(session.ProductionChoices(city, kind), c => c.Id == "projects_victory:ss_casing").Available);
        Assert.True(session.Enqueue(city, kind, "projects_victory:ss_casing", append: true));
        Assert.Equal(["Already in queue"], Choice("projects_victory:ss_casing").Blockers);

        ProductionChoice Choice(string id) => Assert.Single(session.ProductionChoices(city, kind), c => c.Id == id);
    }

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
        Assert.Single(sim.Events.Published.OfType<ProjectCompleted>(), e => e.Project == manhattan);
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
        // The shipped Internet costs 2000, scaled by normal speed's 176 % for projects: 2000 * 176 / 100 = 3520.
        Assert.Equal(3520, kind.Calculate(sim, city.Id, internet).Cost);
        city.ProductionQueue.Add(new("projects_victory:project", internet, 100000));
        sim.AdvanceTurn();
        Assert.Equal(1, Pv(sim).TeamCount(sim, 0, internet));
        Assert.Empty(city.ProductionQueue);
        Assert.False(kind.Validate(sim, city.Id, internet).Success);
    }
    private static IProductionKind Service<T>(Simulation sim, string id) where T : class => sim.Content.Services.Get<IProductionKind>().Single(k => k.Id == id);
    private static IProductionKind Service(Simulation sim, string id) => Service<IProductionKind>(sim, id);

    [Fact]
    public void Space_race_victory_never_names_an_eliminated_teammate()
    {
        var sim = Scenario();
        VictoryRulesTests.JoinTeam(sim, 2, 0);
        foreach (var plot in sim.State.Plots) plot.Owner = 2;
        sim.State.Cities[1].Population = 10; // Keep domination from ending the game first.
        var city = sim.State.Cities[0];
        var parts = new[] { "apollo_program", "ss_cockpit", "ss_life_support", "ss_stasis_chamber", "ss_docking_bay", "ss_engine", "ss_casing", "ss_thrusters" };
        foreach (var part in parts) Pv(sim).CompleteProject(sim, city.Id, "projects_victory:" + part);

        var status = sim.Rules().Victory.Evaluate(sim, true);
        Assert.Equal("projects_victory:space_race", status.VictoryId);
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
    public void The_space_race_is_a_victory_condition_beside_the_base_games()
    {
        var sim = Scenario();
        Assert.Contains(sim.Rules().VictoryConditions, c => c.Id == ProjectVictories.Spaceship);
        Assert.True(sim.Rules().Victory.IsEnabled(sim, "foundation:victory_space_race"));
        Assert.True(sim.Rules().Victory.IsEnabled(sim, "foundation:victory_conquest"));
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
