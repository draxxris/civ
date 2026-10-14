using System.Diagnostics;
using Civ.Content;
using Civ.Game;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

// Behavioral owners: generation/setup, yield queries, worker commands, network queries and sight.
// Cases use independent equations or invariants, not production predicates as expected-value helpers.
public sealed class WorldRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private ContentDb Content => mods.Content;
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();

    [Theory]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(987654UL)]
    public void Continents_are_deterministic_habitable_and_have_valid_layers(ulong seed)
    {
        var watch = Stopwatch.StartNew();
        var sim = GameFactory.Create(Content, seed);
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds < 1000, $"Generation and setup took {watch.ElapsedMilliseconds}ms");
        var again = GameFactory.Create(Content, seed);
        Assert.Equal(sim.ComputeHash(), again.ComputeHash());
        var loaded = SaveGame.Load(Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(new MapSpec(84, 52, true), sim.State.Map);
        int land = sim.State.Plots.Count(p => !Content.Get<S.TerrainDef>(p.Terrain).IsWater);
        Assert.InRange(land * 100 / sim.State.Grid.Count, 24, 26);
        Assert.Contains(sim.State.Plots, p => p.RiverEdges != 0);
        Assert.Contains(sim.State.Plots, p => p.Bonus is not null);
        Assert.Contains(sim.State.Plots, p => p.Kind == PlotKind.Hills);
        Assert.Contains(sim.State.Plots, p => p.Kind == PlotKind.Peak);
        Assert.Contains(sim.State.Plots, p => p.Feature == "world:forest");
        Assert.Contains(sim.State.Plots, p => p.Improvement == "world:goody_hut");
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var plot = sim.State.Plots[tile.Value];
            for (int d = 0; d < 6; d++)
                if (sim.State.Grid.Neighbor(tile, d) is { } neighbor)
                    Assert.Equal((plot.RiverEdges >> d) & 1, (sim.State.Plots[neighbor.Value].RiverEdges >> ((d + 3) % 6)) & 1);
            foreach (var neighbor in sim.State.Grid.Neighbors(tile))
            {
                var other = sim.State.Plots[neighbor.Value];
                bool sameType = Content.Get<S.TerrainDef>(plot.Terrain).IsWater == Content.Get<S.TerrainDef>(other.Terrain).IsWater;
                Assert.Equal(sameType, plot.AreaId == other.AreaId);
            }
            if (plot.Bonus is { } bonusId) AssertBonusValid(sim, tile, Content.Get<S.BonusDef>(bonusId));
        }
        var starts = sim.State.Players.Select(p => p.StartingPlot!.Value).ToArray();
        Assert.True(sim.State.Grid.Distance(starts[0], starts[1]) >= 10);
        foreach (var start in starts)
        {
            Assert.NotEqual(PlotKind.Peak, sim.State.Plots[start.Value].Kind);
            long food = sim.State.Grid.Range(start, 2).Sum(t => Content.Get<S.TerrainDef>(sim.State.Plots[t.Value].Terrain).Yields.Food);
            Assert.True(food >= 20, $"Start {start} has only {food} nearby food");
        }
    }

    private void AssertBonusValid(Simulation sim, TileId tile, S.BonusDef bonus)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Feature is { } feature)
        {
            Assert.True(bonus.Features.GetValueOrDefault(feature));
            Assert.True(bonus.FeatureTerrains.GetValueOrDefault(plot.Terrain));
        }
        else Assert.True(bonus.Terrains.GetValueOrDefault(plot.Terrain));
        Assert.True(plot.Kind switch { PlotKind.Hills => bonus.Hills, PlotKind.Peak => bonus.Peaks, _ => bonus.Flatlands });
        int latitude = Math.Abs(2 * (tile.Value / sim.State.Map.Width) - (sim.State.Map.Height - 1)) * 90 / (sim.State.Map.Height - 1);
        Assert.InRange(latitude, bonus.MinLatitude, bonus.MaxLatitude);
        if (bonus.NoRiverSide) Assert.Equal(0, plot.RiverEdges);
    }

    [Fact]
    public void Three_civilizations_receive_their_own_techs_settlers_and_role_units()
    {
        var sim = GameFactory.Create(Content, 42, "peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar", "peoples:leader_hatshepsut");
        Assert.Equal(new[] { "peoples:albania", "peoples:rome", "peoples:egypt" }, sim.State.Players.Select(p => p.Civilization));
        Assert.Equal(new[] { "research:agriculture", "research:ritualism" }, sim.TeamOf(0).Techs);
        Assert.Equal(new[] { "research:fishing", "research:stone_tools" }, sim.TeamOf(1).Techs);
        Assert.Equal(new[] { "research:agriculture", "research:ritualism" }, sim.TeamOf(2).Techs);
        foreach (var player in sim.State.Players)
        {
            var units = sim.State.Units.Where(u => u.Owner == player.Id).ToArray();
            Assert.Equal(new[] { "military:settler", "military:warrior" }, units.Select(u => u.Type).Order(StringComparer.Ordinal));
            Assert.All(units, u => Assert.Equal(player.StartingPlot, sim.State.Grid.TileAt(u.Position)));
        }
    }

    [Fact]
    public void Plot_ledger_accounts_for_feature_river_irrigation_bonus_route_and_technology()
    {
        var sim = GameFactory.Scenario(Content);
        var tile = sim.State.Grid.TileAt(3, 3);
        var plot = sim.State.Plots[tile.Value];
        sim.State.SetRiver(tile, 0, true);
        var yields = Service<IPlotYieldService>(sim);
        Assert.Equal(new YieldVector(2, 0, 1), yields.Calculate(sim, 0, tile).Yields);
        plot.Feature = "world:forest";
        Assert.Equal(new YieldVector(2, 1, 0), yields.Calculate(sim, 0, tile).Yields); // Feature replaces the terrain river change.
        plot.Feature = null;
        plot.Improvement = "world:improvement_improvement_farm";
        Assert.Equal(new YieldVector(3, 0, 1), yields.Calculate(sim, 0, tile).Yields);
        sim.TeamOf(0).Techs.Add("research:crop_rotation");
        var ledger = yields.Calculate(sim, 0, tile);
        Assert.Equal(new YieldVector(4, 0, 1), ledger.Yields);
        Assert.Contains(ledger.Ledger.Lines, l => l.Source == "research:crop_rotation" && l.Key == "yield.flat.food" && l.Amount == 1);
        Assert.Equal(1, ledger.Ledger.Lines.Single(l => l.Source == "world:river").Amount);
        plot.Kind = PlotKind.Hills;
        plot.Bonus = "world:copper";
        plot.Improvement = "world:improvement_improvement_mine";
        Assert.Equal(new YieldVector(1, 2, 1), yields.Calculate(sim, 0, tile).Yields);
        sim.TeamOf(0).Techs.Add("research:bronze_working");
        Assert.Equal(new YieldVector(1, 4, 1), yields.Calculate(sim, 0, tile).Yields);
        sim.TeamOf(0).Techs.Add("research:iron_working");
        plot.Route = "world:route_route_road";
        Assert.Equal(new YieldVector(1, 6, 1), yields.Calculate(sim, 0, tile).Yields);
        Assert.Equal(6, yields.Calculate(sim, 0, tile).Ledger.Total("world:production"));
    }

    [Fact]
    public void Worker_command_validates_then_accumulates_shared_progress_and_survives_a_save()
    {
        var sim = GameFactory.Scenario(Content);
        var tile = sim.State.Grid.TileAt(3, 3);
        sim.State.SetRiver(tile, 0, true);
        var worker = AddWorker(sim, tile);
        var command = new StartBuildCommand { Player = 0, Unit = worker.Id, Build = "world:build_build_farm" };
        var codec = new CommandCodec(Content);
        var hash = sim.ComputeHash();
        Assert.False(sim.TryApply(codec.Decode(codec.Encode(command)), out _));
        Assert.Equal(hash, sim.ComputeHash());
        sim.TeamOf(0).Techs.Add("research:agriculture");
        Assert.True(sim.TryApply(command, out var error), error);
        sim.AdvanceTurn();
        Assert.Equal(100, sim.State.Plots[tile.Value].BuildProgress[command.Build]);
        var loaded = SaveGame.Load(Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        for (int i = 0; i < 7; i++) { sim.AdvanceTurn(); loaded.AdvanceTurn(); }
        Assert.Equal("world:improvement_improvement_farm", sim.State.Plots[tile.Value].Improvement);
        Assert.Empty(sim.State.Plots[tile.Value].BuildProgress);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Remote_build_preview_is_read_only_and_matches_completed_yields()
    {
        var sim = GameFactory.Scenario(Content);
        var tile = sim.State.Grid.TileAt(3, 3);
        sim.State.SetRiver(tile, 0, true);
        var worker = AddWorker(sim, sim.State.Grid.TileAt(2, 3));
        var builds = Service<IWorkerBuildService>(sim);
        const string farm = "world:build_build_farm";
        var hash = sim.ComputeHash();
        Assert.NotNull(builds.Preview(sim, 0, worker.Id, tile, farm).Error);
        Assert.Equal(hash, sim.ComputeHash());
        sim.TeamOf(0).Techs.Add("research:agriculture");
        hash = sim.ComputeHash();
        var preview = builds.Preview(sim, 0, worker.Id, tile, farm);
        Assert.Null(preview.Error);
        Assert.Equal(new YieldVector(3, 0, 1), preview.Yields);
        Assert.Equal(hash, sim.ComputeHash());
        sim.State.MoveUnit(worker.Id, sim.State.Grid.HexOf(tile));
        Assert.True(sim.TryApply(new StartBuildCommand { Player = 0, Unit = worker.Id, Build = farm }, out var error), error);
        for (int i = 0; i < 8; i++) sim.AdvanceTurn();
        Assert.Equal(preview.Yields, Service<IPlotYieldService>(sim).Calculate(sim, 0, tile).Yields);
        Assert.NotNull(builds.Preview(sim, 0, worker.Id, tile, farm).Error);
    }

    [Fact]
    public void Chopping_publishes_city_production_and_cottages_upgrade_only_when_worked()
    {
        var sim = GameFactory.Scenario(Content);
        var tile = sim.State.Grid.TileAt(3, 3);
        var city = AddCity(sim, sim.State.Grid.TileAt(4, 3));
        sim.State.Plots[tile.Value].Feature = "world:forest";
        sim.TeamOf(0).Techs.Add("research:bronze_working");
        var worker = AddWorker(sim, tile);
        Assert.True(sim.TryApply(new StartBuildCommand { Player = 0, Unit = worker.Id, Build = "world:remove_forest" }, out var error), error);
        for (int i = 0; i < 5; i++) sim.AdvanceTurn(); // 300 * 160% = 480, 100 work per turn.
        Assert.Null(sim.State.Plots[tile.Value].Feature);
        var grant = Assert.Single(sim.Events.PreviousTurn.OfType<FeatureProduction>());
        Assert.Equal(city.Id, grant.City);
        Assert.Equal(52, grant.Production); // 30 * 176 / 100.
        sim.State.Plots[tile.Value].Improvement = "world:improvement_improvement_cottage";
        for (int i = 0; i < 20; i++) sim.AdvanceTurn();
        Assert.Equal("world:improvement_improvement_cottage", sim.State.Plots[tile.Value].Improvement);
        city.Worked.Add(sim.State.Grid.HexOf(tile));
        for (int i = 0; i < 16; i++) sim.AdvanceTurn();
        Assert.Equal("world:improvement_improvement_cottage", sim.State.Plots[tile.Value].Improvement);
        sim.TeamOf(0).Techs.Add("research:city_planning");
        for (int i = 0; i < 16; i++) sim.AdvanceTurn();
        Assert.Equal("world:improvement_improvement_hamlet", sim.State.Plots[tile.Value].Improvement);
    }

    [Fact]
    public void Road_network_requires_reveal_and_connection_and_invalidates_after_pillage()
    {
        var sim = GameFactory.Scenario(Content);
        var city = AddCity(sim, sim.State.Grid.TileAt(2, 3));
        var tile = sim.State.Grid.TileAt(5, 3);
        var bonus = sim.State.Plots[tile.Value];
        bonus.Bonus = "world:copper";
        bonus.Improvement = "world:improvement_improvement_mine";
        foreach (int x in new[] { 3, 4, 5 }) sim.State.Plots[sim.State.Grid.TileAt(x, 3).Value].Route = "world:route_route_road";
        var network = Service<IResourceNetwork>(sim);
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        sim.TeamOf(0).Techs.Add("research:bronze_working");
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        sim.TeamOf(0).Techs.Add("research:mining");
        Assert.Equal(1, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        Assert.True(Content.Requirements.Evaluate(new Requirement.Leaf("bonus", "world:copper"), new(sim, 0, city)).Success);
        sim.State.Plots[sim.State.Grid.TileAt(4, 3).Value].Route = null;
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        Assert.False(Content.Requirements.Evaluate(new Requirement.Leaf("bonus", "world:copper"), new(sim, 0, city)).Success);
    }

    [Fact]
    public void Building_can_clear_a_feature_without_chop_technology_but_grants_no_production()
    {
        var sim = GameFactory.Scenario(Content);
        var tile = sim.State.Grid.TileAt(3, 3);
        sim.State.SetRiver(tile, 0, true);
        sim.State.Plots[tile.Value].Feature = "world:forest";
        var worker = AddWorker(sim, tile);
        sim.TeamOf(0).Techs.Add("research:agriculture");
        Assert.False(sim.TryApply(new StartBuildCommand { Player = 0, Unit = worker.Id, Build = "world:remove_forest" }, out _));
        Assert.True(sim.TryApply(new StartBuildCommand { Player = 0, Unit = worker.Id, Build = "world:build_build_farm" }, out var error), error);
        var grants = new List<FeatureProduction>();
        for (int i = 0; i < 13; i++) { sim.AdvanceTurn(); grants.AddRange(sim.Events.PreviousTurn.OfType<FeatureProduction>()); }
        Assert.Null(sim.State.Plots[tile.Value].Feature);
        Assert.Equal("world:improvement_improvement_farm", sim.State.Plots[tile.Value].Improvement);
        Assert.Empty(grants);
    }

    [Fact]
    public void Irrigation_propagates_only_with_technology_and_through_carriers()
    {
        var sim = GameFactory.Scenario(Content);
        var source = sim.State.Grid.TileAt(2, 3);
        var carrier = sim.State.Grid.TileAt(3, 3);
        var target = sim.State.Grid.TileAt(4, 3);
        sim.State.SetRiver(source, 3, true);
        sim.State.Plots[source.Value].Improvement = "world:improvement_improvement_farm";
        sim.State.Plots[carrier.Value].Improvement = "world:improvement_improvement_farm";
        var water = Service<IWaterService>(sim);
        Assert.True(water.IsFreshWater(sim, source));
        Assert.False(water.IsFreshWater(sim, target));
        Assert.False(water.IsIrrigated(sim, 0, target));
        var tech = Content.Get<S.TechDef>().All.First(t => t.Irrigation);
        sim.TeamOf(0).Techs.Add(tech.Id);
        Assert.True(water.IsIrrigated(sim, 0, target));
        sim.State.Plots[carrier.Value].Improvement = null;
        Assert.False(water.IsIrrigated(sim, 0, target));
    }

    [Fact]
    public void Exploration_survives_movement_and_reload_without_leaking_to_other_teams()
    {
        var sim = GameFactory.Scenario(Content);
        var tile = sim.State.Grid.TileAt(0, 3);
        var worker = AddWorker(sim, tile);
        var sight = Service<IVisibilityService>(sim);
        sight.Recompute(sim);
        Assert.True(sim.State.Plots[sim.State.Grid.TileAt(11, 3).Value].Visible.Contains(0));
        sim.State.MoveUnit(worker.Id, sim.State.Grid.HexOf(sim.State.Grid.TileAt(5, 3)));
        sight.Recompute(sim);
        Assert.False(sim.State.Plots[tile.Value].Visible.Contains(0));
        Assert.True(sim.State.Plots[tile.Value].Revealed.Contains(0));
        var loaded = SaveGame.Load(Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.False(loaded.State.Plots[tile.Value].Visible.Contains(1));
    }

    [Fact]
    public void Standard_world_supports_its_default_player_count_with_separated_starts()
    {
        int count = Content.Get<S.WorldSizeDef>("foundation:worldsize_standard").DefaultPlayers;
        var leaders = Enumerable.Repeat("peoples:leader_gjergj_kastrioti", count).ToArray();
        var sim = GameFactory.Create(Content, 42, leaders);
        var starts = sim.State.Players.Select(p => p.StartingPlot!.Value).ToArray();
        Assert.Equal(count, starts.Length);
        foreach (var player in sim.State.Players)
        {
            var potential = sim.State.Grid.Range(player.StartingPlot!.Value, 2)
                .Select(t => Service<IPlotYieldService>(sim).CalculateNature(sim, player.Id, t).Yields)
                .Aggregate(default(YieldVector), (a, b) => a + b);
            Assert.True(potential.Food >= 20, $"Player {player.Id} lacks nearby food: {potential}");
            Assert.True(potential.Production >= 4, $"Player {player.Id} lacks nearby production: {potential}");
        }
        for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count; j++) Assert.True(sim.State.Grid.Distance(starts[i], starts[j]) >= 10);
    }

    [Fact]
    public void River_and_sea_trade_require_capabilities_and_use_distinct_connected_edges()
    {
        var sim = GameFactory.Scenario(Content);
        var city = AddCity(sim, sim.State.Grid.TileAt(2, 3));
        var target = sim.State.Grid.TileAt(5, 3);
        var plot = sim.State.Plots[target.Value];
        plot.Bonus = "world:copper";
        plot.Improvement = "world:improvement_improvement_mine";
        sim.TeamOf(0).Techs.UnionWith(["research:mining", "research:bronze_working"]);
        foreach (int x in new[] { 2, 3, 4 }) sim.State.SetRiver(sim.State.Grid.TileAt(x, 3), 0, true);
        var network = Service<IResourceNetwork>(sim);
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        var riverTech = Content.Get<S.TechDef>().All.First(t => t.RiverTrade);
        sim.TeamOf(0).Techs.Add(riverTech.Id);
        Assert.Equal(1, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        sim.State.SetRiver(sim.State.Grid.TileAt(4, 3), 0, false);
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        foreach (var p in sim.State.Plots) p.RiverEdges = 0;
        sim.State.Plots[sim.State.Grid.TileAt(3, 3).Value].Terrain = "world:coast";
        sim.State.Plots[sim.State.Grid.TileAt(4, 3).Value].Terrain = "world:ocean";
        // Unowned water is gated by trade techs; it must stay revealed to the team.
        foreach (int x in new[] { 3, 4 })
        {
            var water = sim.State.Plots[sim.State.Grid.TileAt(x, 3).Value];
            water.Owner = null; water.Revealed.Add(sim.State.PlayerById(0).TeamId);
        }
        var port = AddCity(sim, target);
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        var coastTech = Content.Get<S.TechDef>().All.First(t => t.TradeTerrains.GetValueOrDefault("world:coast"));
        sim.TeamOf(0).Techs.Add(coastTech.Id);
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        var oceanTech = Content.Get<S.TechDef>().All.First(t => t.TradeTerrains.GetValueOrDefault("world:ocean"));
        sim.TeamOf(0).Techs.Add(oceanTech.Id);
        Assert.Equal(1, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        sim.State.Plots[target.Value].Owner = 1;
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
    }

    [Fact]
    public void Same_bank_river_and_owned_coast_connect_resources()
    {
        var sim = GameFactory.Scenario(Content);
        var city = AddCity(sim, sim.State.Grid.TileAt(2, 3));
        var target = sim.State.Grid.TileAt(5, 3);
        var plot = sim.State.Plots[target.Value];
        plot.Bonus = "world:copper";
        plot.Improvement = "world:improvement_improvement_mine";
        sim.TeamOf(0).Techs.UnionWith(["research:mining", "research:bronze_working"]);
        var riverTech = Content.Get<S.TechDef>().All.First(t => t.RiverTrade);
        sim.TeamOf(0).Techs.Add(riverTech.Id);
        // Edge 1 is not the shared edge between east neighbors, but runs along the same corridor.
        foreach (int x in new[] { 2, 3, 4, 5 }) sim.State.SetRiver(sim.State.Grid.TileAt(x, 3), 1, true);
        var network = Service<IResourceNetwork>(sim);
        Assert.Equal(1, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        foreach (var p in sim.State.Plots) p.RiverEdges = 0;
        Assert.Equal(0, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
        // Own-border water connects before any sailing tech.
        foreach (int x in new[] { 3, 4 }) sim.State.Plots[sim.State.Grid.TileAt(x, 3).Value].Terrain = "world:coast";
        AddCity(sim, target);
        Assert.Equal(1, network.ConnectedCount(sim, 0, city.Id, "world:copper"));
    }

    [Fact]
    public void Foreign_trade_connections_cannot_path_through_unrevealed_tiles()
    {
        var sim = GameFactory.Scenario(Content);
        var home = AddCity(sim, sim.State.Grid.TileAt(2, 3));
        var foreignTile = sim.State.Grid.TileAt(6, 3);
        var foreign = new City { Id = sim.State.NextCityId++, Owner = 1, Position = sim.State.Grid.HexOf(foreignTile), Name = "Foreign city" };
        sim.State.AddCity(foreign);
        sim.State.Plots[foreignTile.Value].Revealed.Add(0);
        var road = new[] { 3, 4, 5 }.Select(x => sim.State.Grid.TileAt(x, 3)).ToArray();
        foreach (var tile in road)
        {
            sim.State.Plots[tile.Value].Owner = 1;
            sim.State.Plots[tile.Value].Route = "world:route_route_road";
        }
        var connections = Service<ICityConnectionService>(sim);
        Assert.False(connections.Connected(sim, home.Id, foreign.Id));
        foreach (var tile in road) sim.State.Plots[tile.Value].Revealed.Add(0);
        Assert.True(connections.Connected(sim, home.Id, foreign.Id));
    }

    private static Unit AddWorker(Simulation sim, TileId tile)
    {
        var worker = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:worker", Position = sim.State.Grid.HexOf(tile), MovesLeft = 1 };
        sim.State.AddUnit(worker);
        return worker;
    }
    private static City AddCity(Simulation sim, TileId tile)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(tile), Name = "Test city" };
        sim.State.AddCity(city);
        return city;
    }
}
