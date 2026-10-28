using Ariel;
using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class SightMemoryTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static IVisibilityService Sight(Simulation sim) => sim.Content.Services.Get<IVisibilityService>().Single();
    private Simulation Scenario() => GameFactory.Scenario(mods.Content);
    private static Unit Observer(Simulation sim, int owner, TileId tile, string type = "military:warrior", bool extendedSight = false)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = sim.State.Grid.HexOf(tile) };
        if (extendedSight)
            unit.Promotions.Add(sim.Content.Get<Ariel.Content.Schema.PromotionDef>().All.First(p => p.VisibilityChange == 1).Id);
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

        var sight = Sight(sim);
        Assert.Equal(!blocked, sight.CanSeePlot(sim, a, b, 0, 2));
        Assert.Equal(!blocked, sight.CanSeePlot(sim, b, a, 0, 2));
        var observer = Observer(sim, 0, a, extendedSight: true);
        sight.Recompute(sim);
        Assert.Equal(!blocked, sim.State.Plots[b.Value].Visible.Contains(0));
        sim.State.MoveUnit(observer.Id, grid.HexOf(b));
        sight.Recompute(sim);
        Assert.Equal(!blocked, sim.State.Plots[a.Value].Visible.Contains(0));
    }

    [Fact]
    public void High_ground_grants_outer_ring_but_not_sight_through_peaks()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var a = grid.TileAt(3, 3); var b = grid.TileAt(6, 3);
        Observer(sim, 0, a, extendedSight: true);
        void Check(bool visible)
        {
            Assert.Equal(visible, sight.CanSeePlot(sim, a, b, 0, 2));
            sight.Recompute(sim);
            Assert.Equal(visible, sim.State.Plots[b.Value].Visible.Contains(0));
        }
        Check(false);
        sim.State.Plots[a.Value].Kind = PlotKind.Hills;
        Check(true);
        sim.State.Plots[grid.TileAt(4, 3).Value].Kind = PlotKind.Peak;
        Check(false);
        Assert.True(sight.CanSeePlot(sim, a, b, 0, 3, aerial: true));
        Assert.False(sight.CanSeePlot(sim, a, b, 0, 2, aerial: true));
    }

    [Fact]
    public void Water_vantage_requires_the_observers_team_technology_and_survives_reload()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        foreach (var plot in sim.State.Plots) plot.Terrain = "world:coast";
        var origin = grid.TileAt(3, 3); var target = grid.TileAt(6, 3);
        Observer(sim, 0, origin, "military:galley", extendedSight: true);
        Observer(sim, 1, origin, "military:galley", extendedSight: true);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        foreach (var game in new[] { sim, loaded })
        {
            void Check(bool first, bool second)
            {
                sight.Recompute(game);
                Assert.Equal(first, game.State.Plots[target.Value].Visible.Contains(0));
                Assert.Equal(second, game.State.Plots[target.Value].Visible.Contains(1));
                Assert.Equal(first, sight.CanSeePlot(game, origin, target, 0, 2));
                Assert.Equal(second, sight.CanSeePlot(game, origin, target, 1, 2));
            }
            Check(false, false);
            game.TeamOf(1).Techs.Add("research:optics");
            Check(false, true);
            game.TeamOf(0).Techs.Add("research:optics");
            Check(true, true);
            Assert.False(sight.CanSeePlot(game, origin, grid.TileAt(7, 3), 0, 2));
            var blocker = game.State.Plots[grid.TileAt(4, 3).Value];
            blocker.Kind = PlotKind.Peak;
            Check(false, false);
            blocker.Kind = PlotKind.Flat;
            game.TeamOf(0).Techs.Remove("research:optics");
            Check(false, true);
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
        aircraft.Promotions.Add(sim.Content.Get<Ariel.Content.Schema.PromotionDef>().All.First(p => p.VisibilityChange > 0).Id);
        sight.Recompute(sim);
        Assert.True(sim.State.Plots[b.Value].Visible.Contains(0));
        Assert.False(sim.State.Plots[grid.TileAt(6, 3).Value].Visible.Contains(0));
        sim.State.Units.Clear(); sim.State.RebuildIndexes();
        var military = sim.State.Extensions.Get<MilitaryGameState>(sim.Content)!;
        military.ReconSights.Add(new(0, grid.HexOf(a), 2));
        sight.Recompute(sim);
        Assert.True(sim.State.Plots[b.Value].Visible.Contains(0));
        Assert.False(sim.State.Plots[grid.TileAt(6, 3).Value].Visible.Contains(0));
        military.ReconSights.Clear(); sight.Recompute(sim);
        Assert.False(sim.State.Plots[b.Value].Visible.Contains(0));
        Assert.NotNull(sight.SnapshotOf(sim, b, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Loss_captures_latest_observation_per_team_and_reload_continues_memory(bool removed)
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var tile = grid.TileAt(3, 3); var plot = sim.State.Plots[tile.Value];
        var city = new City { Id = sim.State.NextCityId++, Owner = 1, Position = grid.HexOf(tile), Name = "Old Port", Population = 4 };
        sim.State.AddCity(city);
        var first = Observer(sim, 0, tile); var second = Observer(sim, 1, tile);
        void Lose(Unit observer)
        {
            if (removed) sim.State.RemoveUnit(observer.Id);
            else sim.State.MoveUnit(observer.Id, grid.HexOf(grid.TileAt(9, 3)));
            sight.Recompute(sim);
        }
        sight.Recompute(sim);
        plot.Feature = "world:forest"; plot.Route = "world:road"; city.Population = 5;
        Lose(first);
        plot.Feature = "world:jungle"; plot.Kind = PlotKind.Hills; plot.Owner = 1;
        sim.State.RemoveCity(city.Id);
        Lose(second);
        var before = sight.SnapshotOf(sim, tile, 0)!;
        Assert.Equal("world:forest", before.Feature); Assert.Equal("world:road", before.Route);
        Assert.Equal(5, before.City!.Population);
        Assert.Equal("world:jungle", sight.SnapshotOf(sim, tile, 1)!.Feature);
        Assert.Null(sight.SnapshotOf(sim, tile, 1)!.City);
        plot.Feature = null; plot.Route = null; plot.Terrain = "world:ocean";
        var restored = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), restored.ComputeHash());
        sight.Recompute(restored);
        Assert.Equal(before, sight.SnapshotOf(restored, tile, 0));
        Observer(restored, 0, tile); sight.Recompute(restored);
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
    public void Warming_reveals_sight_after_removing_an_obstructing_feature()
    {
        var sim = Scenario(); var grid = sim.State.Grid;
        var origin = grid.TileAt(3, 3); var target = grid.TileAt(5, 3); var blocker = grid.TileAt(4, 3);
        sim.State.Plots[target.Value].Kind = PlotKind.Hills;
        sim.State.Plots[blocker.Value].Feature = "world:forest";
        Observer(sim, 0, origin);
        Sight(sim).Recompute(sim);
        Assert.DoesNotContain(0, sim.State.Plots[target.Value].Visible);
        sim.State.Plots[blocker.Value].Feature = null;
        var change = new GlobalWarmingOccurred(blocker, "world:grass", "world:grass", "world:forest", null);
        var events = sim.Events.Published.Append(change).ToArray();
        sim.Events.Publish(change);
        Assert.Equal(events, sim.Events.Published);
        Assert.Contains(0, sim.State.Plots[target.Value].Visible);
        Assert.Contains(0, sim.State.Plots[target.Value].Revealed);
        Assert.Equal(PlotKind.Hills, Sight(sim).SnapshotOf(sim, target, 0)!.Kind);
    }

    [Fact]
    public void Recompute_preserves_hash_rng_and_events_across_concurrent_forks_and_reload()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        foreach (var plot in sim.State.Plots) plot.Terrain = "world:coast";
        var origin = grid.TileAt(3, 3); var target = grid.TileAt(6, 3);
        Observer(sim, 0, origin, "military:galley", extendedSight: true);
        Observer(sim, 1, origin, "military:galley", extendedSight: true);
        sim.TeamOf(1).Techs.Add("research:optics");
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 1, Position = grid.HexOf(grid.TileAt(10, 7)),
            Name = "Watch Port", Population = 4, Buildings = ["cities:granary"] });
        sim.State.Extensions.Get<MilitaryGameState>(sim.Content)!.ReconSights.Add(new(0, grid.HexOf(grid.TileAt(9, 0)), 1));
        var forks = Enumerable.Range(0, 3).Select(_ => sim.ForkSpeculative()).ToArray();
        forks[1].TeamOf(1).Techs.Remove("research:optics");
        forks[1].TeamOf(0).Techs.Add("research:optics");
        forks[2].State.Plots[grid.TileAt(4, 3).Value].Kind = PlotKind.Peak;
