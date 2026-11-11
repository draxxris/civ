using Ariel.Content.Schema;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The shape every generated river must have: a chain of edges between land tiles, joined corner to corner,
/// that reaches water at a corner, never runs along a shore, has no stubs, and
/// never wraps around or splits a tile.</summary>
public sealed class RiverGenerationTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const int MinEdges = 4, MaxTileEdges = 3;

    [Theory]
    [InlineData("world:continents")]
    [InlineData("world:inland_sea")]
    [InlineData("world:king_of_the_hill")]
    [InlineData("world:new_world")]
    [InlineData("world:twin_continents")]
    [InlineData("world:great_plains")]
    [Trait("Category", "WorldGen")]
    public void Generated_rivers_run_corner_to_corner_between_land_tiles(string generator)
    {
        foreach (ulong seed in new ulong[] { 1, 42 })
        {
            var sim = new Simulation(mods.Content, GameFactory.Settings(seed) with { MapGenerator = generator });
            var grid = sim.State.Grid;
            var corners = new HexCorners(grid);
            bool Water(TileId tile) => sim.Content.Get<TerrainDef>(sim.State.Plots[tile.Value].Terrain).IsWater;
            var degree = new Dictionary<int, int>();
            var links = new Dictionary<int, List<int>>();
            int edges = 0;
            foreach (var tile in grid.Tiles)
            {
                int mask = sim.State.Plots[tile.Value].RiverEdges;
                if (mask == 0) continue;
                string where = $"{generator} seed {seed} tile {grid.OffsetOf(tile)}";
                Assert.False(Water(tile), $"river bits on water at {where}");
                Assert.True(System.Numerics.BitOperations.PopCount((uint)mask) <= MaxTileEdges, $"too many river edges at {where}");
                int runs = Enumerable.Range(0, 6).Count(d => (mask & (1 << d)) != 0 && (mask & (1 << ((d + 5) % 6))) == 0);
                Assert.True(runs <= 1, $"separate river runs at {where}");
                for (int d = 0; d < 6; d++)
                {
                    if ((mask & (1 << d)) == 0) continue;
                    var next = grid.Neighbor(tile, d);
                    Assert.NotNull(next);
                    Assert.False(Water(next.Value), $"river along a shore at {where}");
                    if (next.Value.Value < tile.Value) continue;
                    var (a, b) = corners.Ends(tile, d);
                    Link(a, b); Link(b, a);
                    edges++;
                }
            }
            Assert.True(edges > 0, $"{generator} seed {seed} has no rivers");

            bool Wet(int corner)
            {
                Span<TileId> tiles = stackalloc TileId[3];
                int count = corners.Tiles(corner, tiles);
                for (int i = 0; i < count; i++) if (Water(tiles[i])) return true;
                return false;
            }
            // Each dry dead end is a source: its course runs at least MinEdges before it meets another river or water.
            foreach (var (source, _) in degree.Where(p => p.Value == 1 && !Wet(p.Key)))
            {
                int length = 0, previous = -1, corner = source;
                do
                {
                    int next = links[corner].Single(c => c != previous);
                    (previous, corner) = (corner, next);
                    length++;
                } while (degree[corner] == 2 && !Wet(corner));
                Assert.True(length >= MinEdges, $"{generator} seed {seed}: a river of {length} edges");
            }
            // Every river reaches water.
            var seen = new HashSet<int>();
            foreach (int start in degree.Keys)
            {
                if (!seen.Add(start)) continue;
                var stack = new Stack<int>([start]);
                bool wet = false;
                while (stack.TryPop(out int corner))
                {
                    wet |= Wet(corner);
                    foreach (int next in links[corner]) if (seen.Add(next)) stack.Push(next);
                }
                Assert.True(wet, $"{generator} seed {seed}: a river that reaches no water");
            }

            void Link(int from, int to)
            {
                degree[from] = degree.GetValueOrDefault(from) + 1;
                (links.TryGetValue(from, out var list) ? list : links[from] = []).Add(to);
            }
        }
    }
}
