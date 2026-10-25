using Civ.Client;
using Civ.Client.Terrain;
using Civ.Content.Schema;
using Civ.Game;
using Civ.Modding;
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

    /// <summary>The colour table is the C# half of the shader-parity formula: layer colour, spread, turn and ragged-edge
    /// strength must land in the rows the shaders fetch, and a water tint must not leak onto land.</summary>
    [Fact]
    public void Colour_table_packs_layer_colour_spread_turn_and_ragged_edge_rows()
    {
        var bytes = PaletteTable.Bytes(_palette, id => id == "world:ocean" ? Rgb.Hex(0x123456) : null);

        Assert.Equal(PaletteTable.Width * PaletteTable.Rows * 4, bytes.Length);
        for (int i = 0; i < _palette.Layers.Count; i++)
        {
            var layer = _palette.Layers[i];
            Assert.Equal(new[] { B(layer.Fallback.R), B(layer.Fallback.G), B(layer.Fallback.B), B(layer.Spread) },
                Texel(bytes, PaletteTable.LayerRow, i));
            Assert.Equal(new byte[] { B(layer.Turn), 0, 0, 255 }, Texel(bytes, PaletteTable.TurnRow, i));
            Assert.Equal(new[] { layer.Ocean ? (byte)255 : (byte)0, B((layer.PatchLayer + 1) / 255f), B(layer.PatchShare), layer.Water ? (byte)0 : (byte)255 },
                Texel(bytes, PaletteTable.EdgeRow, i));
            Assert.Equal(layer.Id == "world:ocean" ? new byte[] { 0x12, 0x34, 0x56, B(PaletteTable.WaterTintStrength) } : new byte[4],
                Texel(bytes, PaletteTable.WaterTintRow, i));
        }
        // Index zero of the feature and owner rows means "no tint"; owner colours start at owner + 1.
        Assert.Equal(new byte[4], Texel(bytes, PaletteTable.FeatureRow, 0));
        Assert.Equal(new byte[4], Texel(bytes, PaletteTable.OwnerRow, 0));
        Assert.Equal(new[] { B(MapStyle.PlayerColor(0).R), B(MapStyle.PlayerColor(0).G), B(MapStyle.PlayerColor(0).B), (byte)255 },
            Texel(bytes, PaletteTable.OwnerRow, 1));
    }

    /// <summary>Feature grounds pack a tint, the look that raises craters or haze, and the haze colour — all three rows
    /// are read per texel by the shaders, so a shifted row would tint the wrong thing.</summary>
    [Fact]
    public void Colour_table_packs_feature_tint_look_and_haze_rows()
    {
        var bytes = PaletteTable.Bytes(_palette, _ => null);
        var grounds = _palette.FeatureGrounds;
        Assert.True(grounds.Count > 1, "shipped content should define at least one feature ground");
        for (int i = 1; i < grounds.Count; i++)
        {
            var style = grounds[i]!;
            var look = style.Look;
            Assert.Equal(new[] { B(style.Tint.R), B(style.Tint.G), B(style.Tint.B), B(style.TintStrength) },
                Texel(bytes, PaletteTable.FeatureRow, i));
            Assert.Equal(new byte[]
                {
                    (byte)look.Ground, B(Math.Clamp(look.GroundPercent, 0, 100) / 100f), (byte)look.Haze,
                    look.Haze == FeatureHaze.None ? (byte)0 : B(Math.Clamp(look.HazePercent, 0, 100) / 100f),
                }, Texel(bytes, PaletteTable.LookRow, i));
            Assert.Equal(new[] { B(Rgb.Hex(look.HazeRgb).R), B(Rgb.Hex(look.HazeRgb).G), B(Rgb.Hex(look.HazeRgb).B), (byte)255 },
                Texel(bytes, PaletteTable.HazeRow, i));
        }
    }

    /// <summary>One layer per surface is reserved, so a ruleset with more terrains than the rest of the budget keeps the
    /// first ones in ordinal order and reports <see cref="TerrainPalette.ColorOnlyLayer"/> for the rest.</summary>
    [Fact]
    public void Terrains_beyond_the_layer_budget_fall_back_to_colour_only_and_surfaces_keep_their_layers()
    {
        var sources = mods.Sources.Append(OverBudgetProbe()).ToList();
        var content = ModLoader.Build(sources);
        var palette = new TerrainPalette(new GameCatalog(sources));
        var terrains = content.Get<TerrainDef>().All.Select(t => t.Id).Order(StringComparer.Ordinal).ToArray();
        int limit = TerrainPalette.MaxLayers - Enum.GetValues<TerrainSurface>().Length;

        Assert.True(terrains.Length > limit, "the probe must exceed the layer budget");
        Assert.Equal(TerrainPalette.MaxLayers, palette.Layers.Count);
        Assert.All(terrains.Take(limit), id => Assert.NotEqual(TerrainPalette.ColorOnlyLayer, palette.LayerOf(id)));
        Assert.All(terrains.Skip(limit), id => Assert.Equal(TerrainPalette.ColorOnlyLayer, palette.LayerOf(id)));
        Assert.All(Enum.GetValues<TerrainSurface>(), surface => Assert.NotEqual(TerrainPalette.ColorOnlyLayer, palette.SurfaceLayerOf(surface)));
    }

    private sealed class OverBudgetMod : IMod
    {
        public void Register(IModContext ctx)
        {
            var terrains = ctx.Content<Civ.Content.Schema.TerrainDef>();
            for (int i = 0; i < 15; i++) terrains.Register(new Civ.Content.Schema.TerrainDef { Id = $"palette_probe:t{i:00}" });
        }
    }

    private ModSource OverBudgetProbe() => new()
    {
        Root = mods.Root,
        Manifest = new() { Id = "palette_probe", Version = "1", Dependencies = ["foundation"], References = ["foundation", "world"] },
        Instance = new OverBudgetMod(),
    };

    private static byte B(float v) => PaletteTable.ToByte(v);

    private static byte[] Texel(byte[] bytes, int row, int index)
    {
        int at = (row * PaletteTable.Width + index) * 4;
        return bytes[at..(at + 4)];
    }
}
