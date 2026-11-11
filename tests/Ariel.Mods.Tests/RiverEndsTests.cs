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
        var ends = rivers.Curves.SelectMany(c => new[] { (c.StartPond, c.X[0], c.Z[0], c.Width[0]), (c.EndPond, c.X[^1], c.Z[^1], c.Width[^1]) })
            .Where(t => t.Item1 != null).ToArray();
        Assert.Equal(2, ends.Length);
        foreach (var (pond, x, z, width) in ends)
        {
            // The pond sits on the river's own tip, wider than the river, which widens into it; its bowl is carved to the
            // full depth.
            Assert.Contains(pond!, rivers.Ponds);
            Assert.Equal(x, pond!.X, 9);
            Assert.Equal(z, pond.Z, 9);
            Assert.True(pond.Radius > field.Tuning.RiverWidth, $"pond radius {pond.Radius:F3} is no wider than the river");
            Assert.Equal(pond.Radius, width, 9);
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
        Assert.True((curve.StartPond != null) != (curve.EndPond != null), "the river should end in a pond at exactly one tip");
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
    public void A_river_widens_into_its_pond_as_one_body_of_level_still_water()
    {
        var field = new TerrainField(Chain(), 3);
        var chunks = new TerrainChunks(field.Snapshot.Grid.Spec);
        var curve = field.Rivers.Curves.First(c => c.EndPond != null || c.StartPond != null);
        var pond = (curve.EndPond ?? curve.StartPond)!;
        // The river widens steadily into the pond: no narrowing neck before it.
        var widths = (curve.EndPond != null ? curve.Width : curve.Width.Reverse()).ToArray();
        var reach = Enumerable.Range(0, curve.Count).Where(i => Math.Abs(curve.Arc[i] - (curve.EndPond != null ? curve.Arc[^1] : 0))
            < RiverNetwork.PondFlare * pond.Radius).Select(i => curve.EndPond != null ? i : curve.Count - 1 - i).Order().ToArray();
        for (int k = 1; k < reach.Length; k++)
            Assert.True(widths[reach[k]] >= widths[reach[k - 1]] - 1e-3, "the river narrows on its way into the pond");

        var mesh = RiverMesher.Build(chunks.Chunks.Single(c => c.Contains(pond.X, pond.Z)), field)!;
        double? level = null;
        int water = 0, far = 0;
        for (int v = 0; v < mesh.VertexCount; v++)
        {
            double x = mesh.Positions[v * 3], y = mesh.Positions[v * 3 + 1], z = mesh.Positions[v * 3 + 2];
            double r = Math.Sqrt((x - pond.X) * (x - pond.X) + (z - pond.Z) * (z - pond.Z));
            if (r > 0.5 * pond.Radius || Math.Abs(mesh.Lanes[v * 2]) > 1) continue;
            // In the middle of the pond the water lies level and still, fully shown: the river does not fade out.
            level ??= y;
            Assert.Equal(level.Value, y, 4);
            Assert.Equal(0f, mesh.Flow[v * 2], 6);
            Assert.Equal(0f, mesh.Flow[v * 2 + 1], 6);
            Assert.Equal(1f, mesh.Info[v * 2], 6);
            water++;
        }
        // The cap closes the strip round the far shore: its outer lanes lie beyond the end of the river.
        double ox = curve.EndPond != null ? curve.TX[^1] : -curve.TX[0], oz = curve.EndPond != null ? curve.TZ[^1] : -curve.TZ[0];
        for (int v = 0; v < mesh.VertexCount; v++)
            if (Math.Abs(mesh.Lanes[v * 2] - RiverMesher.BankExtent) < 1e-6
                && (mesh.Positions[v * 3] - pond.X) * ox + (mesh.Positions[v * 3 + 2] - pond.Z) * oz > pond.Radius)
                far++;
        Assert.True(water > 0, "no water in the middle of the pond");
        Assert.True(far > 0, "the strip does not close round the pond's far shore");
    }

    [Fact]
    public void The_water_carries_its_depth_over_the_bed_and_the_banks_are_dry()
    {
        var field = new TerrainField(Chain(), 3);
        var chunks = new TerrainChunks(field.Snapshot.Grid.Spec);
        int deep = 0;
        foreach (var chunk in chunks.Chunks)
        {
            var mesh = RiverMesher.Build(chunk, field);
            if (mesh == null) continue;
            for (int v = 0; v < mesh.VertexCount; v++)
            {
                float across = Math.Abs(mesh.Lanes[v * 2]), depth = mesh.Depth[v];
                // The shader reads depth through an 8-bit channel scaled by DepthRange, so it stays inside it.
                Assert.InRange(depth, 0f, RiverMeshData.DepthRange);
                if (across > 1.0001f) Assert.Equal(0f, depth);
                if (across < 0.01f && depth > 0.01f) deep++;
            }
        }
        Assert.True(deep > 0, "no water stands over its bed in mid-channel");
    }
}
