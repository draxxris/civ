using Xunit;

namespace Ariel.Tests;

public class HexCornersTests
{
    // A corner is where three hexes meet, at the centroid of their centres; corner k of a tile lies between its neighbours
    // in directions k - 1 and k. Positions are compared across the wrap seam, one map width (√3 per column) apart.
    [Theory]
    [InlineData(6, 4, true)]
    [InlineData(5, 5, false)]
    [InlineData(7, 3, true)]
    public void Tile_corners_with_one_id_stand_at_one_point_and_each_point_has_one_id(int width, int height, bool wrap)
    {
        var grid = new HexGrid(new(width, height, wrap));
        var corners = new HexCorners(grid);
        double period = Math.Sqrt(3) * width;
        (double X, double Y) Position(TileId tile, int k)
        {
            var hex = grid.HexOf(tile);
            var (x0, y0) = hex.ToPixel(1);
            var (x1, y1) = hex.Neighbor((k + 5) % 6).ToPixel(1);
            var (x2, y2) = hex.Neighbor(k).ToPixel(1);
            return ((x0 + x1 + x2) / 3, (y0 + y1 + y2) / 3);
        }
        bool Same((double X, double Y) a, (double X, double Y) b)
        {
            double dx = a.X - b.X;
            if (wrap) dx -= Math.Round(dx / period) * period;
            return Math.Abs(dx) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;
        }
        var points = new List<(int Id, (double X, double Y) At)>();
        foreach (var tile in grid.Tiles)
            for (int k = 0; k < 6; k++)
                points.Add((corners.Of(tile, k), Position(tile, k)));
        foreach (var a in points)
            foreach (var b in points)
                Assert.Equal(a.Id == b.Id, Same(a.At, b.At));
        Span<TileId> tiles = stackalloc TileId[3];
        Span<CornerEdge> edges = stackalloc CornerEdge[3];
        foreach (var group in points.GroupBy(p => p.Id))
        {
            Assert.True(corners.IsCorner(group.Key));
            Assert.Equal(group.Count(), corners.Tiles(group.Key, tiles));
            // Each edge listed at a corner runs from it to the other end of that tile edge.
            int count = corners.Edges(group.Key, edges);
            for (int i = 0; i < count; i++)
            {
                var (from, to) = corners.Ends(edges[i].Tile, edges[i].Direction);
                Assert.Contains(group.Key, new[] { from, to });
                Assert.Equal(edges[i].Other, from == group.Key ? to : from);
            }
        }
    }
}
