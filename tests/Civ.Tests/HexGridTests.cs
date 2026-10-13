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
        var neighbors = grid.Neighbors(center).ToArray();
        Assert.Equal(wrap ? new[] { 5, 1, 0, 7, 8, 9 } : new[] { 5, 1, 0, 8, 9 }, neighbors.Select(t => t.Value));
        foreach (var tile in grid.Tiles)
            foreach (int direction in Enumerable.Range(0, 6))
                if (grid.Neighbor(tile, direction) is { } next)
                    Assert.Equal(tile, grid.Neighbor(next, (direction + 3) % 6));
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
}
