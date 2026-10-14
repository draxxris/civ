using Civ.Client;
using Civ.Client.Terrain;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

/// <summary>The visual river network: generator-style edge chains render as one connected river that keeps every rules
/// edge and opens into the water; river surfaces lie on the rendered terrain; and river changes dirty every chunk whose
/// terrain or river surface they reshape.</summary>
public sealed class RiverNetworkTests
{
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
                Assert.Equal(GroundHeight(ground, x, z), river.Positions[v * 3 + 1], 3);
                checkedVertices++;
            }
        }
        Assert.True(checkedVertices > 100);
    }

    [Theory]
    [InlineData(14, 0, 0)] // north boundary: the ordinary terrain ring misses a changed chunk
    [InlineData(2, 15, 2)] // longer connector influence also reaches beyond the ordinary terrain ring
    [InlineData(0, 3, 2)] // west edge: the wrap image reshapes east chunks
    public void Changing_a_river_edge_dirties_every_chunk_whose_terrain_or_river_surface_changes(int col, int row, int direction)
    {
        var before = TerrainTestMaps.Mixed(40, 24, seed: 3);
        var after = TerrainTestMaps.Mixed(40, 24, seed: 3);
        int tile = row * before.Width + col;
        after.Relief[tile] = ReliefClass.Flat;
        after.Sight[tile] = Sight.Visible;
        if ((after.Rivers[tile] & (1 << direction)) != 0) after.Rivers[tile] &= (byte)~(1 << direction);
        else TerrainTestMaps.SetRiver(after, tile, direction);
        var chunks = new TerrainChunks(before.Grid.Spec);
        var dirty = TerrainChunks.DirtyChunks(chunks.Signatures(before), chunks.Signatures(after)).ToHashSet();
        var oldField = new TerrainField(before, 2);
        var newField = oldField.With(after);
        int changed = 0;
        foreach (var chunk in chunks.Chunks)
        {
            bool same = Same(TerrainMesher.Build(chunk, oldField), TerrainMesher.Build(chunk, newField))
                && Same(RiverMesher.Build(chunk, oldField), RiverMesher.Build(chunk, newField));
            if (same) continue;
            changed++;
            Assert.Contains(chunk.Id, dirty);
        }
        Assert.True(changed > 0);
        Assert.True(dirty.Count < chunks.Chunks.Count);
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

    /// <summary>Height of the chunk mesh triangle under (x, z), found by brute force over the mesh.</summary>
    private static double GroundHeight(TerrainMeshData mesh, double x, double z)
    {
        var p = mesh.Positions;
        for (int t = 0; t < mesh.Indices.Length; t += 3)
        {
            int a = mesh.Indices[t] * 3, b = mesh.Indices[t + 1] * 3, c = mesh.Indices[t + 2] * 3;
            double d = (p[b + 2] - p[c + 2]) * (p[a] - p[c]) + (p[c] - p[b]) * (p[a + 2] - p[c + 2]);
            double wa = ((p[b + 2] - p[c + 2]) * (x - p[c]) + (p[c] - p[b]) * (z - p[c + 2])) / d;
            double wb = ((p[c + 2] - p[a + 2]) * (x - p[c]) + (p[a] - p[c]) * (z - p[c + 2])) / d;
            double wc = 1 - wa - wb;
            if (wa >= -1e-6 && wb >= -1e-6 && wc >= -1e-6) return wa * p[a + 1] + wb * p[b + 1] + wc * p[c + 1];
        }
        throw new InvalidOperationException($"({x}, {z}) is outside the chunk mesh");
    }
}
