using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client.Terrain;

/// <summary>A terrain or surface material layer with its colour fallback and optional texture. <see cref="Spread"/> in
/// [0, 1] is how far its ground reaches into neighbouring tiles (see <see cref="TileAppearance.Spread"/>). Water layers
/// fade smoothly into each other instead of the ragged edges of land; <see cref="Ocean"/> marks deep water.
/// <see cref="PatchLayer"/> is the layer whose ground shows through in patches over <see cref="PatchShare"/> (0..1) of
/// this one (see <see cref="TileAppearance.Patch"/>), or <see cref="TerrainPalette.ColorOnlyLayer"/> for none.
/// <see cref="Turn"/> (0..1 of 180°) is the largest random turn of its texture (see <see cref="TileAppearance.TurnDegrees"/>).</summary>
public sealed record TerrainPaletteLayer(string Id, Rgb Fallback, string? TexturePath, TerrainSurface? Surface = null,
    float Spread = 0.5f, bool Water = false, bool Ocean = false, int PatchLayer = TerrainPalette.ColorOnlyLayer,
    float PatchShare = 0, float Turn = 1);

/// <summary>Deterministic, engine-free material and scatter lookups for the loaded ruleset.</summary>
public sealed class TerrainPalette
{
    public const int MaxLayers = 32;
    public const int ColorOnlyLayer = -1;
    private const int MaxFeatureGroundIndex = 63;

    private static readonly IReadOnlyDictionary<TerrainSurface, (string Id, int Rgb)> SurfaceDefaults =
        new Dictionary<TerrainSurface, (string, int)>
        {
            [TerrainSurface.Cliff] = ("surface:cliff", 0x8a8a90),
            [TerrainSurface.Seabed] = ("surface:seabed", 0x234f9c),
            [TerrainSurface.DeepSeabed] = ("surface:deep_seabed", 0x17366b),
            [TerrainSurface.Unexplored] = ("surface:unexplored", 0x777777),
            [TerrainSurface.Snow] = ("surface:snow", 0xf0f5fa),
        };

    private readonly Dictionary<string, int> _terrainLayers = new(StringComparer.Ordinal);
    private readonly Dictionary<TerrainSurface, int> _surfaceLayers = new();
    private readonly Dictionary<(string Feature, string Terrain), int> _featureGroundIndices = new();
    private readonly ContentDb _content;
    private readonly IReadOnlyList<TerrainPaletteLayer> _layers;
    private readonly IReadOnlyList<FeatureStyle?> _featureGrounds;

    public IReadOnlyList<TerrainPaletteLayer> Layers => _layers;
    /// <summary>Index zero is the no-tint entry; feature indices range from one through 63. A feature with a
    /// terrain-specific look (<see cref="ITileAppearanceService.Scatter"/>) gets its own index on that terrain.</summary>
    public IReadOnlyList<FeatureStyle?> FeatureGrounds => _featureGrounds;

