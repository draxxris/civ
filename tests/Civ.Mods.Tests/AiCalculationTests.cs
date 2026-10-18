using System.Collections;
using System.Reflection;
using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class AiCalculationTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void City_production_preserves_decisions_hash_and_rng(bool inWave, bool reload)
    {
        var sim = Scenario(new(24, 10, true), game =>
        {
            game.State.Turn = 60;
            GameFactory.NeutralPolitics(game);
            foreach (int column in new[] { 2, 10, 18 })
            {
                var city = AddCity(game, 0, column, 4);
                city.ProductionQueue.Add(new("economy:process", "economy:process_wealth"));
                foreach (var tile in game.State.Grid.Range(game.State.Grid.TileAt(city.Position), 2))
                    game.State.Plots[tile.Value].Owner = 0;
                for (int i = 0; i < 3; i++) AddUnit(game, 0, "military:warrior", column, 4);
            }
            AddUnit(game, 0, "military:worker", 2, 4);
        });
        if (reload) sim = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        sim.IsParallelAiTurn = inWave;
        var rng = sim.Rng.State;
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var army = NewAi(sim, "ArmyPlan", ctx);
        Call(NewAi(sim, "CitizenPlanner", ctx, army), "Plan");
        Call(NewAi(sim, "CityPlanner", ctx, NewAi(sim, "SitePlanner", ctx), army), "Plan");
        // Recorded on the unoptimized implementation, using only these constructed cities and planners.
        Assert.Equal(new[] { "1:cities:unit/military:worker", "2:cities:unit/military:worker",
                "3:cities:building/cities:elder_council" },
            sim.State.Cities.SelectMany(c => c.ProductionQueue.Select(q => $"{c.Id}:{q.Kind}/{q.ContentId}")));
        Assert.Equal(501270837889495915UL, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(11, true)]
    [InlineData(2, true)]
    public void Radius_queries_match_scans_at_edges_and_wrap_seams(int width, bool wrap)
    {
        var settings = GameFactory.Settings(71, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar",
            "peoples:leader_bismarck", "peoples:leader_gandhi") with { PlayerTeams = [0, 1, 1, 0] };
        var sim = Scenario(new(width, 8, wrap), game =>
        {
            game.State.SetWar(0, 1, true);
            AddUnit(game, 0, "military:warrior", 0, 3);
            AddUnit(game, 1, "military:warrior", width - 1, 3);
            AddUnit(game, 1, "military:settler", width - 1, 3);
            AddUnit(game, 2, "military:javelineer", 0, 5);
            AddUnit(game, 3, "military:javelineer", 0, 3);
        }, settings);
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var threat = Method<Func<Hex, int, int>>(ctx, "ThreatAround");
        var hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        // First access is concurrent, including duplicate queries against the same lazy index.
        Parallel.ForEach(sim.State.Grid.Tiles, tile =>
        {
            var position = sim.State.Grid.HexOf(tile);
            foreach (int radius in new[] { 0, 1, 2, 3, 7 })
            {
                int expected = ScanThreat(sim, position, radius);
                Assert.Equal(expected, threat(position, radius));
                Assert.Equal(expected, threat(position, radius));
                if (wrap) Assert.Equal(expected, threat(new(position.Q + width, position.R), radius));
            }
        });
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    [Fact]
    public void Radius_queries_follow_movement_capture_death_war_and_refresh()
    {
        var sim = Scenario(new(14, 8, true), game =>
        {
            game.State.Turn = 60;
            game.State.SetWar(0, 1, true);
            AddUnit(game, 0, "military:warrior", 0, 3);
            AddUnit(game, 1, "military:warrior", 13, 3);
            AddUnit(game, 1, "military:settler", 1, 3);
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var threat = Method<Func<Hex, int, int>>(ctx, "ThreatAround");
        var issue = Method<Func<GameCommand, bool>>(ctx, "Issue");
        var position = HexGrid.FromOffset(0, 3);
        void Check() => Assert.Equal(ScanThreat(sim, position, 1), threat(position, 1));
        Check();
        Assert.True(issue(new MoveUnitPathCommand { Player = 1, Unit = 2, Path = [HexGrid.FromOffset(12, 3)] }));
        Check();
        Assert.True(issue(new MoveUnitPathCommand { Player = 0, Unit = 1, Path = [HexGrid.FromOffset(1, 3)] }));
        Assert.DoesNotContain(sim.State.Units, u => u.Id == 3 && u.Owner == 1);
        Check();
        Assert.True(issue(new DisbandUnitCommand { Player = 1, Unit = 2 }));
        Check();
        // Direct service/state changes must be followed by an explicit segment boundary.
        var enemy = AddUnit(sim, 1, "military:warrior", 13, 3);
        Call(ctx, "Refresh");
        Check();
        enemy.Health = 0;
        Call(ctx, "Refresh");
        Check();
        enemy.Health = 100;
        Call(ctx, "Refresh");
        Check();
        var peace = (GameCommand)NewAi(sim, "MakePeaceCommand");
        peace.GetType().GetProperty("Player")!.SetValue(peace, 0);
        peace.GetType().GetProperty("Target")!.SetValue(peace, 1);
        Assert.True(issue(peace));
        Check();
    }

    [Fact]
    public void Campaign_targets_follow_war_capture_and_revelation()
    {
        var sim = Scenario(new(16, 10, true), game =>
        {
            game.State.Turn = 60;
            AddCity(game, 0, 0, 3);
            AddCity(game, 1, 15, 3);
            AddCity(game, 1, 8, 6);
        });
        var hidden = sim.State.CityById(3);
        sim.State.PlotAt(hidden.Position).Revealed.Clear();
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var targets = NewAi(sim, "CampaignTargets", ctx);
        Assert.Null(Call(targets, "SelectPrimaryTargetCity", 0));
        var war = (GameCommand)NewAi(sim, "DeclareWarCommand");
        war.GetType().GetProperty("Target")!.SetValue(war, 1);
        Assert.True(Method<Func<GameCommand, bool>>(ctx, "Issue")(war));
        Parallel.For(0, 16, _ => Assert.Equal(2, ((City)Call(targets, "SelectPrimaryTargetCity", 0)!).Id));
        sim.Content.Services.Get<ICityCaptureService>().Single().Capture(sim, 2, 0);
        Call(ctx, "Refresh");
        Assert.Null(Call(targets, "SelectPrimaryTargetCity", 0));
        sim.State.PlotAt(hidden.Position).Revealed.Add(sim.TeamOf(0).Id);
        Call(ctx, "Refresh");
        Assert.Equal(3, ((City)Call(targets, "SelectPrimaryTargetCity", 0)!).Id);
    }

    [Theory]
    [InlineData(22, false)]
    [InlineData(22, true)]
    [InlineData(2, true)]
    public void Settlement_sites_match_independent_spacing_and_score_scans(int width, bool wrap)
    {
        var sim = Scenario(new(width, 14, wrap), game =>
        {
            AddCity(game, 0, 0, 2);
            AddCity(game, 1, width - 1, 11);
            game.State.Plots[game.State.Grid.TileAt(width / 2, 6).Value].Kind = PlotKind.Hills;

        });
        // Scenario reveals all tiles after configuration; leave one genuinely unexplored candidate.
        sim.State.Plots[sim.State.Grid.TileAt(width / 2, 8).Value].Revealed.Clear();
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var sites = NewAi(sim, "SitePlanner", ctx);
        var hash = sim.ComputeHash();
        var rng = sim.Rng.State;
        Assert.Equal(ScanSites(sim), ReadSites(sites));
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
    }

    [Fact]
    public void Settlement_sites_refresh_after_two_foundings_and_a_city_capture()
    {
        var sim = Scenario(new(24, 12, true), game =>
        {
            game.State.SetWar(0, 1, true);
            AddUnit(game, 0, "military:settler", 1, 4);
            AddUnit(game, 0, "military:settler", 10, 4);
            AddCity(game, 1, 18, 7);
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var sites = NewAi(sim, "SitePlanner", ctx);
        var issue = Method<Func<GameCommand, bool>>(ctx, "Issue");
        Assert.NotEmpty(ReadSites(sites));
        Assert.Equal(ScanSites(sim), ReadSites(sites));
        foreach (int settler in new[] { 1, 2 })
        {
            Assert.True(issue(new FoundCityWithSettlerCommand { Player = 0, Settler = settler }));
            Assert.Equal(ScanSites(sim), ReadSites(sites));
        }
        sim.Content.Services.Get<ICityCaptureService>().Single().Capture(sim, 1, 0);
        Call(ctx, "Refresh");
        Assert.Equal(3, sim.State.Cities.Count(c => c.Owner == 0));
        Assert.Equal(ScanSites(sim), ReadSites(sites));
    }

    [Fact]
    public void Counts_and_city_views_follow_assignments_and_queue_changes()
    {
        var sim = Scenario(new(12, 8, true), game =>
        {
            var city = AddCity(game, 0, 4, 4);
            foreach (var tile in game.State.Grid.Range(game.State.Grid.TileAt(city.Position), 2))
                game.State.Plots[tile.Value].Owner = 0;
            AddUnit(game, 0, "military:worker", 4, 4);
        });
        var ctx = NewAi(sim, "AiContext", sim, 0);
        var issue = Method<Func<GameCommand, bool>>(ctx, "Issue");
        var city = sim.State.CityById(1);
        var before = Property<YieldVector>(Call(ctx, "ViewOf", city)!, "Yields");
        Assert.True(issue(new SetCityAssignmentsCommand { Player = 0, CityId = 1, Tiles = [HexGrid.FromOffset(5, 4)] }));
        var after = Property<YieldVector>(Call(ctx, "ViewOf", city)!, "Yields");
        Assert.NotEqual(before, after);
        Assert.Equal(sim.Content.Services.Get<ICityYieldService>().Single().Calculate(sim, 1).Yields, after);
        var roleType = ctx.GetType().Assembly.GetType("Civ.Mods.Ai.UnitRole")!;
        int Count(string method, string role) => (int)Call(ctx, method, Enum.Parse(roleType, role))!;
        Assert.Equal(1, Count("CountUnits", "Worker"));
        Assert.Equal(0, Count("CountQueued", "Melee"));
        Assert.True(issue(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:unit", ContentId = "military:warrior" }));
        Assert.Equal(1, Count("CountQueued", "Melee"));
        Assert.True(issue(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:unit", ContentId = "military:worker", Append = true }));
        Assert.Equal(1, Count("CountQueued", "Worker"));
        Assert.True(issue(new SetCityProductionCommand { Player = 0, CityId = 1, Kind = "cities:unit", ContentId = "military:worker" }));
        Assert.Equal(0, Count("CountQueued", "Melee"));
        Assert.Equal(1, Count("CountQueued", "Worker"));
        Assert.True(issue(new DisbandUnitCommand { Player = 0, Unit = 1 }));
        Assert.Equal(0, Count("CountUnits", "Worker"));
        var hash = sim.ComputeHash();
        Assert.False(issue(new SetCityProductionCommand { Player = 1, CityId = 1, Kind = "cities:unit", ContentId = "military:warrior" }));
        Assert.Equal(hash, sim.ComputeHash());
        Assert.Equal(0, Count("CountQueued", "Melee"));
    }

    private static int ScanThreat(Simulation sim, Hex position, int radius) => sim.State.Units.Where(u => u.Health > 0
        && u.Owner != 0 && sim.TeamOf(0).Wars.Contains(sim.TeamOf(u.Owner).Id)
        && sim.State.Grid.Distance(u.Position, position) <= radius).Sum(u => Math.Max(1, sim.Content.Get<Civ.Content.Schema.UnitDef>(u.Type).Strength));

    // Deliberately scan all plots/cities instead of using range masks or a nearest-city search.
    // These fixtures contain only grass and optional hills, without water, rivers, features or bonuses.
    private static (int Tile, int Score, int Area)[] ScanSites(Simulation sim)
    {
        var grid = sim.State.Grid;
        var own = sim.State.Cities.Where(c => c.Owner == 0).ToArray();
        var anchors = own.Length > 0 ? own.Select(c => c.Position) : sim.State.Units.Where(u => u.Owner == 0).Select(u => u.Position);
        int range = sim.Content.Get<RuleConstantsDef>().All.Single().Values.MinCityRange;
        var sites = new List<(int Tile, int Score, int Area)>();
        foreach (var tile in grid.Tiles)
        {
            if (!ScanEligible(sim, tile, anchors, range)) continue;
            int score = ScanSiteScore(sim, tile, own);
            if (score >= 100) sites.Add((tile.Value, score, sim.State.Plots[tile.Value].AreaId));
        }
        return sites.OrderByDescending(s => s.Score).ThenBy(s => s.Tile).ToArray();
    }

    private static bool ScanEligible(Simulation sim, TileId tile, IEnumerable<Hex> anchors, int range)
    {
        var plot = sim.State.Plots[tile.Value];
        var grid = sim.State.Grid;
        if (!plot.Revealed.Contains(sim.TeamOf(0).Id) || plot.Owner is > 0) return false;
        if (!anchors.Any(a => grid.Distance(grid.TileAt(a), tile) <= 9)) return false;
        return !sim.State.Cities.Any(c => grid.Distance(grid.TileAt(c.Position), tile) <= range + (c.Owner == 0 ? 1 : 2));
    }

    private static int ScanSiteScore(Simulation sim, TileId tile, City[] own)
    {
        var grid = sim.State.Grid;
        int score = 0;
        foreach (var neighbor in grid.Tiles)
        {
            int distance = grid.Distance(tile, neighbor);
            var other = sim.State.Plots[neighbor.Value];
            if (distance > 3 || other.Owner is > 0) continue;
            int value = other.Kind == PlotKind.Hills ? 12 : 8;
            if (sim.State.Cities.Any(c => grid.Distance(grid.TileAt(c.Position), neighbor) <= 2)) value /= 3;
            score += distance <= 2 ? value : value / 2;
        }
        if (sim.State.Plots[tile.Value].Kind == PlotKind.Hills) score += 14;
        int nearest = own.Select(c => grid.Distance(grid.TileAt(c.Position), tile)).DefaultIfEmpty(0).Min();
        return score - Math.Max(0, nearest - 6) * 12;
    }

    private static (int Tile, int Score, int Area)[] ReadSites(object sites) => ((IEnumerable)Property<object>(sites, "Sites"))
        .Cast<object>().Select(s => (Property<TileId>(s, "Tile").Value, Property<int>(s, "Score"), Property<int>(s, "Area"))).ToArray();

    private Simulation Scenario(MapSpec map, Action<Simulation> configure, SimulationSettings? settings = null) => Simulation.CreateScenario(mods.Content,
        settings ?? GameFactory.Settings(71), map, Enumerable.Range(0, map.Width * map.Height)
            .Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), game =>
        {
            configure(game);
            foreach (var plot in game.State.Plots) plot.Revealed.Add(game.TeamOf(0).Id);
        });

    private static City AddCity(Simulation sim, int owner, int column, int row)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner,
            Position = HexGrid.FromOffset(column, row), Population = 3 };
        sim.State.AddCity(city);
        sim.State.PlotAt(city.Position).Owner = owner;
        return city;
    }

    private static Unit AddUnit(Simulation sim, int owner, string type, int column, int row)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type,
            Position = HexGrid.FromOffset(column, row) };
        sim.State.AddUnit(unit);
        sim.Content.Services.Get<IUnitService>().Single().Initialize(sim, unit.Id);
        return unit;
    }

    // Mods load from their shipped payloads. Exercise their internal query owners without exporting test hooks.
    private static object NewAi(Simulation sim, string name, params object[] args) => Activator.CreateInstance(
        sim.Content.PlayerAIs.Get("ai:standard").GetType().Assembly.GetType($"Civ.Mods.Ai.{name}")!, args)!;
    private static object? Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public)!.Invoke(target, args);
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name)!.GetValue(target)!;
    private static T Method<T>(object target, string name) where T : Delegate => target.GetType().GetMethod(name)!.CreateDelegate<T>(target);
}
