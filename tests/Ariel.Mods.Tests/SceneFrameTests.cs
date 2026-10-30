using Ariel.Client;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A scene's own world must place the file's tiles exactly as the file lays them out. Cliff and river data are
/// six direction bits, and a direction index names a different neighbour on an even row than on an odd one, so a frame
/// that moves rows by an odd count turns every diagonal bit into a different edge.</summary>
public sealed class SceneFrameTests
{
    [Theory]
    [InlineData(10, 7, 12, 15)]   // cliffs-two-states: an odd first row, the scene that first exposed the flip
    [InlineData(10, 10, 12, 18)]  // an even first row, which the same rule has to leave alone
    [InlineData(5, 5, 5, 5)]      // one tile, small enough that the minimum map size decides the frame
    public void Frame_keeps_every_tile_between_the_neighbours_the_file_names(int col0, int row0, int col1, int row1)
    {
        var frame = SceneFrame.Fit(col0, row0, col1, row1);
        var map = new HexGrid(new MapSpec(frame.Width, frame.Height));
        for (int row = row0; row <= row1; row++)
            for (int col = col0; col <= col1; col++)
            {
                Assert.InRange(col + frame.ShiftCol, SceneFrame.Margin, frame.Width - 1 - SceneFrame.Margin);
                Assert.InRange(row + frame.ShiftRow, SceneFrame.Margin, frame.Height - 1 - SceneFrame.Margin);
                var framed = map.TileAt(col + frame.ShiftCol, row + frame.ShiftRow);
                for (int direction = 0; direction < 6; direction++)
                {
                    // A cliff or river entry on this tile at this direction must land on the edge the file drew.
                    var (ncol, nrow) = HexGrid.ToOffset(HexGrid.FromOffset(col, row).Neighbor(direction));
                    Assert.Equal(map.TileAt(ncol + frame.ShiftCol, nrow + frame.ShiftRow), map.Neighbor(framed, direction));
                }
            }
    }
}
