using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A scene names the ground under a tile, and the height the ground implies follows it, so the renderer can
/// draw a terraced map from what the file says rather than from a stored lift.</summary>
public sealed class SceneHeightsTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static string Scene(string json)
    {
        var file = Path.Combine(Path.GetTempPath(), $"scene-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, json);
        return file;
    }

    private static sbyte Height(GameSession session, int col, int row) =>
        session.Sim.State.PlotAt(HexGrid.FromOffset(col, row)).Height;

    [Fact]
    public void A_scene_derives_the_height_of_the_ground_it_names_and_lets_a_tile_override_it()
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content);
        string file = Scene("""
            {
              "fill": { "terrain": "world:grass", "kind": "Flat" },
              "tiles": [
                { "at": [3, 3], "kind": "Hills", "cliff": 1 },
                { "at": [4, 3], "kind": "Flat" },
                { "at": [5, 3], "kind": "Peak", "height": 0 }
              ]
            }
            """);
        try
        {
            DebugScenario.Apply(session, file);
        }
        finally
        {
            File.Delete(file);
        }
        var state = session.Sim.State;
        Assert.Equal(1, Height(session, 3, 3));  // Hills imply tier 1.
        Assert.Equal(0, Height(session, 4, 3));  // Flat implies sea-level flat.
        Assert.Equal(0, Height(session, 5, 3));  // An explicit height wins over the Peak its ground implies.
        // The cliff bit is the east edge of the hills tile, and the renderer lifts that side because the heights differ.
        var hills = state.Grid.TileAt(HexGrid.FromOffset(3, 3));
        Assert.NotEqual(0, state.Plots[hills.Value].CliffEdges & 1);
        Assert.NotEqual(0, state.CliffLifts(hills) & 1);
        var snapshot = TerrainTestMaps.Snapshot(session);
        Assert.Equal(1, snapshot.Heights[snapshot.ClampedTile(HexGrid.FromOffset(3, 3))]);
        Assert.Equal(0, snapshot.Heights[snapshot.ClampedTile(HexGrid.FromOffset(4, 3))]);
        Assert.Equal(0, snapshot.Heights[snapshot.ClampedTile(HexGrid.FromOffset(5, 3))]);
    }

    [Fact]
    public void A_scene_may_put_a_cliff_on_a_lake_shore_and_the_rules_accept_it()
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content);
        var state = session.Sim.State;
        var land = state.Grid.TileAt(HexGrid.FromOffset(6, 6));
        var lake = state.Grid.Neighbor(land, 0)!.Value;
        string file = Scene("""
            {
              "tiles": [
                { "at": [6, 6], "terrain": "world:grass", "kind": "Flat", "cliff": true },
                { "at": [7, 6], "terrain": "world:coast", "lake": true }
              ]
            }
            """);
        try
        {
            DebugScenario.Apply(session, file);
        }
        finally
        {
            File.Delete(file);
        }
        Assert.True(state.Plots[lake.Value].IsLake);
        Assert.NotEqual(0, state.Plots[land.Value].CliffEdges & 1);
        Assert.NotEqual(0, state.CliffLifts(land) & 1);
        Assert.DoesNotContain(session.Sim.Rules().MapGeometry.Report(session.Sim),
            line => line.Contains("lake"));
    }

    [Fact]
    public void A_fill_repaint_takes_the_height_of_the_new_ground_and_drops_the_cliffs_cut_into_the_old_one()
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content);
        var state = session.Sim.State;
        var tile = state.Grid.TileAt(HexGrid.FromOffset(6, 6));
        var neighbor = state.Grid.Neighbor(tile, 0)!.Value;
        state.Plots[tile.Value].Kind = PlotKind.Hills;
        state.Plots[tile.Value].Height = 1;
        state.SetCliff(tile, 0, true);
        Assert.Equal(1, state.Plots[tile.Value].Height);

        string file = Scene("""{ "fill": { "terrain": "world:grass", "kind": "Flat" } }""");
        try
        {
            DebugScenario.Apply(session, file);
        }
        finally
        {
            File.Delete(file);
        }
        Assert.Equal(0, state.Plots[tile.Value].Height);
        Assert.Equal(0, state.Plots[tile.Value].CliffEdges);
        Assert.Equal(0, state.Plots[neighbor.Value].CliffEdges);
        Assert.Equal(0, state.CliffLifts(tile));
    }
}
