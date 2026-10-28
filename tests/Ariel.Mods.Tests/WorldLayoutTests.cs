using Ariel;
using Ariel.Client;
using Ariel.Content.Schema;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class WorldLayoutTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    // The public setup boundary owns layout geometry, start reservations, and relief contracts.
    [Theory]
    [InlineData("world:inland_sea", "Inland Sea", 1UL)]
    [InlineData("world:inland_sea", "Inland Sea", 42UL)]
    [InlineData("world:king_of_the_hill", "King of the Hill", 1UL)]
    [InlineData("world:king_of_the_hill", "King of the Hill", 42UL)]
    [InlineData("world:new_world", "New World", 1UL)]
    [InlineData("world:new_world", "New World", 42UL)]
    [InlineData("world:new_world", "New World", 987654UL)]
    [InlineData("world:twin_continents", "Twin Continents", 1UL)]
    [InlineData("world:twin_continents", "Twin Continents", 42UL)]
    [InlineData("world:great_plains", "Great Plains", 1UL)]
    [InlineData("world:great_plains", "Great Plains", 42UL)]
    [Trait("Category", "WorldGen")]
    public void Layouts_keep_their_landmarks_and_supply_separated_starts(string generator, string name, ulong seed)
    {
        var catalog = mods.Catalog;
        Assert.Equal(name, Assert.Single(catalog.MapGenerators, c => c.Id == generator).Name);
        foreach (string size in new[] { "duel", "standard", "gigantic" })
        {
            var settings = (catalog.DefaultOptions() with
            {
                MapGenerator = generator, Seed = seed, Opponents = 3, WorldSize = "foundation:worldsize_" + size
            }).ToSettings(catalog) with { PlayerAis = [] };
            var sim = new Simulation(catalog.Content, settings);
            AssertLandmarks(sim, generator);
            var starts = sim.State.Players.Select(p => p.StartingPlot!.Value).ToArray();
            Assert.Equal(4, starts.Length);
            foreach (var start in starts)
            {
                Assert.False(Water(sim, sim.State.Plots[start.Value]));
                Assert.NotEqual(PlotKind.Peak, sim.State.Plots[start.Value].Kind);
                foreach (var other in starts.Where(t => t != start)) Assert.True(sim.State.Grid.Distance(start, other) >= 10);
                if (generator == "world:new_world")
                {
                    Assert.Equal(At(sim, .25, .5).AreaId, sim.State.Plots[start.Value].AreaId);
                    Assert.True(sim.State.Grid.OffsetOf(start).Col < sim.State.Map.Width / 2);
                }
            }
        }
    }

    private static Plot At(Simulation sim, double x, double y) => sim.State.Plots[sim.State.Grid.TileAt(
        (int)(x * (sim.State.Map.Width - 1)), (int)(y * (sim.State.Map.Height - 1))).Value];
    private static bool Water(Simulation sim, Plot plot) => sim.Content.Get<TerrainDef>(plot.Terrain).IsWater;

    private static void AssertLandmarks(Simulation sim, string generator)
    {
        var center = At(sim, .5, .5);
        switch (generator)
        {
            case "world:inland_sea":
                Assert.True(Water(sim, center));
                Assert.False(Water(sim, At(sim, .05, .5)));
                Assert.False(Water(sim, At(sim, .5, .05)));
                break;
            case "world:king_of_the_hill":
                Assert.False(Water(sim, center));
                Assert.Equal(center.AreaId, At(sim, .8, .5).AreaId);
                Assert.Equal(center.AreaId, At(sim, .7, .3).AreaId);
                Assert.True(Water(sim, At(sim, .8, .375)));
                Assert.Equal(center.AreaId, At(sim, .925, .5).AreaId);
                Assert.True(Water(sim, At(sim, .02, .02)));
                break;
            case "world:new_world":
            case "world:twin_continents":
                AssertContinents(sim);
                break;
            case "world:great_plains":
                Assert.True(Water(sim, At(sim, .25, .35)));
                Assert.True(Water(sim, At(sim, .7, .65)));
                Assert.False(Water(sim, center));
                Assert.True(sim.State.Plots.Count(p => Water(sim, p)) < sim.State.Plots.Length / 10);
                Assert.DoesNotContain(sim.State.Plots, p => p.Kind == PlotKind.Peak);
                Assert.InRange(sim.State.Plots.Count(p => p.Kind == PlotKind.Hills), 1, sim.State.Plots.Length / 10);
                break;
        }
    }

    private static void AssertContinents(Simulation sim)
    {
        var west = At(sim, .25, .5);
        var east = At(sim, .75, .5);
        Assert.False(Water(sim, west));
        Assert.False(Water(sim, east));
        Assert.NotEqual(west.AreaId, east.AreaId);
        Assert.All(sim.State.Grid.Tiles.Where(t => sim.State.Grid.OffsetOf(t).Col == sim.State.Map.Width / 2),
            t => Assert.True(Water(sim, sim.State.Plots[t.Value])));
        // The wrap seam must not join the two landmasses either.
        Assert.All(sim.State.Grid.Tiles.Where(t => sim.State.Grid.OffsetOf(t).Col == 0),
            t => Assert.True(Water(sim, sim.State.Plots[t.Value])));
    }
}
