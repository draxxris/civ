using Civ.Client;
using Civ.Client.Terrain;
using Civ.Content.Schema;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class TerrainPaletteTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private readonly TerrainPalette _palette = new(mods.Catalog);

    [Fact]
    public void Layers_keep_ordinal_terrain_order_then_surface_order()
    {
        var terrains = mods.Content.Get<TerrainDef>().All.Select(terrain => terrain.Id).ToArray();
        var terrainLayers = _palette.Layers.Where(layer => layer.Surface is null).Select(layer => layer.Id).ToArray();
        var surfaceLayers = _palette.Layers.Where(layer => layer.Surface is not null).Select(layer => layer.Surface!.Value).ToArray();

        Assert.Equal(terrains, terrainLayers);
        Assert.Equal(Enum.GetValues<TerrainSurface>(), surfaceLayers);
        Assert.Equal(_palette.Layers.Select(layer => (layer.Id, layer.Surface)),
            new TerrainPalette(mods.Catalog).Layers.Select(layer => (layer.Id, layer.Surface)));
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
    public void Grassland_shows_patches_of_plains_and_water_shows_none()
    {
        var grass = _palette.Layers[_palette.LayerOf("world:grass")];
        var ocean = _palette.Layers[_palette.LayerOf("world:ocean")];

        Assert.Equal(_palette.LayerOf("world:plains"), grass.PatchLayer);
        Assert.InRange(grass.PatchShare, 0.05f, 0.5f);
        Assert.Equal(TerrainPalette.ColorOnlyLayer, ocean.PatchLayer);
    }

    [Fact]
    public void Sand_and_grass_keep_their_direction_while_other_ground_turns_freely()
    {
        foreach (var terrain in new[] { "world:desert", "world:grass", "world:plains" })
            Assert.InRange(_palette.Layers[_palette.LayerOf(terrain)].Turn, 0.01f, 0.2f);
        Assert.Equal(1f, _palette.Layers[_palette.LayerOf("world:tundra")].Turn);
    }

    [Fact]
    public void Lush_ground_spreads_into_plains_and_plains_into_desert()
    {
        float Spread(string id) => Assert.Single(_palette.Layers, layer => layer.Id == id).Spread;

        Assert.True(Spread("world:grass") > Spread("world:plains"));
        Assert.True(Spread("world:plains") > Spread("world:desert"));
        Assert.All(_palette.Layers, layer => Assert.InRange(layer.Spread, 0f, 1f));
    }

    [Fact]
    public void Water_layers_fade_into_each_other_without_ragged_edges()
    {
        Assert.True(Assert.Single(_palette.Layers, layer => layer.Id == "world:ocean").Water);
        Assert.True(Assert.Single(_palette.Layers, layer => layer.Id == "world:coast").Water);
        Assert.False(Assert.Single(_palette.Layers, layer => layer.Id == "world:grass").Water);
    }

    [Fact]
    public void Featureless_revealed_tiles_use_zero_ground_and_featured_tiles_use_the_palette_index()
    {
        var session = TerrainTestMaps.RevealedSession(mods.Content);
        session.Sim.State.Plots[1].Feature = "world:forest";

        Assert.Equal(0, _palette.FeatureGroundOf(null, "world:grass"));

        var snapshot = TerrainSnapshot.From(session, _palette.LayerOf, _palette.FeatureGroundOf);
        int forestGround = _palette.FeatureGroundOf("world:forest", "world:grass");

        Assert.InRange(forestGround, 1, 63);
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
        Assert.Equal(95, snowyForest?.ScatterDensity);
        Assert.Equal("forest", ordinaryForest?.Scatter);
        Assert.Equal(95, ordinaryForest?.ScatterDensity);
    }
}
