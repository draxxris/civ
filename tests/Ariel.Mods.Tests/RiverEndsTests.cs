using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>How a river is drawn away from its rules edges: a river that ends on land in plain sight ends in a still pond
/// sunk into the ground, one that runs on into unexplored ground does not; corner nodes move off their hex corners by a
/// bounded amount so the course sweeps across the grid, except on cliffs and shores, where it must stay exact.</summary>
public sealed class RiverEndsTests
{
    private const int Row = 4, First = 3, Length = 6;

    /// <summary>A river along the zigzag of hex edges below one row of tiles: every south-west and south-east edge of
    /// tiles First..First + Length - 1, so the river is one chain of edges with a dead end at each tip.</summary>
    private static TerrainSnapshot Chain()
    {
        var s = TerrainTestMaps.Uniform(ReliefClass.Flat);
        for (int col = First; col < First + Length; col++)
        {
            TerrainTestMaps.SetRiver(s, Row * s.Width + col, 4);
            TerrainTestMaps.SetRiver(s, Row * s.Width + col, 5);
        }
        return s;
    }

    [Fact]
    public void A_river_that_ends_in_plain_sight_ends_in_a_sunken_pond_at_each_tip()
    {
        var field = new TerrainField(Chain(), 3);
        var rivers = field.Rivers;

        Assert.Equal(2, rivers.Ponds.Count);
        var tips = rivers.Curves.SelectMany(c => new[] { (c.StartPond, c.X[0], c.Z[0]), (c.EndPond, c.X[^1], c.Z[^1]) })
            .Where(t => t.Item1 > 0).ToArray();
        Assert.Equal(2, tips.Length);
        foreach (var pond in rivers.Ponds)
        {
            // The pond sits on the river's own tip, wider than the river, and its bowl is carved to the full depth.
            Assert.Contains(tips, t => Math.Abs(t.Item2 - pond.X) < 1e-9 && Math.Abs(t.Item3 - pond.Z) < 1e-9);
            Assert.True(pond.Radius > field.Tuning.RiverWidth, $"pond radius {pond.Radius:F3} is no wider than the river");
            Assert.Equal(field.Tuning.RiverDepth, rivers.Carve(pond.X, pond.Z), 6);
            Assert.True(rivers.Near(pond.X + pond.Radius, pond.Z));
        }
    }

    [Fact]
    public void A_river_that_runs_on_into_unexplored_ground_does_not_end_in_a_pond_there()
    {
        var s = Chain();
        // The west tip's corner touches the tile west of the first river tile.
        s.Relief[Row * s.Width + First - 1] = ReliefClass.Hidden;
        s.Sight[Row * s.Width + First - 1] = Sight.Hidden;

        var rivers = new TerrainField(s, 3).Rivers;

        var pond = Assert.Single(rivers.Ponds);
        var (eastX, _) = s.Grid.HexOf(new TileId(Row * s.Width + First + Length - 1)).ToPixel(1);
        Assert.True(pond.X > eastX - 1, "the remaining pond is not at the east tip");
    }

    [Fact]
    public void A_river_tip_on_the_shore_of_a_lake_ends_in_the_lake_not_in_a_pond()
    {
        var s = Chain();
        // The east tip's corner touches the tile east of the last river tile; make it lake water, with no river bit on it.
        s.Relief[Row * s.Width + First + Length] = ReliefClass.ShallowWater;

        var rivers = new TerrainField(s, 3).Rivers;

        var pond = Assert.Single(rivers.Ponds);
        var (westX, _) = s.Grid.HexOf(new TileId(Row * s.Width + First)).ToPixel(1);
        Assert.True(pond.X < westX + 1, "the remaining pond is not at the west tip");
    }

    [Fact]
    public void A_short_isolated_river_ends_in_one_pond_rather_than_two()
    {
        var s = TerrainTestMaps.Uniform(ReliefClass.Flat);
        TerrainTestMaps.SetRiver(s, Row * s.Width + First, 0);

        var rivers = new TerrainField(s, 3).Rivers;

        Assert.Single(rivers.Ponds);
        var curve = Assert.Single(rivers.Curves);
        Assert.True(curve.StartsDry && curve.EndsDry);
        Assert.True((curve.StartPond > 0) != (curve.EndPond > 0), "the river should end in a pond at exactly one tip");
    }

    [Fact]
    public void Corner_nodes_move_a_bounded_distance_except_on_cliffs_and_shores()
    {
        var field = new TerrainField(TerrainTestMaps.Mixed(), 8);
        var rivers = field.Rivers;
        var graph = rivers.Graph;
        int moved = 0, pinned = 0;
        for (int i = 0; i < graph.Links.Count; i++)
        {
            var link = graph.Links[i];
            var curve = rivers.Curves[i];
            foreach (var (node, x, z, cx, cz) in new[] { (link.A, link.AX, link.AZ, curve.X[0], curve.Z[0]),
                (link.B, link.BX, link.BZ, curve.X[^1], curve.Z[^1]) })
            {
                double shift = Math.Sqrt((cx - x) * (cx - x) + (cz - z) * (cz - z));
                Assert.True(shift <= RiverNetwork.MaxNodeShift + 1e-9, $"node {node} moved {shift:F3}");
                if (graph.IsPinned(node)) { Assert.Equal(0, shift, 9); pinned++; }
                else if (shift > 0.02) moved++;
            }
        }
        Assert.True(pinned > 0, "the mixed map has no cliff or shore nodes");
        Assert.True(moved > 0, "no free node moved off its hex corner");
    }

    [Fact]
    public void A_pond_is_still_water_ringed_by_its_bed()
    {
        var field = new TerrainField(Chain(), 3);
        var chunks = new TerrainChunks(field.Snapshot.Grid.Spec);
        var pond = field.Rivers.Ponds[0];
        var chunk = chunks.Chunks.Single(c => c.Contains(pond.X, pond.Z));
        var mesh = RiverMesher.Build(chunk, field)!;
        int inside = 0;
        for (int v = 0; v < mesh.VertexCount; v++)
        {
            double dx = mesh.Positions[v * 3] - pond.X, dz = mesh.Positions[v * 3 + 2] - pond.Z;
            if (dx * dx + dz * dz > 1e-8) continue;
            // At the pond's centre (its ring's hub and the river's tip) the water lies still and level, and the river's
            // own strip has faded out: the pond draws the water there.
            Assert.Equal(0f, mesh.Flow[v * 2], 6);
            Assert.Equal(0f, mesh.Flow[v * 2 + 1], 6);
            Assert.True(mesh.Normals[v * 3 + 1] > 0.99f);
            if (mesh.Lanes[v * 2 + 1] > 0.01f || mesh.Info[v * 2] == 1f) inside++;
            else Assert.Equal(0f, mesh.Info[v * 2], 6);
        }
        Assert.True(inside > 0);
        Assert.Contains(Enumerable.Range(0, mesh.VertexCount), v => Math.Abs(mesh.Lanes[v * 2] - RiverMesher.BankExtent) < 1e-6f);
    }
}
