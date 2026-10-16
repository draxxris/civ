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
