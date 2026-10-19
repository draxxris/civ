using Civ.Game;
using Xunit;

namespace Civ.Tests;

public class HexGridTests
{
    [Fact]
    public void OddRowCoordinatesAndTileIndicesRoundTripWithWrapNormalization()
    {
        var grid = new HexGrid(new(4, 3, true));
        Assert.Equal(new Hex(3, 1), grid.HexOf(new TileId(7)));
        Assert.Equal((3, 1), HexGrid.ToOffset(new Hex(3, 1)));
        Assert.Equal(new TileId(7), grid.TileAt(-1, 1));
        Assert.Equal(new TileId(7), grid.TileAt(7, 1));
        Assert.Equal(new Hex(3, 1), grid.Normalize(new Hex(-1, 1)));
        Assert.Equal(new Hex(2, 3), HexGrid.FromOffset(3, 3));
        foreach (var tile in grid.Tiles)
        {
            var (col, row) = grid.OffsetOf(tile);
            Assert.Equal(tile, grid.TileAt(HexGrid.FromOffset(col, row)));
            Assert.Equal(tile, grid.TileAt(col, row));
        }
        Assert.False(grid.TryTile(0, -1, out _));
        Assert.False(grid.TryTile(0, 3, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.HexOf(new TileId(12)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HexGrid(new(0, 3)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RangeAndRingClipAtEdgesAndHaveDeterministicDistinctTileOrder(bool wrap)
    {
        var grid = new HexGrid(new(4, 3, wrap));
        var origin = grid.TileAt(0, 0);
        Assert.Equal(wrap ? new[] { 0, 1, 3, 4, 7 } : new[] { 0, 1, 4 }, grid.Range(origin, 1).Select(t => t.Value));
        var center = grid.TileAt(0, 1);
        Assert.Equal(wrap ? new[] { 0, 1, 4, 5, 7, 8, 9 } : new[] { 0, 1, 4, 5, 8, 9 }, grid.Range(center, 1).Select(t => t.Value));
        Assert.Equal(grid.Range(center, 1).Where(t => t != center), grid.Ring(center, 1));
        Assert.Equal(12, grid.Range(center, 20).Count());
        Assert.Empty(grid.Ring(center, 20));
        Assert.Equal(grid.Ring(center, 2).OrderBy(t => t.Value), grid.Ring(center, 2));
        var neighbors = grid.Neighbors(center).ToArray();
        Assert.Equal(wrap ? new[] { 5, 1, 0, 7, 8, 9 } : new[] { 5, 1, 0, 8, 9 }, neighbors.Select(t => t.Value));
        foreach (var tile in grid.Tiles)
            foreach (int direction in Enumerable.Range(0, 6))
                if (grid.Neighbor(tile, direction) is { } next)
                    Assert.Equal(tile, grid.Neighbor(next, (direction + 3) % 6));
    }

    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(1, 5, false)]
    [InlineData(2, 1, false)]
    [InlineData(2, 5, false)]
    [InlineData(3, 4, false)]
    [InlineData(4, 3, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 5, true)]
    [InlineData(2, 1, true)]
    [InlineData(2, 5, true)]
    [InlineData(3, 4, true)]
    [InlineData(4, 3, true)]
    public void RangeMatchesBreadthFirstMembershipInAscendingDistinctTileOrder(int width, int height, bool wrap)
    {
        var grid = new HexGrid(new(width, height, wrap));
        foreach (var center in grid.Tiles)
        {
            var distances = BreadthFirstDistances(grid, center);
            int maximumRadius = distances.Values.Max() + 1;
            for (int radius = 0; radius <= maximumRadius; radius++)
            {
                var expected = distances
                    .Where(pair => pair.Value <= radius)
                    .Select(pair => pair.Key)
                    .OrderBy(tile => tile.Value)
                    .ToArray();
                Assert.Equal(expected, grid.Range(center, radius));
            }
        }
    }

    [Fact]
    public void RangeRejectsNegativeRadiusAndInvalidCenterImmediately()
    {
        var grid = new HexGrid(new(3, 2, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Range(new TileId(0), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Range(new TileId(-1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Range(new TileId(grid.Count), 0));
    }

    [Theory]
    [InlineData(1, 7, true)]
    [InlineData(2, 8, true)]
    [InlineData(4, 12, true)]
    [InlineData(5, 4, true)]
    [InlineData(5, 4, false)]
    public void WrappedDistanceEqualsShortestNeighborPathEvenOnTallNarrowMaps(int width, int height, bool wrap)
    {
        var grid = new HexGrid(new(width, height, wrap));
        foreach (var source in grid.Tiles)
        {
            var distances = new Dictionary<TileId, int> { [source] = 0 };
            var queue = new Queue<TileId>(); queue.Enqueue(source);
            while (queue.TryDequeue(out var tile))
                foreach (var n in grid.Neighbors(tile))
                    if (distances.TryAdd(n, distances[tile] + 1)) queue.Enqueue(n);
            foreach (var target in grid.Tiles)
                Assert.Equal(distances[target], grid.Distance(source, target));
        }
        Assert.All(grid.Tiles, t => Assert.DoesNotContain(t, grid.Neighbors(t)));
    }

    [Fact]
    public void HexRingWalksCounterClockwiseOnScreenFromTheSouthWestCorner()
    {
        var ring = Hex.Ring(new Hex(0, 0), 2).ToArray();
        Assert.Equal(new Hex(-2, 2), ring[0]);
        Assert.Equal(12, ring.Distinct().Count());
        for (int i = 0; i < ring.Length; i++) Assert.Equal(1, ring[i].DistanceTo(ring[(i + 1) % ring.Length]));
        // Screen y grows downward, so a negative shoelace sum means counter-clockwise as drawn.
        var points = ring.Select(h => h.ToPixel(1)).ToArray();
        double twiceArea = 0;
        for (int i = 0; i < points.Length; i++)
        {
            var (x1, y1) = points[i];
            var (x2, y2) = points[(i + 1) % points.Length];
            twiceArea += x1 * y2 - x2 * y1;
        }
        Assert.True(twiceArea < 0);
    }

    [Fact]
    public void RiverEdgesIgnoreOffMapEdgesOnTopAndBottomRows()
    {
        var state = new GameState { Map = new(4, 3), Plots = Enumerable.Range(0, 12).Select(_ => new Plot()).ToArray() };
        var grid = state.Grid;
        int last = state.Map.Height - 1;
        foreach (int col in Enumerable.Range(0, state.Map.Width))
            foreach (var tile in new[] { grid.TileAt(col, 0), grid.TileAt(col, last) })
                foreach (int direction in Enumerable.Range(0, 6))
                    if (grid.Neighbor(tile, direction) is null) state.SetRiver(tile, direction, true);
        Assert.All(state.Plots, p => Assert.Equal(0, p.RiverEdges));

        var middle = grid.TileAt(1, 1);
        var east = grid.Neighbor(middle, 0)!.Value;
        state.SetRiver(middle, 0, true);
        Assert.Equal(1, state.Plots[middle.Value].RiverEdges);
        Assert.Equal(1 << 3, state.Plots[east.Value].RiverEdges);

        var top = grid.TileAt(0, 0);
        state.Plots[top.Value].RiverEdges = 1 << 1; // stale off-map bit from older data
        state.SetRiver(top, 1, false);
        Assert.Equal(0, state.Plots[top.Value].RiverEdges);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CliffEdgesMirrorAcrossNeighborsAndClearStaleOffMapBits(bool wrap)
    {
        var state = new GameState { Map = new(4, 3, wrap), Plots = Enumerable.Range(0, 12).Select(_ => new Plot()).ToArray() };
        foreach (var tile in state.Grid.Tiles)
            for (int direction = 0; direction < 6; direction++)
            {
                state.SetCliff(tile, direction, true);
                if (state.Grid.Neighbor(tile, direction) is { } next)
                {
                    Assert.NotEqual(0, state.Plots[tile.Value].CliffEdges & (1 << direction));
                    Assert.NotEqual(0, state.Plots[next.Value].CliffEdges & (1 << ((direction + 3) % 6)));
                    var copy = state.Clone();
                    state.SetCliff(tile, direction, false);
                    Assert.NotEqual(0, copy.Plots[tile.Value].CliffEdges & (1 << direction));
                    Assert.Equal(0, state.Plots[next.Value].CliffEdges & (1 << ((direction + 3) % 6)));
                }
                else
                {
                    Assert.Equal(0, state.Plots[tile.Value].CliffEdges & (1 << direction));
                    state.Plots[tile.Value].CliffEdges |= (byte)(1 << direction);
                    state.SetCliff(tile, direction, true);
                    Assert.Equal(0, state.Plots[tile.Value].CliffEdges & (1 << direction));
                }
            }
        Assert.All(state.Plots, p => Assert.Equal(0, p.CliffEdges));
    }

    [Fact]
    public void WrappedDistanceMatchesBreadthFirstPathsOnLargerMap()
    {
        var grid = new HexGrid(new(24, 16, true));
        int maximum = 0;
        foreach (var source in grid.Tiles)
        {
            var distances = BreadthFirstDistances(grid, source);
            foreach (var target in grid.Tiles)
            {
                Assert.Equal(distances[target], grid.Distance(source, target));
                maximum = Math.Max(maximum, distances[target]);
            }
        }
        Assert.Equal(maximum, grid.MaximumDistance);
    }

    private static Dictionary<TileId, int> BreadthFirstDistances(HexGrid grid, TileId source)
    {
        var distances = new Dictionary<TileId, int> { [source] = 0 };
        var queue = new Queue<TileId>();
        queue.Enqueue(source);
        while (queue.TryDequeue(out var tile))
            for (int direction = 0; direction < 6; direction++)
                if (grid.Neighbor(tile, direction) is { } next && distances.TryAdd(next, distances[tile] + 1))
                    queue.Enqueue(next);
        return distances;
    }
}
