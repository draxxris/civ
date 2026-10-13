using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Civic content. All authoritative numbers are integers.</summary>
public sealed class CivicDef : GameContentDef
{
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, CommerceVector> BuildingClassCommerceChanges { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BuildingClassProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BuildingHappinessChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BuildingHealthChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    public CommerceVector CapitalCommerceModifiers { get; init; } = default;
    public YieldVector CapitalYieldModifiers { get; init; } = default;
    [ContentReference(typeof(CivicDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, CivicAttitudeEffect> CivicAttitudeChanges { get; init; } = ImmutableDictionary<string, CivicAttitudeEffect>.Empty;
    [ContentReference(typeof(CivicOptionDef), ReferencePart.Values)]
    public string? Option { get; init; } = null;
    public CommerceVector CommerceModifiers { get; init; } = default;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FeatureHappinessChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, int> Flavors { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(HurryDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Hurrys { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(ImprovementDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ImprovementHealthPercentChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(ImprovementDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> ImprovementYieldChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    public YieldVector LandmarkYieldChanges { get; init; } = default;
    [ContentReference(typeof(BuildingCategoryDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> SpecialBuildingNotRequireds { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, CommerceVector> SpecialistCommercePercentChanges { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
    public CommerceVector SpecialistExtraCommerces { get; init; } = default;
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> SpecialistValids { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> SpecialistYieldPercentChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    public YieldVector TradeYieldModifiers { get; init; } = default;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitCombatProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UpkeepDef), ReferencePart.Values)]
    public string? Upkeep { get; init; } = null;
    public YieldVector YieldModifiers { get; init; } = default;
    public bool AllowInquisitions { get; init; } = false;
    public bool BuildingOnlyHealthy { get; init; } = false;
    public bool CanDoElection { get; init; } = false;
    public bool CanNotCeaseRelations { get; init; } = false;
    public bool Communism { get; init; } = false;
    public bool DisallowInquisitions { get; init; } = false;
    public bool EnablesMADNukes { get; init; } = false;
    public bool FixedBorders { get; init; } = false;
    public bool ForcesOpenTradeRoutesWithWeakerPlayers { get; init; } = false;
    public bool FreeReligion { get; init; } = false;
    public bool FreeSpeech { get; init; } = false;
    public bool MilitaryFoodProduction { get; init; } = false;
    public bool NoCapitalUnhappiness { get; init; } = false;
    public bool NoCorporations { get; init; } = false;
    public bool NoForeignCorporations { get; init; } = false;
    public bool NoForeignTrade { get; init; } = false;
    public bool NoLandmarkAnger { get; init; } = false;
    public bool NoNonStateReligionSpread { get; init; } = false;
    public bool NoUnhealthyPopulation { get; init; } = false;
    public bool RequiresStateReligion { get; init; } = false;
    public bool UpgradeAnywhere { get; init; } = false;
    public int AiWeight { get; init; } = 0;
    public int AnarchyLength { get; init; } = 0;
    public int AttitudeShareMod { get; init; } = 0;
    public int BaseFreeMilitaryUnits { get; init; } = 0;
    public int BaseFreeUnits { get; init; } = 0;
    public int CityLimit { get; init; } = 0;
    public int CivicHappiness { get; init; } = 0;
    public int CivicPercentAnger { get; init; } = 0;
    public int CorporationMaintenanceModifier { get; init; } = 0;
    public int DistanceMaintenanceModifier { get; init; } = 0;
    public int DistantUnitSupportCostModifier { get; init; } = 0;
    public int DomesticConnectednessModifier { get; init; } = 0;
    public int DomesticGreatGeneralRateModifier { get; init; } = 0;
    public int EnslavementChance { get; init; } = 0;
    public int ExpInBorderModifier { get; init; } = 0;
    public int ExtraCityDefense { get; init; } = 0;
    public int ExtraHealth { get; init; } = 0;
    public int ForeignConnectednessModifier { get; init; } = 0;
    public int ForeignTradeRouteModifier { get; init; } = 0;
    public int ForeignerUnhappyPercent { get; init; } = 0;
    public int FractionalXPEarnedInCity { get; init; } = 0;
    public int FreeExperience { get; init; } = 0;
    public int FreeMilitaryUnitsPopulationPercent { get; init; } = 0;
    public int FreeSpecialist { get; init; } = 0;
    public int FreeUnitsPopulationPercent { get; init; } = 0;
    public int GlobalRelationsChange { get; init; } = 0;
    public int GoldPerMilitaryUnit { get; init; } = 0;
    public int GoldPerUnit { get; init; } = 0;
    public int GreatGeneralRateModifier { get; init; } = 0;
    public int GreatPeopleRateModifier { get; init; } = 0;
    public int HappyPerMilitaryUnit { get; init; } = 0;
    public int HomeAreaMaintenanceModifier { get; init; } = 0;
    public int HurryCostModifier { get; init; } = 0;
    public int HurryInflationModifier { get; init; } = 0;
    public int ImprovementUpgradeRateModifier { get; init; } = 0;
    public int Inflation { get; init; } = 0;
    public int LandmarkHappiness { get; init; } = 0;
    public int LargestCityHappiness { get; init; } = 0;
    public int LessPowerfulRelationsChange { get; init; } = 0;
    public int MaxConscript { get; init; } = 0;
    public int MilitaryProductionModifier { get; init; } = 0;
    public int NonStateReligionHappiness { get; init; } = 0;
    public int CityCountMaintenancePercent { get; init; } = 0;
    public int OtherAreaMaintenanceModifier { get; init; } = 0;
    public int PopulationAngerModifier { get; init; } = 0;
    public int PopulationGrowthPercent { get; init; } = 0;
    public int PowerfulRelationsChange { get; init; } = 0;
    public int RealCorporationMaintenanceModifier { get; init; } = 0;
    public int ReligionSpreadRate { get; init; } = 0;
    public int SeizedForeignConnectednessPercent { get; init; } = 0;
    public int SharedCivicTradeRouteModifier { get; init; } = 0;
    public int StateReligionBuildingProductionModifier { get; init; } = 0;
    public int StateReligionFreeExperience { get; init; } = 0;
    public int StateReligionGreatPeopleRateModifier { get; init; } = 0;
    public int StateReligionHappiness { get; init; } = 0;
    public int StateReligionHealth { get; init; } = 0;
    public int StateReligionUnitProductionModifier { get; init; } = 0;
    public int TaxRateUnhappiness { get; init; } = 0;
    public int TradeRoutes { get; init; } = 0;
    public int TributePercent { get; init; } = 0;
    public int WarWearinessModifier { get; init; } = 0;
    public int WeakestRelationsChange { get; init; } = 0;
    public int WorkerSpeedModifier { get; init; } = 0;
}
