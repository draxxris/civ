using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client.Terrain;

/// <summary>A terrain or surface material layer with its colour fallback and optional texture.</summary>
public sealed record TerrainPaletteLayer(string Id, Rgb Fallback, string? TexturePath, TerrainSurface? Surface = null);

/// <summary>Deterministic, engine-free material and scatter lookups for the loaded ruleset.</summary>
public sealed class TerrainPalette
{
    public const int MaxLayers = 32;
    public const int ColorOnlyLayer = -1;
    private const int MaxFeatureGroundIndex = 15;

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
    private readonly Dictionary<string, Rgb> _terrainColors = new(StringComparer.Ordinal);
    private readonly Dictionary<TerrainSurface, int> _surfaceLayers = new();
    private readonly Dictionary<string, int> _featureGroundIndices = new(StringComparer.Ordinal);
    private readonly ContentDb _content;
    private readonly IReadOnlyList<TerrainPaletteLayer> _layers;
    private readonly IReadOnlyList<FeatureStyle?> _featureGrounds;

    public IReadOnlyList<TerrainPaletteLayer> Layers => _layers;
    /// <summary>Index zero is the no-tint entry; feature indices range from one through fifteen.</summary>
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
            _terrainColors[terrain.Id] = color;
            if (_terrainLayers.Count >= terrainLayerLimit) continue;

            int index = layers.Count;
            _terrainLayers.Add(terrain.Id, index);
            layers.Add(new(terrain.Id, color, catalog.TexturePath(terrain.Id)));
        }

        foreach (var surface in surfaces)
        {
            var (defaultId, defaultRgb) = SurfaceDefaults[surface];
            string? textureId = appearances.Select(service => service.Surface(surface)).FirstOrDefault(id => id is not null);
            var id = textureId ?? defaultId;
            int index = layers.Count;
            _surfaceLayers.Add(surface, index);
            layers.Add(new(id, Rgb.Hex(defaultRgb), textureId is null ? null : catalog.TexturePath(textureId), surface));
        }

        _layers = Array.AsReadOnly(layers.ToArray());
        _featureGrounds = BuildFeatureGrounds(appearances);
    }

    /// <summary>Returns a material layer index, or <see cref="ColorOnlyLayer"/> when the terrain exceeds the layer budget.</summary>
    public int LayerOf(string terrainId) => _terrainLayers.GetValueOrDefault(terrainId, ColorOnlyLayer);

    /// <summary>Colour for terrain definitions that use the color-only fallback, or the default gray for unknown ids.</summary>
    public Rgb TerrainColorOf(string terrainId) => _terrainColors.GetValueOrDefault(terrainId, Rgb.Hex(0x777777));

    public int SurfaceLayerOf(TerrainSurface surface) => _surfaceLayers.GetValueOrDefault(surface, ColorOnlyLayer);

    /// <summary>Returns a tint-table index in 0..15; zero means no feature ground tint.</summary>
    public int FeatureGroundOf(string? featureId) => featureId is null ? 0 : _featureGroundIndices.GetValueOrDefault(featureId);

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
        foreach (var feature in _content.Get<Schema.FeatureDef>().All.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            var appearance = FirstAppearance(appearances, service => service.Feature(feature.Id));
            if (appearance is null || grounds.Count > MaxFeatureGroundIndex) continue;
            int index = grounds.Count;
            _featureGroundIndices[feature.Id] = index;
            grounds.Add(new(Rgb.Hex(appearance.Rgb), appearance.TintPercent / 100f, appearance.Marks));
        }
        return Array.AsReadOnly(grounds.ToArray());
    }

    private static TileAppearance? FirstAppearance(
        IReadOnlyList<ITileAppearanceService> services,
        Func<ITileAppearanceService, TileAppearance?> select) =>
        services.Select(select).FirstOrDefault(appearance => appearance is not null);
}