// Canonical hashes include zero-valued cliff fields, sight, memory, city and recon state.
        ulong[] expectedHashes = [3619044202355672266UL, 17890833774966299212UL, 5786886059300407840UL];
        ulong parentHash = sim.ComputeHash();
        var rng = sim.Rng.State;
        Parallel.For(0, forks.Length, i =>
        {
            var fork = forks[i];
            var events = fork.Events.Published.ToArray();
            sight.Recompute(fork);
            sight.Recompute(fork);
            Assert.Equal(i == 1, fork.State.Plots[target.Value].Visible.Contains(0));
            Assert.Equal(i == 0, fork.State.Plots[target.Value].Visible.Contains(1));
            Assert.Equal(rng, fork.Rng.State);
            Assert.Equal(events, fork.Events.Published);
            var loaded = SaveGame.Load(sim.Content, SaveGame.Save(fork));
            sight.Recompute(loaded);
            Assert.Equal(fork.ComputeHash(), loaded.ComputeHash());
            Assert.Equal(rng, loaded.Rng.State);
        });
        Assert.Equal(parentHash, sim.ComputeHash());
        Assert.Equal(rng, sim.Rng.State);
        Assert.Equal(expectedHashes, forks.Select(fork => fork.ComputeHash()).ToArray());
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
        var before = TerrainSnapshot.From(session, _ => 1, (_, _) => 0);
        foreach (var t in grid.Tiles.Where(t => !sim.State.Plots[t.Value].Visible.Contains(0)))
        {
            var p = sim.State.Plots[t.Value];
            p.Terrain = "world:ocean"; p.Kind = PlotKind.Peak; p.IsLake = true;
            p.Feature = "world:jungle"; p.Owner = 1; p.RiverEdges = 63;
            p.Improvement = "world:city_ruins"; p.Route = "world:road";
        }
        var after = TerrainSnapshot.From(session, _ => 1, (_, _) => 0);
        Assert.Equal(before.Relief, after.Relief); Assert.Equal(before.Terrain, after.Terrain);
        Assert.Equal(before.Feature, after.Feature); Assert.Equal(before.Owner, after.Owner);
        Assert.Equal(before.Rivers, after.Rivers);
        var next = session.DescribeTile(grid.HexOf(tile))!;
        Assert.Equal(description.Title, next.Title); Assert.Equal(description.Lines, next.Lines);
        Assert.Equal(default, session.YieldsOf(grid.HexOf(tile)));
    }

    // Overlay boundary: catches live resource/owner leaks, fabricated remembered yields, and reveal-tech bypasses.
    [Fact]
    public void Overlay_data_uses_observation_and_reveal_tech_with_yields_only_in_sight()
    {
        var sim = Scenario(); var grid = sim.State.Grid; var sight = Sight(sim);
        var tile = grid.TileAt(3, 3); var plot = sim.State.Plots[tile.Value];
        var bonus = sim.Content.Get<Ariel.Content.Schema.BonusDef>().All.First(b => b.RevealTech != null);
        plot.Bonus = bonus.Id;
        var session = new GameSession(sim);
        Assert.Null(session.OverlayOf(tile));
        plot.Revealed.Add(0); // No observation: do not reconstruct older saves from live data.
        Assert.Null(session.OverlayOf(tile));
        var observer = Observer(sim, 0, tile); sight.Recompute(sim);
        var locked = session.OverlayOf(tile)!;
        Assert.Equal(Ariel.Client.Sight.Visible, locked.Sight);
        Assert.Equal(0, locked.Owner);
        Assert.Null(locked.Resource);
        Assert.NotNull(locked.Yields);
        sim.TeamOf(0).Techs.Add(bonus.RevealTech!);
        Assert.Equal(bonus.Id, session.OverlayOf(tile)!.Resource);
        sim.State.MoveUnit(observer.Id, grid.HexOf(grid.TileAt(9, 3))); sight.Recompute(sim);
        var remembered = session.OverlayOf(tile)!;
        Assert.Equal(new TileOverlay(Ariel.Client.Sight.Fogged, bonus.Id, 0, null), remembered);
        ulong hash = sim.ComputeHash(); var rng = sim.Rng.State;
        Assert.Equal(remembered, session.OverlayOf(tile));
        Assert.Equal(hash, sim.ComputeHash()); Assert.Equal(rng, sim.Rng.State);
        foreach (var t in grid.Tiles.Where(t => !sim.State.Plots[t.Value].Visible.Contains(0)))
        {
            var hidden = sim.State.Plots[t.Value];
            hidden.Bonus = null; hidden.Owner = 1; hidden.Kind = PlotKind.Peak;
            hidden.Terrain = "world:ocean"; hidden.Feature = "world:jungle"; hidden.Improvement = "world:city_ruins";
        }
        Assert.Equal(remembered, session.OverlayOf(tile));
        var unseen = grid.TileAt(3, 7);
        Assert.Null(session.OverlayOf(unseen));
        sim.TeamOf(0).Techs.Remove(bonus.RevealTech!);
        Assert.Null(session.OverlayOf(tile)!.Resource); // Even remembered bonuses recheck current knowledge.
        sim.State.MoveUnit(observer.Id, grid.HexOf(tile)); sight.Recompute(sim);
        var fresh = session.OverlayOf(tile)!;
        Assert.Equal(Ariel.Client.Sight.Visible, fresh.Sight);
        Assert.Equal(1, fresh.Owner);
        Assert.Null(fresh.Resource);
        Assert.NotNull(fresh.Yields);
    }

    // Local preference ownership: catches commands, RNG use, state/revision invalidation or reset on UI/turn changes.
    [Fact]
    public void All_overlay_combinations_are_session_local_and_do_not_invalidate_the_world()
    {
        var sim = Scenario(); var tile = sim.State.Grid.TileAt(3, 3);
        Observer(sim, 0, tile); Sight(sim).Recompute(sim);
        var session = new GameSession(sim);
        Assert.Equal(new MapOverlays(false, true, false, true), session.Overlays);
        ulong hash = sim.ComputeHash(); var rng = sim.Rng.State;
        var events = sim.Events.Published.ToArray();
        int revision = session.Revision, worldRevision = session.WorldRevision;
        for (int bits = 0; bits < 16; bits++)
        {
            session.Overlays = new((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0);
            Assert.NotNull(session.OverlayOf(tile)!.Yields);
            Assert.Equal(hash, sim.ComputeHash()); Assert.Equal(rng, sim.Rng.State);
            Assert.Equal(events, sim.Events.Published);
            Assert.Equal(revision, session.Revision); Assert.Equal(worldRevision, session.WorldRevision);
        }
        session.SelectAt(sim.State.Grid.HexOf(tile));
        session.ClearSelection();
        int turn = sim.State.Turn;
        session.EndTurn();
        Assert.Equal(turn + 1, sim.State.Turn);
        Assert.Equal(new MapOverlays(true, true, true, true), session.Overlays);
        Assert.Equal(new MapOverlays(false, true, false, true), new GameSession(sim).Overlays);
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
