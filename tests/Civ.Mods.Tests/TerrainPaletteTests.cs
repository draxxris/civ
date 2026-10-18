using Civ.Client;
using Civ.Client.Terrain;
using Civ.Content.Schema;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class TerrainPaletteTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private readonly TerrainPalette _palette = new(new GameCatalog(mods.Sources));

    [Fact]
    public void Layers_keep_ordinal_terrain_order_then_surface_order()
    {
        var terrains = mods.Content.Get<TerrainDef>().All.Select(terrain => terrain.Id).ToArray();
        var terrainLayers = _palette.Layers.Where(layer => layer.Surface is null).Select(layer => layer.Id).ToArray();
        var surfaceLayers = _palette.Layers.Where(layer => layer.Surface is not null).Select(layer => layer.Surface!.Value).ToArray();

        Assert.Equal(terrains, terrainLayers);
        Assert.Equal(Enum.GetValues<TerrainSurface>(), surfaceLayers);
        Assert.Equal(_palette.Layers.Select(layer => (layer.Id, layer.Surface)),
            new TerrainPalette(new GameCatalog(mods.Sources)).Layers.Select(layer => (layer.Id, layer.Surface)));
    }

    [Fact]
    public void Terrain_keeps_its_colour_when_no_texture_is_available()
    {
        // The graphical placeholder deliberately ships no texture; playable grass now supplies art.
        var placeholder = Assert.Single(_palette.Layers, layer => layer.Id == "world:none");
        Assert.Null(placeholder.TexturePath);
        Assert.Equal(Rgb.Hex(0x777777), placeholder.Fallback);
        var grass = Assert.Single(_palette.Layers, layer => layer.Id == "world:grass");
        Assert.NotNull(grass.TexturePath);
        Assert.Equal(Rgb.Hex(0x66ad4b), grass.Fallback);
    }

    [Fact]
    public void Featureless_revealed_tiles_use_zero_ground_and_featured_tiles_use_the_palette_index()
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content);
        session.Sim.State.Plots[1].Feature = "world:forest";

        Assert.Equal(0, _palette.FeatureGroundOf(null));

        var snapshot = TerrainSnapshot.From(session, _palette.LayerOf, _palette.FeatureGroundOf);
        int forestGround = _palette.FeatureGroundOf("world:forest");

        Assert.InRange(forestGround, 1, 15);
        Assert.Equal(Sight.Visible, snapshot.Sight[0]);
        Assert.Equal(Sight.Visible, snapshot.Sight[1]);
        Assert.Equal((byte)_palette.LayerOf("world:grass"), snapshot.Layer[0]);
        Assert.Equal((byte)0, snapshot.FeatureGround[0]);
        Assert.Equal((byte)_palette.LayerOf("world:grass"), snapshot.Layer[1]);
        Assert.Equal((byte)forestGround, snapshot.FeatureGround[1]);
    }

    [Fact]
    public void Terrain_specific_scatter_overrides_the_feature_default()
    {
        var snowyForest = _palette.ScatterFor("world:forest", "world:tundra");
        var ordinaryForest = _palette.ScatterFor("world:forest", "world:grass");

        Assert.Equal("forest_snow", snowyForest?.Scatter);
        Assert.Equal(70, snowyForest?.ScatterDensity);
        Assert.Equal("forest", ordinaryForest?.Scatter);
        Assert.Equal(70, ordinaryForest?.ScatterDensity);
    }
}
