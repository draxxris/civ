using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The visual river network: generator-style edge chains render as one connected river that keeps every rules
/// edge and opens into the water; river surfaces lie on the rendered terrain; and river changes dirty every chunk whose
/// terrain or river surface they reshape.</summary>
public sealed class RiverNetworkTests(RiverNetworkTests.DirtyBaseline baseline) : IClassFixture<RiverNetworkTests.DirtyBaseline>
{
    /// <summary>Share the rendered 20 × 20 map across edits; it retains distant clean chunks and both wrap edges.
    /// Build only when an edit test runs.</summary>
    public sealed class DirtyBaseline
    {
        private readonly Lazy<RenderedBaseline> _value = new(() => new());
        public RenderedBaseline Value => _value.Value;
    }

    public sealed class RenderedBaseline
    {
        public TerrainSnapshot Snapshot { get; } = TerrainTestMaps.Mixed(20, 20, seed: 3, cliffs: false);
        public TerrainChunks Chunks { get; }
        public TerrainField Field { get; }
        public TerrainMeshData[] Ground { get; }
        public RiverMeshData?[] Rivers { get; }

        public RenderedBaseline()
        {
            Chunks = new(Snapshot.Grid.Spec);
            Field = new(Snapshot, 2);
            Ground = Chunks.Chunks.Select(c => TerrainMesher.Build(c, Field)).ToArray();
            Rivers = Chunks.Chunks.Select(c => RiverMesher.Build(c, Field)).ToArray();
        }
    }
    private static readonly double Sqrt3 = TerrainTestMaps.Sqrt3;

    /// <summary>The map generator marks the edge crossed by each step of a tile-centre drainage chain. A chain running
    /// straight east crosses opposite edges of every tile, so none of its river edges touch each other.</summary>
    [Fact]
    public void A_straight_generated_chain_becomes_one_connected_river_that_keeps_its_rules_edges_and_reaches_the_sea()
    {
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.Flat);
        const int row = 6, first = 3, length = 6;
        int Tile(int col) => row * snapshot.Width + col;
        snapshot.Relief[Tile(first + length)] = ReliefClass.ShallowWater;
        for (int col = first; col < first + length; col++) TerrainTestMaps.SetRiver(snapshot, Tile(col), 0);

        var graph = RiverGraph.Build(snapshot);

