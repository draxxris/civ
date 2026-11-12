using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The slope and enclosure controls, one case per arrangement, all built through SetCliff so the heights, the
/// presence bits and the derived lifts are what the game would hold. Directions: E=0 NE=1 NW=2 W=3 SW=4 SE=5.</summary>
public sealed class CliffSlopeRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private Simulation Flat() => Simulation.CreateScenario(mods.Content, GameFactory.Settings(), new(12, 8, false),
        Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray());

    private void Cliff(Simulation sim, int col, int row, int direction) =>
        sim.State.SetCliff(sim.State.Grid.TileAt(HexGrid.FromOffset(col, row)), direction, true);

    private IReadOnlyList<string> Report(Simulation sim) =>
        sim.Rules().MapGeometry.Report(sim);

    [Fact]
    public void One_cliff_stands_alone()
    {
        var sim = Flat();
        Cliff(sim, 4, 3, 0);
        Assert.Empty(Report(sim));
    }

    [Fact]
    public void Two_cliffs_facing_opposite_ways_on_one_tile_may_share_the_plateau_between_them()
    {
        var sim = Flat();
        Cliff(sim, 4, 3, 0);
        Cliff(sim, 4, 3, 3);
        Assert.Empty(Report(sim));
    }

    [Fact]
    public void Two_walls_on_neighbouring_edges_of_one_tile_are_fine()
    {
        var sim = Flat();
        Cliff(sim, 4, 3, 0);
        Cliff(sim, 4, 3, 5);
        Assert.Empty(Report(sim));
    }

    [Fact]
    public void Two_mounds_three_tiles_apart_keep_their_slopes_to_themselves()
    {
        var sim = Flat();
        Cliff(sim, 3, 3, 0);
        Cliff(sim, 6, 3, 0);
        Assert.Empty(Report(sim));
    }

    [Fact]
    public void A_mass_walled_on_every_side_has_no_way_down()
    {
        var sim = Flat();
        for (int d = 0; d < 6; d++) Cliff(sim, 4, 3, d);
        Assert.Contains(Report(sim), line => line.Contains("walled in"));
    }

    [Fact]
    public void Three_raised_tiles_round_one_vertex_are_walled_in()
    {
        var sim = Flat();
        foreach (int d in new[] { 2, 3, 4, 5 }) Cliff(sim, 4, 3, d);
        foreach (int d in new[] { 0, 1, 4, 5 }) Cliff(sim, 5, 3, d);
        foreach (int d in new[] { 0, 1, 2, 3 }) Cliff(sim, 5, 2, d);
        Assert.Contains(Report(sim), line => line.Contains("walled in"));
    }

    [Fact]
    public void A_two_tile_bluff_leaves_its_slope_no_room_before_the_next_face()
    {
        var sim = Flat();
        Cliff(sim, 4, 3, 4);   // SW
        Cliff(sim, 5, 3, 4);   // SW
        Assert.Contains(Report(sim), line => line.Contains("meets a cliff face"));
    }

    [Fact]
    public void A_ridge_walled_along_one_side_leaves_its_slope_no_room()
    {
        var sim = Flat();
        foreach (int d in new[] { 4, 5 }) Cliff(sim, 4, 3, d);
        Cliff(sim, 5, 3, 4);
        foreach (int d in new[] { 1, 2, 4, 5 }) Cliff(sim, 6, 3, d);
        Assert.Contains(Report(sim), line => line.Contains("meets a cliff face"));
    }

    [Fact]
    public void Two_mounds_facing_each_other_across_one_low_tile_are_too_close()
    {
        var sim = Flat();
        Cliff(sim, 3, 3, 0);   // 3,3 is the high side and drops east
        Cliff(sim, 5, 3, 3);   // 5,3 is the high side and drops west, onto that same low tile
        Assert.Contains(Report(sim), line => line.Contains("too close"));
    }
}
