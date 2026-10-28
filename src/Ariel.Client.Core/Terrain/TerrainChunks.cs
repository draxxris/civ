namespace Ariel.Client.Terrain;

/// <summary>A world-space rectangle of terrain, measured in whole tile columns (√3 world units each) and tile rows
/// (1.5 world units each), so its edges land on lattice lines for any mesh resolution.</summary>
public sealed record TerrainChunk(int Id, int Column, int Row, int UnitX0, int UnitX1, int UnitZ0, int UnitZ1)
{
    private static readonly double Sqrt3 = Math.Sqrt(3);

    /// <summary>West edge in world x.</summary>
    public double X0 => UnitX0 * Sqrt3;
    /// <summary>East edge in world x.</summary>
    public double X1 => UnitX1 * Sqrt3;
    /// <summary>North edge in world z.</summary>
    public double Z0 => UnitZ0 * 1.5;
    /// <summary>South edge in world z.</summary>
    public double Z1 => UnitZ1 * 1.5;

    /// <summary>Whether a world point lies in the half-open rectangle [X0, X1) × [Z0, Z1).</summary>
    public bool Contains(double x, double z) => x >= X0 && x < X1 && z >= Z0 && z < Z1;
}

/// <summary>A chunk's lattice resolution (<see cref="TerrainMesher"/>) and those of its eight neighbours. A missing
/// neighbour (beyond the north or south edge, or across the edge of a map that does not wrap) has the chunk's own.</summary>
public sealed record ChunkResolution(int K, int West, int East, int North, int South, int NorthWest, int NorthEast,
    int SouthWest, int SouthEast)
{
    /// <summary>The same resolution everywhere.</summary>
    public static ChunkResolution Uniform(int k) => new(k, k, k, k, k, k, k, k, k);
}

/// <summary>Partition of one map period into chunks of 4 × 4 tiles, plus the per-chunk dirty signatures that let a
/// renderer rebuild only the chunks whose geometry or scatter inputs changed. Chunks cover x in [0, Period) and z from one
/// tile row beyond the north edge to one tile row beyond the south edge.</summary>
public sealed class TerrainChunks
{
    /// <summary>Tile columns per chunk.</summary>
    public const int TileColumns = 4;
    /// <summary>Tile rows per chunk.</summary>
    public const int TileRows = 4;
    /// <summary>First chunk row starts this many tile rows north of row 0 (z = -3).</summary>
    public const int NorthMarginUnits = 2;

    private static readonly double Sqrt3 = Math.Sqrt(3);
    private readonly int[][] _tiles, _riverTiles, _cliffTiles;

    /// <summary>Partition a map. The ring of tiles hashed per chunk is wide enough for the height field with
    /// <paramref name="tuning"/> sampled on a lattice of resolution <paramref name="resolution"/>.</summary>
    public TerrainChunks(MapSpec map, TerrainTuning? tuning = null, int resolution = TerrainMesher.DefaultResolution)
    {
        if (resolution < 1) throw new ArgumentOutOfRangeException(nameof(resolution));
        Map = map;
        var grid = new HexGrid(map);
        // The field reads tiles within InfluenceRadius of a sample, and mesh normals and cavity also sample lattice
        // vertices up to two steps beyond the chunk (at the coarsest resolution).
        double step = Sqrt3 / resolution;
        tuning ??= new TerrainTuning();
        RingRadius = tuning.InfluenceRadius + 2 * step;
        RiverRingRadius = tuning.RiverReach + 2 * step;
        // A plateau tile lifts land up to CliffRamp beyond its hexagon, and whether it is a plateau depends on its neighbours.
        CliffRingRadius = tuning.InfluenceRadius + tuning.CliffRamp + 2 * Sqrt3 + 2 * step;
        int zEnd = map.Height + 1; // last row centre + 2 tile rows (3 world units)
        Columns = (map.Width + TileColumns - 1) / TileColumns;
        Rows = (zEnd + NorthMarginUnits + TileRows - 1) / TileRows;
        var chunks = new List<TerrainChunk>(Columns * Rows);
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
            {
                int z0 = -NorthMarginUnits + row * TileRows;
                chunks.Add(new(chunks.Count, col, row, col * TileColumns, Math.Min((col + 1) * TileColumns, map.Width),
                    z0, Math.Min(z0 + TileRows, zEnd)));
            }
        Chunks = chunks;
        _tiles = chunks.Select(c => InfluenceTiles(c, grid, RingRadius)).ToArray();
        _riverTiles = chunks.Select(c => InfluenceTiles(c, grid, RiverRingRadius)).ToArray();
        _cliffTiles = chunks.Select(c => InfluenceTiles(c, grid, CliffRingRadius)).ToArray();
    }

