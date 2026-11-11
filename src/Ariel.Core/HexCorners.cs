namespace Ariel;

/// <summary>One of the up to three tile edges that meet at a hex corner: the edge in <see cref="Direction"/> from
/// <see cref="Tile"/>, and the corner at its far end.</summary>
public readonly record struct CornerEdge(TileId Tile, int Direction, int Other);

/// <summary>The corners of a hex grid, where three tiles meet, and the tile edges that join them: the lattice a river runs
/// along. Corner k of a tile ends the tile's edge k − 1 and starts its edge k (in <see cref="Hex.Directions"/> order), so
/// it is shared with the neighbours across those two edges. Every corner has one id, the least of tile * 6 + k over the
/// tiles that name it, so ids lie in [0, <see cref="Count"/>) but not every number in that range is a corner.</summary>
public sealed class HexCorners
{
    private readonly HexGrid _grid;
    private readonly int[] _ids;

    /// <summary>The corners of a grid, wrapped like the grid.</summary>
    public HexCorners(HexGrid grid)
    {
        _grid = grid;
        _ids = new int[grid.Count * 6];
        for (int tile = 0; tile < grid.Count; tile++)
            for (int k = 0; k < 6; k++)
            {
                int id = tile * 6 + k;
                if (grid.Neighbor(new TileId(tile), k) is { } after) id = Math.Min(id, after.Value * 6 + (k + 4) % 6);
                if (grid.Neighbor(new TileId(tile), (k + 5) % 6) is { } before) id = Math.Min(id, before.Value * 6 + (k + 2) % 6);
                _ids[tile * 6 + k] = id;
            }
    }

    /// <summary>Size of the id range: six per tile.</summary>
    public int Count => _ids.Length;

    /// <summary>The id of a tile's corner <paramref name="corner"/> (taken modulo 6).</summary>
    public int Of(TileId tile, int corner) => _ids[tile.Value * 6 + corner % 6];

    /// <summary>Whether an id names a corner, so each corner is visited once when walking the id range.</summary>
    public bool IsCorner(int id) => _ids[id] == id;

    /// <summary>The corners at either end of a tile's edge in a direction.</summary>
    public (int From, int To) Ends(TileId tile, int direction) => (Of(tile, direction), Of(tile, direction + 1));

    /// <summary>The tiles at a corner, up to three (fewer on a map's bounded north and south rows), into
    /// <paramref name="tiles"/>; returns how many.</summary>
    public int Tiles(int corner, Span<TileId> tiles)
    {
        var (tile, k) = Split(corner);
        int count = 0;
        tiles[count++] = tile;
        if (_grid.Neighbor(tile, (k + 5) % 6) is { } before) tiles[count++] = before;
        if (_grid.Neighbor(tile, k) is { } after) tiles[count++] = after;
        return count;
    }

    /// <summary>The edges that meet at a corner, up to three, into <paramref name="edges"/>; returns how many. An edge is
    /// listed only where tiles stand on both of its sides.</summary>
    public int Edges(int corner, Span<CornerEdge> edges)
    {
        var (tile, k) = Split(corner);
        int count = 0;
        var before = _grid.Neighbor(tile, (k + 5) % 6);
        var after = _grid.Neighbor(tile, k);
        if (before is not null) edges[count++] = new(tile, (k + 5) % 6, Of(tile, k + 5));
        if (after is { } next)
        {
            edges[count++] = new(tile, k, Of(tile, k + 1));
            // The third edge runs between the two neighbours, away from the tile: the neighbour across edge k sees this
            // corner as its corner k + 4, at the start of its edge k + 4, which leads to the neighbour across edge k − 1.
            if (before is not null) edges[count++] = new(next, (k + 4) % 6, Of(next, k + 5));
        }
        return count;
    }

    private (TileId Tile, int Corner) Split(int corner) => (new TileId(corner / 6), corner % 6);
}
