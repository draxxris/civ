using Civ;
using Civ.Client;
using Civ.Client.Terrain;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class SightMemoryTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static IVisibilityService Sight(Simulation sim) => sim.Content.Services.Get<IVisibilityService>().Single();
    private Simulation Scenario() => GameFactory.Scenario(mods.Content);
    private static Unit Observer(Simulation sim, int owner, TileId tile, string type = "military:warrior")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = sim.State.Grid.HexOf(tile) };
        sim.State.AddUnit(unit);
        return unit;
    }

    // Owner-boundary matrix: range, blocking and elevation are independent of renderer implementation.
    [Theory]
    [InlineData(PlotKind.Flat, null, false)]
    [InlineData(PlotKind.Hills, null, true)]
    [InlineData(PlotKind.Peak, null, true)]
    [InlineData(PlotKind.Flat, "world:forest", true)]
    public void Wrapped_rays_block_symmetrically(PlotKind kind, string? feature, bool blocked)
    {
        var sim = Scenario();
        var grid = sim.State.Grid;
        var a = grid.TileAt(11, 3); var b = grid.TileAt(1, 3);
        var middle = sim.State.Plots[grid.TileAt(0, 3).Value];
        middle.Kind = kind; middle.Feature = feature;

        Assert.Equal(!blocked, Sight(sim).CanSeePlot(sim, a, b, 0, 2));
        Assert.Equal(!blocked, Sight(sim).CanSeePlot(sim, b, a, 0, 2));
    }

    [Fact]
    public void High_ground_grants_outer_ring_but_not_sight_through_peaks()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var a = grid.TileAt(3, 3); var b = grid.TileAt(6, 3);
        Assert.False(sight.CanSeePlot(sim, a, b, 0, 2));
        sim.State.Plots[a.Value].Kind = PlotKind.Hills;
        Assert.True(sight.CanSeePlot(sim, a, b, 0, 2));
        sim.State.Plots[grid.TileAt(4, 3).Value].Kind = PlotKind.Peak;
        Assert.False(sight.CanSeePlot(sim, a, b, 0, 2));
        Assert.True(sight.CanSeePlot(sim, a, b, 0, 3, aerial: true));
        Assert.False(sight.CanSeePlot(sim, a, b, 0, 2, aerial: true));
    }

    [Fact]
    public void Water_vantage_requires_the_observers_team_technology_and_survives_reload()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        foreach (var plot in sim.State.Plots) plot.Terrain = "world:coast";
        var origin = grid.TileAt(3, 3); var target = grid.TileAt(6, 3);
        var observer = Observer(sim, 0, origin, "military:galley");
        observer.Promotions.Add(sim.Content.Get<Civ.Content.Schema.PromotionDef>().All.First(p => p.VisibilityChange == 1).Id);
        Assert.False(sight.CanSeePlot(sim, origin, target, 0, 2));
        sim.TeamOf(1).Techs.Add("research:optics");
        Assert.False(sight.CanSeePlot(sim, origin, target, 0, 2));
        Assert.True(sight.CanSeePlot(sim, origin, target, 1, 2));
        sim.TeamOf(0).Techs.Add("research:optics");
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        foreach (var game in new[] { sim, loaded })
        {
            sight.Recompute(game);
            Assert.True(game.State.Plots[target.Value].Visible.Contains(0));
            Assert.True(sight.CanSeePlot(game, origin, target, 0, 2));
            Assert.False(sight.CanSeePlot(game, origin, grid.TileAt(7, 3), 0, 2));
            game.State.Plots[grid.TileAt(4, 3).Value].Kind = PlotKind.Peak;
            sight.Recompute(game);
            Assert.False(game.State.Plots[target.Value].Visible.Contains(0));
        }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(2)]
    public void Tied_shortest_wrap_images_keep_sight_until_both_paths_are_blocked(int rowDelta)
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var origin = grid.TileAt(0, 3); var target = grid.TileAt(6, 3 + rowDelta);
        var east = grid.TileAt(1, 3); var west = grid.TileAt(11, 3);
        void Check(bool visible)
        {
            Assert.Equal(visible, sight.CanSeePlot(sim, origin, target, 0, 7));
            Assert.Equal(visible, sight.CanSeePlot(sim, target, origin, 0, 7));
        }
        Check(true);
        sim.State.Plots[east.Value].Kind = PlotKind.Peak; Check(true);
        sim.State.Plots[west.Value].Kind = PlotKind.Peak; Check(false);
        sim.State.Plots[east.Value].Kind = PlotKind.Flat; Check(true);
    }

    [Fact]
    public void Remembered_city_fields_affect_hash_and_survive_hidden_removal_until_reobservation()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var tile = grid.TileAt(3, 3);
        var city = new City { Id = sim.State.NextCityId++, Owner = 1, Position = grid.HexOf(tile), Name = "Old Port", Population = 4, Buildings = ["cities:granary"] };
        sim.State.AddCity(city); sim.State.Plots[tile.Value].Owner = 1;
        var observer = Observer(sim, 0, tile); sight.Recompute(sim);
        sim.State.MoveUnit(observer.Id, grid.HexOf(grid.TileAt(9, 3))); sight.Recompute(sim);
        Assert.False(sim.State.Plots[tile.Value].Visible.Contains(0));
        city.Population = 9; city.Buildings.Clear();
        sim.State.RemoveCity(city.Id);
        sight.Recompute(sim);
        var saved = SaveGame.Save(sim);
        var loaded = SaveGame.Load(sim.Content, saved);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        var remembered = loaded.State.Plots[tile.Value].RevealedSnapshots[0];
        Assert.NotNull(remembered.City);
        ulong hash = loaded.ComputeHash();
        loaded.State.Plots[tile.Value].RevealedSnapshots[0] = remembered with { City = remembered.City with { Population = 5 } };
        Assert.NotEqual(hash, loaded.ComputeHash());
        loaded = SaveGame.Load(sim.Content, saved);
        foreach (var game in new[] { sim, loaded })
        {
            var session = new GameSession(game);
            Assert.Null(game.State.FindCity(city.Id));
            var mark = Assert.Single(session.KnownCities(), c => c.Id == city.Id);
            Assert.Equal("Old Port", mark.Name); Assert.Equal(4, mark.Population);
            Assert.Equal(1, mark.Owner); Assert.Equal(grid.HexOf(tile), mark.Position);
            Assert.Equal(new[] { "cities:granary" }, mark.Buildings);
            Assert.Contains("City: Old Port (size 4)", session.DescribeTile(grid.HexOf(tile))!.Lines);
            game.State.MoveUnit(observer.Id, grid.HexOf(tile)); sight.Recompute(game);
            Assert.DoesNotContain(session.KnownCities(), c => c.Id == city.Id);
            Assert.Null(session.PerceivedPlot(tile)!.City);
            Assert.DoesNotContain(session.DescribeTile(grid.HexOf(tile))!.Lines, l => l.StartsWith("City:", StringComparison.Ordinal));
        }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Aircraft_and_recon_bypass_occlusion_without_extra_vantage_ring()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var a = grid.TileAt(3, 3); var b = grid.TileAt(5, 3);
        sim.State.Plots[grid.TileAt(4, 3).Value].Kind = PlotKind.Peak;
        var aircraft = Observer(sim, 0, a, "military:fighter");
        aircraft.Promotions.Add(sim.Content.Get<Civ.Content.Schema.PromotionDef>().All.First(p => p.VisibilityChange > 0).Id);
        sight.Recompute(sim);
        Assert.True(sim.State.Plots[b.Value].Visible.Contains(0));
        sim.State.Units.Clear(); sim.State.RebuildIndexes();
        var military = sim.State.Extensions.Get<MilitaryGameState>(sim.Content)!;
        military.ReconSights.Add(new(0, grid.HexOf(a), 2));
        sight.Recompute(sim);
        Assert.True(sim.State.Plots[b.Value].Visible.Contains(0));
        military.ReconSights.Clear(); sight.Recompute(sim);
        Assert.False(sim.State.Plots[b.Value].Visible.Contains(0));
        Assert.NotNull(sight.SnapshotOf(sim, b, 0));
    }

    [Fact]
    public void Loss_captures_latest_observation_per_team_and_reload_continues_memory()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var tile = grid.TileAt(3, 3); var plot = sim.State.Plots[tile.Value];
        var first = Observer(sim, 0, tile); var second = Observer(sim, 1, tile);
        sight.Recompute(sim);
        plot.Feature = "world:forest"; plot.Route = "world:road";
        sim.State.MoveUnit(first.Id, grid.HexOf(grid.TileAt(9, 3))); sight.Recompute(sim);
        plot.Feature = "world:jungle"; plot.Kind = PlotKind.Hills; plot.Owner = 1;
        sim.State.MoveUnit(second.Id, grid.HexOf(grid.TileAt(9, 3))); sight.Recompute(sim);
        var before = sight.SnapshotOf(sim, tile, 0)!;
        Assert.Equal("world:forest", before.Feature); Assert.Equal("world:road", before.Route);
        Assert.Equal("world:jungle", sight.SnapshotOf(sim, tile, 1)!.Feature);
        plot.Feature = null; plot.Route = null; plot.Terrain = "world:ocean";
        var restored = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), restored.ComputeHash());
        sight.Recompute(restored);
        Assert.Equal(before, sight.SnapshotOf(restored, tile, 0));
        restored.State.MoveUnit(first.Id, grid.HexOf(tile)); sight.Recompute(restored);
        Assert.Equal("world:ocean", sight.SnapshotOf(restored, tile, 0)!.Terrain);
    }

    [Fact]
    public void Occlusion_event_precedes_later_hidden_edits()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var a = grid.TileAt(3, 3); var target = grid.TileAt(5, 3); var blocker = grid.TileAt(4, 3);
        sim.State.Plots[a.Value].Kind = PlotKind.Hills;
        Observer(sim, 0, a); sight.Recompute(sim);
        Assert.True(sim.State.Plots[target.Value].Visible.Contains(0));
        sim.State.Plots[target.Value].Feature = "world:forest";
        sim.State.Plots[blocker.Value].Kind = PlotKind.Peak;
        sim.Events.Publish(new FeatureChanged(blocker, null, null));
        Assert.False(sim.State.Plots[target.Value].Visible.Contains(0));
        sim.State.Plots[target.Value].Feature = "world:jungle";
        sim.Events.Publish(new FeatureChanged(target, "world:forest", "world:jungle"));
        Assert.Equal("world:forest", sight.SnapshotOf(sim, target, 0)!.Feature);
    }

    [Fact]
    public void Fogged_and_unseen_edits_do_not_change_terrain_descriptions_or_city_marks()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var tile = grid.TileAt(3, 3); var observer = Observer(sim, 0, tile);
        sight.Recompute(sim);
        sim.State.MoveUnit(observer.Id, grid.HexOf(grid.TileAt(9, 3))); sight.Recompute(sim);
        var session = new GameSession(sim);
        var description = session.DescribeTile(grid.HexOf(tile))!;
        var before = TerrainSnapshot.From(session, _ => 1, _ => 0);
        foreach (var t in grid.Tiles.Where(t => !sim.State.Plots[t.Value].Visible.Contains(0)))
        {
            var p = sim.State.Plots[t.Value];
            p.Terrain = "world:ocean"; p.Kind = PlotKind.Peak; p.IsLake = true;
            p.Feature = "world:jungle"; p.Owner = 1; p.RiverEdges = 63;
            p.Improvement = "world:city_ruins"; p.Route = "world:road";
        }
        var after = TerrainSnapshot.From(session, _ => 1, _ => 0);
        Assert.Equal(before.Relief, after.Relief); Assert.Equal(before.Terrain, after.Terrain);
        Assert.Equal(before.Feature, after.Feature); Assert.Equal(before.Owner, after.Owner);
        Assert.Equal(before.Rivers, after.Rivers);
        var next = session.DescribeTile(grid.HexOf(tile))!;
        Assert.Equal(description.Title, next.Title); Assert.Equal(description.Lines, next.Lines);
        Assert.Equal(default, session.YieldsOf(grid.HexOf(tile)));
    }

    [Fact]
    public void Missing_fog_history_is_not_reconstructed_from_live_state()
    {
        var sim = Scenario(); var tile = sim.State.Grid.TileAt(3, 3);
        sim.State.Plots[tile.Value].Revealed.Add(0);
        var restored = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Sight(restored).Recompute(restored);
        Assert.Null(Sight(restored).SnapshotOf(restored, tile, 0));
        Assert.Equal("Unexplored", new GameSession(restored).DescribeTile(restored.State.Grid.HexOf(tile))!.Title);
        Sight(restored).RevealPlot(restored, tile, 0);
        Assert.Equal("world:grass", Sight(restored).SnapshotOf(restored, tile, 0)!.Terrain);
    }
}