    /// <summary>Partitioned map.</summary>
    public MapSpec Map { get; }
    /// <summary>Chunks per row of chunks.</summary>
    public int Columns { get; }
    /// <summary>Rows of chunks.</summary>
    public int Rows { get; }
    /// <summary>All chunks; <see cref="TerrainChunk.Id"/> is the index.</summary>
    public IReadOnlyList<TerrainChunk> Chunks { get; }
    /// <summary>Distance beyond a chunk's rectangle within which a tile centre can change the chunk's geometry.</summary>
    public double RingRadius { get; }
    /// <summary>Distance beyond a chunk's rectangle within which a tile's river bits or relief can change the chunk's river
    /// geometry (see <see cref="TerrainTuning.RiverReach"/>).</summary>
    public double RiverRingRadius { get; }
    /// <summary>Distance beyond a chunk's rectangle within which a tile's cliff bits or relief can lift the chunk's ground
    /// (the plateau ramp, see <see cref="TerrainTuning.CliffRamp"/>).</summary>
    public double CliffRingRadius { get; }

    /// <summary>Lattice resolution of one chunk: <see cref="TerrainMesher.PeakResolution"/> when a peak or a cliff can shape
    /// it (cliff faces and sea-stack shoals are finer than the default lattice), otherwise
    /// <see cref="TerrainMesher.DefaultResolution"/>.</summary>
    public int ResolutionOf(TerrainChunk chunk, TerrainSnapshot snapshot) =>
        _tiles[chunk.Id].Any(t => snapshot.Relief[t] == ReliefClass.Peak || snapshot.Cliffs[t] != 0)
            ? TerrainMesher.PeakResolution : TerrainMesher.DefaultResolution;

    /// <summary>Lattice resolution of every chunk, indexed by chunk id.</summary>
    public int[] Resolutions(TerrainSnapshot snapshot) => Chunks.Select(c => ResolutionOf(c, snapshot)).ToArray();

    /// <summary>A chunk's resolution and its neighbours', from <see cref="Resolutions"/>.</summary>
    public ChunkResolution Resolution(TerrainChunk chunk, IReadOnlyList<int> resolutions)
    {
        int k = resolutions[chunk.Id];
        int Of(int dc, int dr) => Neighbour(chunk, dc, dr) is int id and >= 0 ? resolutions[id] : k;
        return new(k, Of(-1, 0), Of(1, 0), Of(0, -1), Of(0, 1), Of(-1, -1), Of(1, -1), Of(-1, 1), Of(1, 1));
    }

    /// <summary>A chunk's resolution and its neighbours' for a snapshot.</summary>
    public ChunkResolution Resolution(TerrainChunk chunk, TerrainSnapshot snapshot)
    {
        int k = ResolutionOf(chunk, snapshot);
        int Of(int dc, int dr) => Neighbour(chunk, dc, dr) is int id and >= 0 ? ResolutionOf(Chunks[id], snapshot) : k;
        return new(k, Of(-1, 0), Of(1, 0), Of(0, -1), Of(0, 1), Of(-1, -1), Of(1, -1), Of(-1, 1), Of(1, 1));
    }

    /// <summary>Resolution of the chunk that holds world point (x, z) (wrapped on a wrapping map, clamped to the chunk
    /// rows), from <see cref="Resolutions"/>.</summary>
    public int ResolutionAt(double x, double z, IReadOnlyList<int> resolutions)
    {
        double period = Map.Width * Sqrt3;
        if (Map.Wrap) x -= period * Math.Floor(x / period);
        int col = Math.Clamp((int)Math.Floor(x / Sqrt3) / TileColumns, 0, Columns - 1);
        int row = Math.Clamp((int)Math.Floor((z / 1.5 + NorthMarginUnits) / TileRows), 0, Rows - 1);
        return resolutions[row * Columns + col];
    }

    /// <summary>Id of the chunk dc columns and dr rows away, wrapping columns on a wrapping map; -1 when there is none.</summary>
    private int Neighbour(TerrainChunk chunk, int dc, int dr)
    {
        int row = chunk.Row + dr, col = chunk.Column + dc;
        if (row < 0 || row >= Rows) return -1;
        if (col < 0 || col >= Columns)
        {
            if (!Map.Wrap) return -1;
            col = (col + Columns) % Columns;
        }
        return row * Columns + col;
    }

