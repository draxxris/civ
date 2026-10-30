namespace Ariel.Client;

/// <summary>Where a hand-made scene (tools/shots/scenarios) sits in a world of its own: the offset that shifts the
/// scene's authored column,row pairs into the map, and the map size that leaves.
/// <para>A scene is a translation, never a mirror, so the row offset must keep every tile on the row parity it was
/// written with. Offsetting a pointy-top odd-row grid by an odd number of rows flips the parity of every row, and a
/// direction index names a different neighbour on an even row than on an odd one. Cliff and river data are six
/// direction bits, so an odd row offset would point every diagonal bit (northeast, northwest, southwest, southeast)
/// at the wrong tile, while east and west survived: walls would fall on edges the file never named.</para></summary>
public static class SceneFrame
{
    /// <summary>Tiles kept around the scene, so the camera can look along its edge without leaving the map.</summary>
    public const int Margin = 2;

    /// <summary>Smallest map a scene gets, however small the scene is.</summary>
    private const int MinWidth = 8, MinHeight = 6;

    /// <summary>The offset and map size that frame a non-empty scene covering the given span of authored coordinates.
    /// Column and row bounds are inclusive, lowest first.</summary>
    public readonly record struct Placement(int ShiftCol, int ShiftRow, int Width, int Height);

    /// <summary>Frame a scene that covers columns <paramref name="col0"/>..<paramref name="col1"/> and rows
    /// <paramref name="row0"/>..<paramref name="row1"/>, leaves at least <see cref="Margin"/> tiles on every side, and
    /// moves no tile to a different neighbour.</summary>
    public static Placement Fit(int col0, int row0, int col1, int row1)
    {
        int shiftCol = Margin - col0, shiftRow = Margin - row0;
        if ((shiftRow & 1) != 0) shiftRow++;
        int width = Math.Max(MinWidth, col1 + shiftCol + Margin + 1);
        int height = Math.Max(MinHeight, row1 + shiftRow + Margin + 1);
        return new(shiftCol, shiftRow, width, height);
    }
}
