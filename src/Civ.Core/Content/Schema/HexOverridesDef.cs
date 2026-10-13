using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Hand-authored hex adaptation, never emitted by the importer.</summary>
public sealed class HexOverridesDef : ContentDef
{
    public int SourceGridScale { get; init; }
    public int WaterPercent { get; init; }
    public int NoiseScale { get; init; }
    public int NoiseOctaves { get; init; }
    public int PolarPenalty { get; init; }
    public int HillsPercent { get; init; }
    public int PeaksPercent { get; init; }
    public int SnowLatitude { get; init; }
    public int TundraLatitude { get; init; }
    public int DesertMinLatitude { get; init; }
    public int DesertMaxLatitude { get; init; }
    public int DesertMoisture { get; init; }
    public int PlainsMoisture { get; init; }
    public int MarshMoisture { get; init; }
    public int RiverSourceOneIn { get; init; }
    public int LakeMaxSize { get; init; }
    public int StartRadius { get; init; }
    public int StartOuterRadius { get; init; }
    public int StartSpacing { get; init; }
    public int StartFoodWeight { get; init; }
    public int StartProductionWeight { get; init; }
    public int StartCoastWeight { get; init; }
    public int StartFreshWaterWeight { get; init; }
    public int StartQualityPercent { get; init; }
    public int UnitSight { get; init; }
    public int CitySight { get; init; }
    public int GovernorFoodWeight { get; init; } = 10;
    public int GovernorProductionWeight { get; init; } = 6;
    public int GovernorCommerceWeight { get; init; } = 4;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, FeatureClimate> FeatureClimate { get; init; } = ImmutableDictionary<string, FeatureClimate>.Empty;
}
public sealed record FeatureClimate(int MinLatitude, int MaxLatitude, int Appearance);