    /// <summary>64-bit hash of everything that shapes a chunk's geometry and scatter: layer, relief class (which includes
    /// water and the hidden flag), river and cliff bits and feature ground of its tiles and their ring, its mesh resolution (and its
    /// neighbours' when it is finer than the default), plus an optional per-tile
    /// <paramref name="extras"/> value (for example feature id, city or improvement presence) indexed by tile. When any
    /// tile in the wider river ring has river bits, the river bits and relief of that whole ring count too; likewise for cliff
    /// bits in the wider cliff ring.</summary>
    public ulong Signature(TerrainChunk chunk, TerrainSnapshot snapshot, IReadOnlyList<int>? extras = null)
    {
        if (snapshot.Grid.Spec != Map) throw new ArgumentException("Snapshot is for a different map", nameof(snapshot));
        ulong h = 0xcbf29ce484222325UL;
        foreach (int t in _tiles[chunk.Id])
        {
            ulong v = (ulong)(uint)t << 32 | (ulong)snapshot.Layer[t] << 24 | (ulong)snapshot.Relief[t] << 16
                | (ulong)snapshot.Rivers[t] << 8 | snapshot.FeatureGround[t];
            h = Mix(h ^ v);
            h = Mix(h ^ snapshot.Cliffs[t]);
            h = MixString(h, snapshot.Terrain[t]);
            h = MixString(h, snapshot.Feature[t]);
            if (extras != null) h = Mix(h ^ (uint)extras[t]);
        }
        // A fine chunk follows the edges of coarser neighbours, so their resolutions shape it too.
        var resolution = Resolution(chunk, snapshot);
        h = Mix(h ^ (ulong)resolution.K);
        if (resolution.K > TerrainMesher.DefaultResolution)
            foreach (int k in new[] { resolution.West, resolution.East, resolution.North, resolution.South, resolution.NorthWest,
                resolution.NorthEast, resolution.SouthWest, resolution.SouthEast })
                h = Mix(h ^ (ulong)k);
        var cliffs = _cliffTiles[chunk.Id];
        if (cliffs.Any(t => snapshot.Cliffs[t] != 0))
            foreach (int t in cliffs)
                h = Mix(h ^ ((ulong)(uint)t << 32 | (ulong)snapshot.Relief[t] << 8 | snapshot.Cliffs[t]));
        var rivers = _riverTiles[chunk.Id];
        if (!rivers.Any(t => snapshot.Rivers[t] != 0)) return h;
        foreach (int t in rivers)
            h = Mix(h ^ ((ulong)(uint)t << 32 | (ulong)snapshot.Relief[t] << 8 | snapshot.Rivers[t]));
        return h;
    }

    /// <summary>Signatures of every chunk, indexed by chunk id.</summary>
    public ulong[] Signatures(TerrainSnapshot snapshot, IReadOnlyList<int>? extras = null) =>
        Chunks.Select(c => Signature(c, snapshot, extras)).ToArray();

    /// <summary>Chunk ids whose signature differs from <paramref name="previous"/>; every chunk when there is none.</summary>
    public static IReadOnlyList<int> DirtyChunks(IReadOnlyList<ulong>? previous, IReadOnlyList<ulong> current) =>
        Enumerable.Range(0, current.Count).Where(i => previous == null || i >= previous.Count || previous[i] != current[i]).ToList();

    private int[] InfluenceTiles(TerrainChunk chunk, HexGrid grid, double m)
    {
        var tiles = new SortedSet<int>();
        int rowFrom = (int)Math.Floor((chunk.Z0 - m) / 1.5), rowTo = (int)Math.Ceiling((chunk.Z1 + m) / 1.5);
        for (int row = rowFrom; row <= rowTo; row++)
        {
            double offset = (row & 1) * 0.5, z = row * 1.5;
            int colFrom = (int)Math.Floor((chunk.X0 - m) / Sqrt3 - offset), colTo = (int)Math.Ceiling((chunk.X1 + m) / Sqrt3 - offset);
            for (int col = colFrom; col <= colTo; col++)
            {
                double x = Sqrt3 * (col + offset);
                double dx = Math.Max(Math.Max(chunk.X0 - x, x - chunk.X1), 0), dz = Math.Max(Math.Max(chunk.Z0 - z, z - chunk.Z1), 0);
                if (dx * dx + dz * dz > m * m) continue;
                // Same resolution as the height field: rows clamp to the map, columns wrap (or clamp on a bounded map).
                int c = Map.Wrap ? col : Math.Clamp(col, 0, Map.Width - 1);
                if (grid.TryTile(c, Math.Clamp(row, 0, Map.Height - 1), out var tile)) tiles.Add(tile.Value);
            }
        }
        return tiles.ToArray();
    }

    private static ulong MixString(ulong h, string? value)
    {
        if (value != null)
            foreach (char c in value) h = Mix(h ^ c);
        return Mix(h ^ 0xff);
    }

    private static ulong Mix(ulong h)
    {
        h *= 0x100000001b3UL;
        h ^= h >> 29;
        h *= 0xbf58476d1ce4e5b9UL;
        return h ^ (h >> 32);
    }
}
