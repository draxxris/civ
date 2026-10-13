using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Handicap content. All authoritative numbers are integers.</summary>
public sealed class HandicapDef : GameContentDef
{
    [ContentReference(typeof(TechDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> AiStartingTechs { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> StartingTechs { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(GoodyDef), ReferencePart.Values)]
    public ImmutableArray<string> Goodies { get; init; } = [];
    public int AiAdvancedStartPercent { get; init; } = 0;
    public int AiAnimalBonus { get; init; } = 0;
    public int AiBarbarianBonus { get; init; } = 0;
    public int AiCivicUpkeepPercent { get; init; } = 0;
    public int AiConstructPercent { get; init; } = 0;
    public int AiCreatePercent { get; init; } = 0;
    public int AiDeclareWarProbability { get; init; } = 0;
    public int AiGrowthPercent { get; init; } = 0;
    public int AiInflationPercent { get; init; } = 0;
    public int AiPerEraModifier { get; init; } = 0;
    public int AiResearchPercent { get; init; } = 0;
    public int AiStartingDefenseUnits { get; init; } = 0;
    public int AiStartingExploreUnits { get; init; } = 0;
    public int AiStartingUnitMultiplier { get; init; } = 0;
    public int AiStartingWorkerUnits { get; init; } = 0;
    public int AiTrainPercent { get; init; } = 0;
    public int AiUnitCostPercent { get; init; } = 0;
    public int AiUnitSupplyPercent { get; init; } = 0;
    public int AiUnitUpgradePercent { get; init; } = 0;
    public int AiWarWearinessPercent { get; init; } = 0;
    public int AiWorkRateModifier { get; init; } = 0;
    public int AiWorldConstructPercent { get; init; } = 0;
    public int AiWorldCreatePercent { get; init; } = 0;
    public int AiWorldTrainPercent { get; init; } = 0;
    public int AdvancedStartPointsMod { get; init; } = 0;
    public int AnimalAttackProbability { get; init; } = 0;
    public int AnimalBonus { get; init; } = 0;
    public int AttitudeChange { get; init; } = 0;
    public int BarbarianBonus { get; init; } = 0;
    public int BarbarianCityCreationProbability { get; init; } = 0;
    public int BarbarianCityCreationTurnsElapsed { get; init; } = 0;
    public int BarbarianCreationTurnsElapsed { get; init; } = 0;
    public int BarbarianDefenders { get; init; } = 0;
    public int CivicUpkeepPercent { get; init; } = 0;
    public int ColonyMaintenancePercent { get; init; } = 0;
    public int CorporationMaintenancePercent { get; init; } = 0;
    public int DistanceMaintenancePercent { get; init; } = 0;
    public int FreeUnits { get; init; } = 0;
    public int FreeWinsAgainstBarbarians { get; init; } = 0;
    public int Gold { get; init; } = 0;
    public int HappyBonus { get; init; } = 0;
    public int HealthBonus { get; init; } = 0;
    public int InflationPercent { get; init; } = 0;
    public int MaxColonyMaintenance { get; init; } = 0;
    public int MaxNumCitiesMaintenance { get; init; } = 0;
    public int NoTechTradeModifier { get; init; } = 0;
    public int NumCitiesMaintenancePercent { get; init; } = 0;
    public int ResearchPercent { get; init; } = 0;
    public int StartingDefenseUnits { get; init; } = 0;
    public int StartingExploreUnits { get; init; } = 0;
    public int StartingLocPercent { get; init; } = 0;
    public int StartingWorkerUnits { get; init; } = 0;
    public int TechTradeKnownModifier { get; init; } = 0;
    public int UnitCostPercent { get; init; } = 0;
    public int UnownedTilesPerBarbarianCity { get; init; } = 0;
    public int UnownedTilesPerBarbarianUnit { get; init; } = 0;
    public int UnownedTilesPerGameAnimal { get; init; } = 0;
    public int UnownedWaterTilesPerBarbarianUnit { get; init; } = 0;
}
