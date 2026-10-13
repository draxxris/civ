using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Build content. All authoritative numbers are integers.</summary>
public sealed class BuildDef : GameContentDef
{
    [ContentReference(typeof(FeatureDef), ReferencePart.Values)]
    public string? FeatureChange { get; init; } = null;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, BuildFeature> FeatureEffects { get; init; } = ImmutableDictionary<string, BuildFeature>.Empty;
    [ContentReference(typeof(ImprovementDef), ReferencePart.Values)]
    public string? ImprovementType { get; init; } = null;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? ObsoleteTech { get; init; } = null;
    [ContentReference(typeof(RouteDef), ReferencePart.Values)]
    public string? RouteType { get; init; } = null;
    [ContentReference(typeof(TerrainDef), ReferencePart.Values)]
    public string? TerrainChange { get; init; } = null;
    public bool Kill { get; init; } = false;
    public bool Mine { get; init; } = false;
    public int Cost { get; init; } = 0;
    public int Time { get; init; } = 0;
}
