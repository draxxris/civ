using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Bonus content. All authoritative numbers are integers.</summary>
public sealed class BonusDef : GameContentDef
{
    [ContentReference(typeof(BonusClassDef), ReferencePart.Values)]
    public string? BonusClass { get; init; } = null;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Features { get; init; } = ImmutableDictionary<string, bool>.Empty;
    public ImmutableArray<int> AppearanceRandomRanges { get; init; } = [];
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? TradeTech { get; init; } = null;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? ObsoleteTech { get; init; } = null;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? RevealTech { get; init; } = null;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Terrains { get; init; } = ImmutableDictionary<string, bool>.Empty;
    public YieldVector YieldChanges { get; init; } = default;
    public bool Area { get; init; } = false;
    public bool Currency { get; init; } = false;
    public bool Flatlands { get; init; } = false;
    public bool Hills { get; init; } = false;
    public bool NoRiverSide { get; init; } = false;
    public bool Normalize { get; init; } = false;
    public bool Peaks { get; init; } = false;
    public bool UseLSystem { get; init; } = false;
    public int AiObjective { get; init; } = 0;
    public int AiTradeModifier { get; init; } = 0;
    public int ConstAppearance { get; init; } = 0;
    public int GroupRand { get; init; } = 0;
    public int GroupRange { get; init; } = 0;
    public int Happiness { get; init; } = 0;
    public int Health { get; init; } = 0;
    public int MaxLatitude { get; init; } = 90;
    public int MaximumPerWorld { get; init; } = -1;
    public int MinAreaSize { get; init; } = 0;
    public int MinLandPercent { get; init; } = 0;
    public int MinLatitude { get; init; } = 0;
    public int MinimumPerWorld { get; init; } = -1;
    public int PlacementOrder { get; init; } = 0;
    public int Player { get; init; } = 0;
    public int TilesPer { get; init; } = 0;
    public int Unique { get; init; } = 0;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> FeatureTerrains { get; init; } = ImmutableDictionary<string, bool>.Empty;
}
