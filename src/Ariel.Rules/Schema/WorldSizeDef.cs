using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>WorldSize content. All authoritative numbers are integers.</summary>
public sealed class WorldSizeDef : GameContentDef
{
    public ImmutableDictionary<string, int> ScalingPercents { get; init; } = ImmutableDictionary<string, int>.Empty;
    public int AdvancedStartPointsMod { get; init; } = 0;
    public int BuildingClassPrereqModifier { get; init; } = 0;
    public int ColonyMaintenancePercent { get; init; } = 0;
    public int CorporationMaintenancePercent { get; init; } = 0;
    public int DefaultPlayers { get; init; } = 0;
    public int DistanceMaintenancePercent { get; init; } = 0;
    public int FeatureGrainChange { get; init; } = 0;
    public int GridHeight { get; init; } = 0;
    public int GridWidth { get; init; } = 0;
    public int MaxConscriptModifier { get; init; } = 0;
    public int NumCitiesAnarchyPercent { get; init; } = 0;
    public int NumCitiesMaintenancePercent { get; init; } = 0;
    public int NumCitiesUnhappinessDivisor { get; init; } = 0;
    public int NumFreeBuildingBonuses { get; init; } = 0;
    public int ResearchPercent { get; init; } = 0;
    public int TargetNumCities { get; init; } = 0;
    public int TerrainGrainChange { get; init; } = 0;
    public int TradeProfitPercent { get; init; } = 0;
    public int UnitNameModifier { get; init; } = 0;
    public int WarWearinessModifier { get; init; } = 0;
}
