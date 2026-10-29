using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A river edge that also carries a cliff pours over the face: the strip's low side stretches to the foot of the
/// ramp and its flow turns downhill. These tests pin that geometry.</summary>
public sealed class RiverFallTests
{
    private static (TerrainSnapshot Snapshot, TerrainField Field, HexGrid Grid, int High, int Low) Fall(bool cliff = true, bool river = true)
    {
        var s = new TerrainSnapshot(new MapSpec(12, 10, false));
        for (int i = 0; i < s.Relief.Length; i++) { s.Relief[i] = ReliefClass.Flat; s.Sight[i] = Sight.Visible; }
        var grid = s.Grid;
        int high = grid.TileAt(5, 4).Value;
        int low = grid.Neighbor(new TileId(high), 0)!.Value.Value;
        if (cliff)
        {
            s.Cliffs[high] |= 1; s.Cliffs[low] |= 1 << 3;
            s.CliffLifts[high] |= 1;
        }
        if (river) { s.Rivers[high] |= 1; s.Rivers[low] |= 1 << 3; }
        var field = new TerrainField(s, 3, new TerrainTuning { CliffRise = 1.2 });
        return (s, field, grid, high, low);
    }

    private static RiverMeshData AllRivers(TerrainField field)
    {
        var chunks = new TerrainChunks(field.Snapshot.Grid.Spec);
        var parts = chunks.Chunks.Select(c => RiverMesher.Build(c, field)).Where(d => d != null).ToArray();
        return new RiverMeshData(
            [.. parts.SelectMany(p => p!.Positions)], [.. parts.SelectMany(p => p!.Normals)],
            [.. parts.SelectMany(p => p!.Lanes)], [.. parts.SelectMany(p => p!.Flow)],
            [.. parts.SelectMany(p => p!.Info)], [.. parts.SelectMany(p => p!.Indices)]);
    }

    [Fact]
    public void A_river_on_a_cliff_edge_falls_to_the_foot_of_the_ramp()
    {
        var (_, field, grid, high, low) = Fall();
        var data = AllRivers(field);
        Assert.True(data.VertexCount > 0);
        int falls = 0;
        double lowest = double.MaxValue, highest = double.MinValue;
        for (int v = 0; v < data.VertexCount; v++)
        {
            if (data.Info[v * 3 + 2] < 0.5f) continue;
            falls++;
            double y = data.Positions[v * 3 + 1];
            lowest = Math.Min(lowest, y);
            highest = Math.Max(highest, y);
        }
        Assert.True(falls > 0, "the fall has no stretched lanes");
        var (hx, hz) = grid.HexOf(new TileId(high)).ToPixel(1);
        var (lx, lz) = grid.HexOf(new TileId(low)).ToPixel(1);
        Assert.True(highest > lowest + 0.9, $"the fall only spans {highest - lowest:F2}");
        // The stretched lanes reach most of the way down the low tile.
        double reached = 0;
        for (int v = 0; v < data.VertexCount; v++)
        {
            if (data.Info[v * 3 + 2] < 0.9f) continue;
            double px = data.Positions[v * 3] - hx, pz = data.Positions[v * 3 + 2] - hz;
            double along = (px * (lx - hx) + pz * (lz - hz)) / Math.Sqrt((lx - hx) * (lx - hx) + (lz - hz) * (lz - hz));
            reached = Math.Max(reached, along);
        }
        Assert.True(reached > 0.8, $"the fall reaches only {reached:F2} into the low tile");
        // Flow on the fall runs downhill, across the edge, not along it.
        var (dx, dz) = (lx - hx, lz - hz);
        double len = Math.Sqrt(dx * dx + dz * dz);
        for (int v = 0; v < data.VertexCount; v++)
        {
            if (data.Info[v * 3 + 2] < 0.9f) continue;
            double fx = data.Flow[v * 2], fz = data.Flow[v * 2 + 1];
            double flen = Math.Sqrt(fx * fx + fz * fz);
            Assert.True(flen > 0.1, "a falling lane has no flow");
            Assert.True((fx * dx + fz * dz) / (flen * len) > 0.9, "the fall flows along the edge instead of down it");
        }
    }

    [Fact]
    public void A_river_without_a_cliff_has_no_fall()
    {
        var (_, field, _, _, _) = Fall(cliff: false);
        var data = AllRivers(field);
        Assert.True(data.VertexCount > 0);
        for (int v = 0; v < data.VertexCount; v++) Assert.Equal(0, data.Info[v * 3 + 2]);
    }
}
