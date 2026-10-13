using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Improvement content. All authoritative numbers are integers.</summary>
public sealed class ImprovementDef : GameContentDef
{
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, ImprovementBonus> BonusEffects { get; init; } = ImmutableDictionary<string, ImprovementBonus>.Empty;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ValidFeatures { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(ImprovementDef), ReferencePart.Values)]
    public string? ImprovementPillage { get; init; } = null;
    [ContentReference(typeof(ImprovementDef), ReferencePart.Values)]
    public string? ImprovementUpgrade { get; init; } = null;
    public YieldVector IrrigatedYieldChange { get; init; } = default;
    public YieldVector PrereqNatureYields { get; init; } = default;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? PrereqTech { get; init; } = null;
    public YieldVector RiverSideYieldChange { get; init; } = default;
    [ContentReference(typeof(RouteDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> RouteYieldChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> TechYieldChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ValidTerrains { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(TraitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> TraitYieldChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    public YieldVector YieldChanges { get; init; } = default;
    public bool ActsAsCity { get; init; } = false;
    public bool CarriesIrrigation { get; init; } = false;
    public bool DepletedMine { get; init; } = false;
    public bool FreshWaterMakesValid { get; init; } = false;
    public bool Goody { get; init; } = false;
    public bool GraphicalOnly { get; init; } = false;
    public bool HillsMakesValid { get; init; } = false;
    public bool NoFreshWater { get; init; } = false;
    public bool OutsideBorders { get; init; } = false;
    public bool PeakMakesValid { get; init; } = false;
    public bool Permanent { get; init; } = false;
    public bool RequiresFeature { get; init; } = false;
    public bool RequiresFlatlands { get; init; } = false;
    public bool RequiresIrrigation { get; init; } = false;
    public bool RequiresRiverSide { get; init; } = false;
    public bool RiverSideMakesValid { get; init; } = false;
    public bool UseLSystem { get; init; } = false;
    public bool IsWater { get; init; } = false;
    public int AdvancedStartCost { get; init; } = 0;
    public int AdvancedStartCostIncrease { get; init; } = 0;
    public int AirBombDefense { get; init; } = 0;
    public int DefenseModifier { get; init; } = 0;
    public int DepletionRand { get; init; } = 0;
    public int FeatureGrowth { get; init; } = 0;
    public int GoodyRange { get; init; } = 0;
    public int Happiness { get; init; } = 0;
    public int HealthPercent { get; init; } = 0;
    public int PillageGold { get; init; } = 0;
    public int TilesPerGoody { get; init; } = 0;
    public int UpgradeTime { get; init; } = 0;
}