    public TerrainPalette(GameCatalog catalog)
    {
        _content = catalog.Content;
        var appearances = _content.Services.Get<ITileAppearanceService>();
        var layers = new List<TerrainPaletteLayer>(MaxLayers);
        var surfaces = Enum.GetValues<TerrainSurface>();
        int terrainLayerLimit = MaxLayers - surfaces.Length;

        foreach (var terrain in _content.Get<Schema.TerrainDef>().All.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            var appearance = FirstAppearance(appearances, service => service.Terrain(terrain.Id));
            var color = Rgb.Hex(appearance?.Rgb ?? 0x777777);
            if (_terrainLayers.Count >= terrainLayerLimit) continue;

            int index = layers.Count;
            _terrainLayers.Add(terrain.Id, index);
            float spread = Math.Clamp(appearance?.Spread ?? 50, 0, 100) / 100f;
            layers.Add(new(terrain.Id, color, catalog.TexturePath(terrain.Id), Spread: spread,
                Water: terrain.IsWater, Ocean: terrain.IsWater && TerrainSnapshot.IsOcean(terrain.Id), Turn: TurnOf(appearance)));
        }

        foreach (var surface in surfaces)
        {
            var (defaultId, defaultRgb) = SurfaceDefaults[surface];
            string? textureId = appearances.Select(service => service.Surface(surface)).FirstOrDefault(id => id is not null);
            var id = textureId ?? defaultId;
            int index = layers.Count;
            _surfaceLayers.Add(surface, index);
            // A surface textured with a terrain's art turns like that terrain.
            var turn = TurnOf(textureId is null ? null : FirstAppearance(appearances, service => service.Terrain(textureId)));
            layers.Add(new(id, Rgb.Hex(defaultRgb), textureId is null ? null : catalog.TexturePath(textureId), surface, Turn: turn));
        }

        // Patch partners resolve once every terrain has its layer; a partner without a layer (or itself) is ignored.
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].Surface is not null) continue;
            var appearance = FirstAppearance(appearances, service => service.Terrain(layers[i].Id));
            if (appearance?.Patch is not { } partner || appearance.PatchPercent <= 0) continue;
            int patch = LayerOf(partner);
            if (patch == ColorOnlyLayer || patch == i) continue;
            layers[i] = layers[i] with { PatchLayer = patch, PatchShare = Math.Clamp(appearance.PatchPercent, 0, 100) / 100f };
        }

        _layers = Array.AsReadOnly(layers.ToArray());
        _featureGrounds = BuildFeatureGrounds(appearances);
    }

    private static float TurnOf(TileAppearance? appearance) => Math.Clamp(appearance?.TurnDegrees ?? 180, 0, 180) / 180f;

    /// <summary>Returns a material layer index, or <see cref="ColorOnlyLayer"/> when the terrain exceeds the layer budget.</summary>
    public int LayerOf(string terrainId) => _terrainLayers.GetValueOrDefault(terrainId, ColorOnlyLayer);
    public int SurfaceLayerOf(TerrainSurface surface) => _surfaceLayers.GetValueOrDefault(surface, ColorOnlyLayer);

    /// <summary>Returns a tint-table index in 0..63 for a feature on a terrain; zero means no feature ground.</summary>
    public int FeatureGroundOf(string? featureId, string terrainId) =>
        featureId is null ? 0 : _featureGroundIndices.GetValueOrDefault((featureId, terrainId));

    /// <summary>True when the feature ground at this index raises a crater in the height field.</summary>
    public bool IsCrater(int ground) =>
        ground > 0 && ground < _featureGrounds.Count && _featureGrounds[ground]?.Look.Ground == FeatureGround.Crater;

    /// <summary>Returns a terrain-specific scatter appearance, then the feature's general appearance.</summary>
    public TileAppearance? ScatterFor(string feature, string terrain)
    {
        var services = _content.Services.Get<ITileAppearanceService>();
        return FirstAppearance(services, service => service.Scatter(feature, terrain))
            ?? FirstAppearance(services, service => service.Feature(feature));
    }

    private IReadOnlyList<FeatureStyle?> BuildFeatureGrounds(IReadOnlyList<ITileAppearanceService> appearances)
    {
        var grounds = new List<FeatureStyle?> { null };
        var indices = new Dictionary<FeatureStyle, int>();
        var terrains = _content.Get<Schema.TerrainDef>().All.Select(t => t.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        foreach (var feature in _content.Get<Schema.FeatureDef>().All.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            if (FirstAppearance(appearances, service => service.Feature(feature.Id)) is null) continue;
            foreach (var terrain in terrains)
            {
                if (ScatterFor(feature.Id, terrain) is not { } appearance) continue;
                var style = StyleOf(appearance);
                if (!indices.TryGetValue(style, out int index))
                {
                    if (grounds.Count > MaxFeatureGroundIndex) continue;
                    indices[style] = index = grounds.Count;
                    grounds.Add(style);
                }
                _featureGroundIndices[(feature.Id, terrain)] = index;
            }
        }
        return Array.AsReadOnly(grounds.ToArray());
    }

    /// <summary>The ground part of an appearance; props and marks do not change the ground.</summary>
    internal static FeatureStyle StyleOf(TileAppearance appearance) =>
        new(Rgb.Hex(appearance.Rgb), appearance.TintPercent / 100f, 0, appearance.Look ?? new FeatureLook());

    private static TileAppearance? FirstAppearance(
        IReadOnlyList<ITileAppearanceService> services,
        Func<ITileAppearanceService, TileAppearance?> select) =>
        services.Select(select).FirstOrDefault(appearance => appearance is not null);
}