        var edges = graph.Links.Where(l => l.Kind == RiverLinkKind.Edge).Select(l => Segment(l.AX, l.AZ, l.BX, l.BZ)).ToHashSet();
        var expected = Enumerable.Range(first, length - 1).Select(col => RulesEdge(snapshot, Tile(col), Tile(col + 1))).ToHashSet();
        Assert.Equal(expected, edges);
        Assert.Equal(graph.NodeCount, Reachable(graph, 0));
        var mouth = Assert.Single(graph.Links, l => l.Kind == RiverLinkKind.Mouth);
        Assert.Equal(snapshot.Grid.HexOf(new TileId(Tile(first + length))), Hex.FromPixel(mouth.BX, mouth.BZ, 1));
        // Bridges alternate sides, so the only dry end is the source.
        Assert.Single(Enumerable.Range(0, graph.NodeCount), n => graph.LinksAt(n).Count == 1 && !graph.IsOpen(n));
    }

    /// <summary>Water cannot slope across a river, so a river running along the foot of a mountain range must have its two
    /// water edges at one height, while the mountain still rises beside it.</summary>
    [Fact]
    public void A_river_beside_mountains_is_level_across_and_the_mountain_still_rises_beside_it()
    {
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.Flat);
        const int row = 6;
        for (int col = 2; col < 11; col++)
        {
            snapshot.Relief[(row - 1) * snapshot.Width + col] = ReliefClass.Peak;
            if (col < 10) TerrainTestMaps.SetRiver(snapshot, row * snapshot.Width + col, 0);
        }
        var field = new TerrainField(snapshot, 8);
        int checkedSamples = 0;
        foreach (var curve in field.Rivers.Curves)
            for (int i = 3; i < curve.Count - 3; i += 2)
            {
                double edge = curve.Width[i], nx = -curve.TZ[i], nz = curve.TX[i];
                double left = field.HeightAt(curve.X[i] + nx * edge, curve.Z[i] + nz * edge);
                double right = field.HeightAt(curve.X[i] - nx * edge, curve.Z[i] - nz * edge);
                // Within 2% of a summit's rise: on a steep bend the inner edge reads a floor a little upstream.
                Assert.Equal(left, right, 0.02);
                checkedSamples++;
            }
        Assert.True(checkedSamples > 20);
        var (px, pz) = snapshot.Grid.HexOf(new TileId((row - 1) * snapshot.Width + 6)).ToPixel(1);
        Assert.True(field.HeightAt(px, pz) > field.Tuning.PeakBase + 0.3);
    }

    /// <summary>Watermills stand on the near bank of the river closest to their tile centre: the bank point is on the
    /// waterline, the across direction points over the water away from the tile and is square to the river's course, and
    /// the answer does not depend on which wrap copy of the map is asked.</summary>
    [Fact]
    public void The_near_bank_of_a_tile_is_on_the_waterline_and_faces_straight_across_the_river()
    {
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.Flat);
        const int row = 6;
        int[] chain = [13, 14, 15, 0, 1, 2];
        foreach (int col in chain) TerrainTestMaps.SetRiver(snapshot, row * snapshot.Width + col, 0);
        var field = new TerrainField(snapshot, 4);
        int checkedBanks = 0;
        foreach (int tile in Enumerable.Range(0, snapshot.Relief.Length).Where(t => snapshot.Rivers[t] != 0))
        {
            var (cx, cz) = snapshot.Grid.HexOf(new TileId(tile)).ToPixel(1);
            var bank = Assert.NotNull(field.Rivers.NearBank(cx, cz));
            Assert.Equal(1, Math.Sqrt(bank.AcrossX * bank.AcrossX + bank.AcrossZ * bank.AcrossZ), 1e-9);
            Assert.True(bank.AcrossX * (bank.X - cx) + bank.AcrossZ * (bank.Z - cz) > 0, "the river lies beyond the bank");
            var (distance, width) = CentreLine(field, bank.X, bank.Z);
            Assert.Equal(width, distance, 0.01);
            // Half a river width straight across reaches the centre line, and the waterline runs on along the course.
            Assert.Equal(0, CentreLine(field, bank.X + bank.AcrossX * width, bank.Z + bank.AcrossZ * width).Distance, 0.01);
            foreach (double along in new[] { -0.05, 0.05 })
                Assert.Equal(width, CentreLine(field, bank.X - bank.AcrossZ * along, bank.Z + bank.AcrossX * along).Distance, 0.015);
            var copy = Assert.NotNull(field.Rivers.NearBank(cx + field.Period, cz));
            Assert.Equal(bank.X + field.Period, copy.X, 1e-9);
            Assert.Equal(bank.Z, copy.Z, 1e-9);
            Assert.Equal(bank.AcrossX, copy.AcrossX, 1e-9);
            Assert.Equal(bank.AcrossZ, copy.AcrossZ, 1e-9);
            checkedBanks++;
        }
        Assert.Equal(chain.Length + 1, checkedBanks);
    }

    [Fact]
    public void River_surfaces_lie_on_the_rendered_terrain_triangles()
    {
        var field = new TerrainField(TerrainTestMaps.Mixed(), 8);
        var chunks = new TerrainChunks(field.Snapshot.Grid.Spec);
        int checkedVertices = 0;
        foreach (var chunk in chunks.Chunks.Take(6))
        {
            var river = RiverMesher.Build(chunk, field);
            if (river == null) continue;
            var ground = TerrainMesher.Build(chunk, field);
            for (int v = 0; v < river.VertexCount; v += 5)
            {
                double x = river.Positions[v * 3], z = river.Positions[v * 3 + 2];
                // Odd lattice rows start half a step in, so keep clear of the chunk's east and west edges.
                if (!chunk.Contains(x - 0.2, z) || !chunk.Contains(x + 0.2, z)) continue;
                Assert.Equal(TerrainTestMaps.HeightAt(ground, x, z), river.Positions[v * 3 + 1], 5e-4);
                checkedVertices++;
            }
        }
        Assert.True(checkedVertices > 100);
    }

    [Theory]
    [InlineData(10, 0, 0)] // north boundary: the ordinary terrain ring misses a changed chunk
    [InlineData(2, 3, 2)] // a moved long connector changes terrain beyond the ordinary terrain ring
    [InlineData(0, 3, 2)] // west edge: the wrap image reshapes east chunks
    public void Changing_a_river_edge_dirties_every_chunk_whose_terrain_or_river_surface_changes(int col, int row, int direction)
    {
        var rendered = baseline.Value;
        var before = rendered.Snapshot;
        var after = TerrainTestMaps.Mixed(20, 20, seed: 3, cliffs: false);
        int tile = row * before.Width + col;
        after.Relief[tile] = ReliefClass.Flat;
        after.Sight[tile] = Sight.Visible;
        if ((after.Rivers[tile] & (1 << direction)) != 0) after.Rivers[tile] &= (byte)~(1 << direction);
        else TerrainTestMaps.SetRiver(after, tile, direction);
        var chunks = rendered.Chunks;
        var dirty = TerrainChunks.DirtyChunks(chunks.Signatures(before), chunks.Signatures(after)).ToHashSet();
        var newField = rendered.Field.With(after);
        var changed = new HashSet<int>();
        // A one-tile edit can only reshape a chunk that reads the tile: the chunk's terrain ring, its river ring and its
        // cliff ring are TerrainChunks' documented bounds on every tile that can change its ground, river surface or
        // cliffs, so a chunk outside all three is provably unchanged. Every chunk in the widest ring (plus every chunk
        // the signatures already call dirty) is still meshed and compared, so "every changed chunk is dirty" keeps full
        // coverage instead of assuming the signature it checks.
        double reaches = Math.Max(chunks.RingRadius, Math.Max(chunks.RiverRingRadius, chunks.CliffRingRadius));
        var (editX, editZ) = before.Grid.HexOf(new TileId(tile)).ToPixel(1);
        foreach (var chunk in chunks.Chunks.Where(c => dirty.Contains(c.Id)
            || InInfluenceRing(c, editX, editZ, before.Period, reaches)))
        {
            bool sameGround = Same(rendered.Ground[chunk.Id], TerrainMesher.Build(chunk, newField));
            bool sameRiver = Same(rendered.Rivers[chunk.Id], RiverMesher.Build(chunk, newField));
            if (sameGround && sameRiver) continue;
            changed.Add(chunk.Id);
            Assert.Contains(chunk.Id, dirty);
        }
        Assert.NotEmpty(changed);
        Assert.True(dirty.Count < chunks.Chunks.Count);
        if (col == 0)
            Assert.Contains(changed, id => chunks.Chunks[id].Column == chunks.Columns - 1);
        else
        {
            // Both the north-boundary edit and the connector edit need more than the ordinary terrain ring.
            Assert.Contains(changed, id => !InInfluenceRing(chunks.Chunks[id], editX, editZ, before.Period, chunks.RingRadius));
            if (row != 0)
            {
                // Keep this a long-connector regression, not just another isolated river-edge edit.
                var previousBridges = RiverGraph.Build(before).Links.Where(l => l.Kind == RiverLinkKind.Bridge)
                    .Select(l => Segment(l.AX, l.AZ, l.BX, l.BZ)).ToHashSet();
                Assert.Contains(RiverGraph.Build(after).Links, l => l.Kind == RiverLinkKind.Bridge && l.Length > 1.5
                    && !previousBridges.Contains(Segment(l.AX, l.AZ, l.BX, l.BZ)));
            }
        }
    }

    /// <summary>Whether a tile centre lies in a chunk's influence ring: its rectangle grown by <paramref name="radius"/>,
    /// across the map wrap. Mirrors how <see cref="TerrainChunks"/> collects the tiles that can change a chunk.</summary>
    private static bool InInfluenceRing(TerrainChunk chunk, double x, double z, double period, double radius)
    {
        double dx = new[] { x - period, x, x + period }.Min(px => Math.Max(Math.Max(chunk.X0 - px, px - chunk.X1), 0));
        double dz = Math.Max(Math.Max(chunk.Z0 - z, z - chunk.Z1), 0);
        return Math.Sqrt(dx * dx + dz * dz) <= radius;
    }

    private static bool Same(TerrainMeshData a, TerrainMeshData b) =>
        a.Positions.SequenceEqual(b.Positions) && a.Normals.SequenceEqual(b.Normals);

    private static bool Same(RiverMeshData? a, RiverMeshData? b)
    {
        if (a == null || b == null) return a == b;
        return a.Positions.SequenceEqual(b.Positions) && a.Normals.SequenceEqual(b.Normals)
            && a.Info.SequenceEqual(b.Info) && a.Flow.SequenceEqual(b.Flow)
            && a.Lanes.SequenceEqual(b.Lanes) && a.Indices.SequenceEqual(b.Indices);
    }

    /// <summary>The shared edge of two neighbouring tiles: the segment through its midpoint, perpendicular to the centres.</summary>
    private static (double, double, double, double) RulesEdge(TerrainSnapshot s, int a, int b)
    {
        var (ax, az) = s.Grid.HexOf(new TileId(a)).ToPixel(1);
        var (bx, bz) = s.Grid.HexOf(new TileId(b)).ToPixel(1);
        double mx = (ax + bx) / 2, mz = (az + bz) / 2, ux = -(bz - az) / Sqrt3 / 2, uz = (bx - ax) / Sqrt3 / 2;
        return Segment(mx - ux, mz - uz, mx + ux, mz + uz);
    }

    private static (double, double, double, double) Segment(double ax, double az, double bx, double bz)
    {
        static double R(double v) => Math.Round(v, 6);
        var (p, q) = ((R(ax), R(az)), (R(bx), R(bz)));
        return p.CompareTo(q) <= 0 ? (p.Item1, p.Item2, q.Item1, q.Item2) : (q.Item1, q.Item2, p.Item1, p.Item2);
    }

    /// <summary>Brute force over every curve and wrap copy: the distance from (x, z) to the nearest river centre line and
    /// the water half width there.</summary>
    private static (double Distance, double Width) CentreLine(TerrainField field, double x, double z)
    {
        var best = (Distance: double.MaxValue, Width: 0.0);
        foreach (var c in field.Rivers.Curves)
            foreach (double shift in new[] { -field.Period, 0, field.Period })
                for (int i = 0; i + 1 < c.Count; i++)
                {
                    double ax = c.X[i] + shift, bx = c.X[i + 1] + shift, vx = bx - ax, vz = c.Z[i + 1] - c.Z[i];
                    double t = Math.Clamp(((x - ax) * vx + (z - c.Z[i]) * vz) / (vx * vx + vz * vz), 0, 1);
                    double d = Math.Sqrt(Math.Pow(x - ax - t * vx, 2) + Math.Pow(z - c.Z[i] - t * vz, 2));
                    if (d < best.Distance) best = (d, c.Width[i] + t * (c.Width[i + 1] - c.Width[i]));
                }
        return best;
    }

    private static int Reachable(RiverGraph graph, int start)
    {
        var seen = new HashSet<int> { start };
        var pending = new Stack<int>([start]);
        while (pending.TryPop(out int node))
            foreach (int l in graph.LinksAt(node))
                foreach (int next in new[] { graph.Links[l].A, graph.Links[l].B })
                    if (seen.Add(next)) pending.Push(next);
        return seen.Count;
    }
}
