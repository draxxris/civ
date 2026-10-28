using Ariel.Client;
using Ariel.Client.Terrain;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

public class HazeVolumeTests
{
    private static readonly FeatureStyle Storm = new(Rgb.Hex(0), 0, 0,
        new FeatureLook(Haze: FeatureHaze.Storm, HazeRgb: 0x4c5462, HazePercent: 75));

    // Index 0 is "no feature"; index 1 is the storm.
    private static readonly IReadOnlyList<FeatureStyle?> Grounds = [null, Storm];

    private static (TerrainSnapshot Snapshot, int Tile) StormAtSea(Sight sight = Sight.Visible)
    {
        var snapshot = TerrainTestMaps.Uniform(ReliefClass.DeepWater);
        int tile = snapshot.Grid.TileAt(HexGrid.FromOffset(8, 6))!.Value;
        snapshot.FeatureGround[tile] = 1;
        snapshot.Sight[tile] = sight;
        return (snapshot, tile);
    }

    private static int TexelAt(HazeVolume volume, double x, double z) =>
        ((int)Math.Floor((z - volume.ZMin) / volume.TexelZ) * volume.Width + (int)Math.Floor(x / volume.TexelX)) * 4;

    [Fact]
    public void Storm_cloud_stands_clear_of_the_sea_over_its_tile_and_is_gone_two_tiles_away()
    {
        var (snapshot, tile) = StormAtSea();
        var volume = HazeVolume.Bake(new TerrainField(snapshot, 5), Grounds);
        var (cx, cz) = TerrainTestMaps.Centre(snapshot.Grid.HexOf(new TileId(tile)));

        int over = TexelAt(volume, cx, cz);
        Assert.True(volume.Any);
        Assert.InRange(volume.Shape[over], 150, 255);
        // The base sits a layer above the sea surface, not on the sea floor.
        Assert.InRange(HazeVolume.DecodeHeight(volume.Shape[over + 2]), 0.3, 0.4);
        Assert.Equal(0, volume.Shape[TexelAt(volume, cx + 2 * TerrainTestMaps.Sqrt3, cz)]);
    }

    [Fact]
    public void Unexplored_storm_bakes_no_haze()
    {
        var (snapshot, tile) = StormAtSea(Sight.Hidden);
        snapshot.Relief[tile] = ReliefClass.Hidden;

        Assert.False(HazeVolume.Bake(new TerrainField(snapshot, 5), Grounds).Any);
    }

    [Fact]
    public void Storm_out_of_sight_is_toned_down()
    {
        var (seen, tile) = StormAtSea();
        var (fogged, _) = StormAtSea(Sight.Fogged);
        var (cx, cz) = TerrainTestMaps.Centre(seen.Grid.HexOf(new TileId(tile)));
        var visible = HazeVolume.Bake(new TerrainField(seen, 5), Grounds);
        var dim = HazeVolume.Bake(new TerrainField(fogged, 5), Grounds);

        int at = TexelAt(visible, cx, cz);
        // Fog desaturates toward the colour's own luminance and then scales it by 0.72, so the tone must get darker.
        int seenTone = Luminance(visible.Look, at), foggedTone = Luminance(dim.Look, at);
        Assert.True(foggedTone < seenTone * 95 / 100, $"fogged haze {foggedTone} is not darker than visible haze {seenTone}");
    }

    private static int Luminance(byte[] look, int at) =>
        (2126 * look[at] + 7152 * look[at + 1] + 722 * look[at + 2]) / 10000;
}
