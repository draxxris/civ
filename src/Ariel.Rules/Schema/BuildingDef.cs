using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Building content. All authoritative numbers are integers.</summary>
public sealed class BuildingDef : GameContentDef
{
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BonusHappinessChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BonusHealthChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BonusDef), ReferencePart.Values)]
    public string? BonusType { get; init; } = null;
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> BonusYieldChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> BonusYieldModifiers { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BuildingClassProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BuildingHappinessChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    public CommerceVector CommerceChanges { get; init; } = default;
    public CommerceVector CommerceModifiers { get; init; } = default;
    [ContentReference(typeof(DomainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> DomainFreeExperiences { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(DomainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> DomainProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Values)]
    public string? ExtendsBuildingClass { get; init; } = null;
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ExtraFreeBonuses { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, int> Flavors { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(CorporationIdentityDef), ReferencePart.Values)]
    public string? FoundsCorporation { get; init; } = null;
    /// <summary>The religion this building founds when it is completed, with the completing city as its holy city.</summary>
    [ContentReference(typeof(ReligionIdentityDef), ReferencePart.Values)]
    public string? FoundsReligion { get; init; } = null;
    [ContentReference(typeof(BonusDef), ReferencePart.Values)]
    public string? FreeBonus { get; init; } = null;
    [ContentReference(typeof(BuildingDef), ReferencePart.Values)]
    public string? FreeBuilding { get; init; } = null;
    [ContentReference(typeof(PromotionDef), ReferencePart.Values)]
    public string? FreePromotion { get; init; } = null;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> GlobalBuildingClassProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, CommerceVector> GlobalBuildingExtraCommerces { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
    public CommerceVector GlobalCommerceModifiers { get; init; } = default;
    public YieldVector GlobalSeaPlotYieldChanges { get; init; } = default;
    public YieldVector GlobalYieldModifiers { get; init; } = default;
    [ContentReference(typeof(TraitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> TraitHappinessChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BonusDef), ReferencePart.Values)]
    public string? NoBonus { get; init; } = null;
    public CommerceVector ObsoleteSafeCommerceChanges { get; init; } = default;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? ObsoleteTech { get; init; } = null;
    [ContentReference(typeof(BonusDef), ReferencePart.Values)]
    public string? PowerBonus { get; init; } = null;
    public YieldVector PowerYieldModifiers { get; init; } = default;
    [ContentReference(typeof(TraitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> TraitProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(ReligionIdentityDef), ReferencePart.Values)]
    public string? ReligionType { get; init; } = null;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ReplaceBuildings { get; init; } = ImmutableDictionary<string, bool>.Empty;
    public YieldVector RiverPlotYieldChanges { get; init; } = default;
    public YieldVector SeaPlotYieldChanges { get; init; } = default;
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, CommerceVector> SpecialistCommercePercentChanges { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> SpecialistSlots { get; init; } = ImmutableDictionary<string, int>.Empty;
    public CommerceVector SpecialistExtraCommerces { get; init; } = default;
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> SpecialistYieldChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, CommerceVector> TechCommerceChanges { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, SpecialistSlotsChange> TechSpecialistChanges { get; init; } = ImmutableDictionary<string, SpecialistSlotsChange>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> TechYieldChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> TechYieldModifiers { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitClassProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitCombatExtraStrengths { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitCombatFreeExperiences { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, YieldVector> VicinityBonusYieldChanges { get; init; } = ImmutableDictionary<string, YieldVector>.Empty;
    public YieldVector YieldChanges { get; init; } = default;
    public YieldVector YieldModifiers { get; init; } = default;
    public bool AllowsNukes { get; init; } = false;
    public bool AreaCleanPower { get; init; } = false;
    public bool BuildingOnlyHealthy { get; init; } = false;
    public bool Capital { get; init; } = false;
    public bool AllowsNuclearStrikes { get; init; } = false;
    public bool DirtyPower { get; init; } = false;
    public bool GoldenAge { get; init; } = false;
    public bool GovernmentCenter { get; init; } = false;
    public bool NeverCapture { get; init; } = false;
    public bool NoPower { get; init; } = false;
    public bool NoUnhappiness { get; init; } = false;
    public bool NoUnhealthyPopulation { get; init; } = false;
    public bool NukeImmune { get; init; } = false;
    public bool ProducesPower { get; init; } = false;
    public bool RequiresPower { get; init; } = false;
    public bool RequiresReligion { get; init; } = false;
    public bool RequiresActiveCivics { get; init; } = false;
    public bool RiverLocation { get; init; } = false;
    public bool RequiresStateReligion { get; init; } = false;
    public bool TeamShare { get; init; } = false;
    public bool CoastalLocation { get; init; } = false;
    public int AiWeight { get; init; } = 0;
    public int AdvancedStartCost { get; init; } = 0;
    public int AdvancedStartCostIncrease { get; init; } = 0;
    public int AirModifier { get; init; } = 0;
    public int AirUnitCapacity { get; init; } = 0;
    public int AllCityDefense { get; init; } = 0;
    public int AnarchyModifier { get; init; } = 0;
    public int AreaFreeSpecialist { get; init; } = 0;
    public int AreaHappiness { get; init; } = 0;
    public int AreaHealth { get; init; } = 0;
    public int AreaMaintenanceModifier { get; init; } = 0;
    public int BombardDefense { get; init; } = 0;
    public int CapitalExtraCommerce { get; init; } = 0;
    public int CoastalDistanceMaintenanceModifier { get; init; } = 0;
    public int CaptureSurvivalPercent { get; init; } = 0;
    public int Cost { get; init; } = 0;
    public int DefensePercent { get; init; } = 0;
    public int DistanceMaintenanceModifier { get; init; } = 0;
    public int Experience { get; init; } = 0;
    public int FoodKept { get; init; } = 0;
    public int FreeSpecialist { get; init; } = 0;
    public int FreeTechs { get; init; } = 0;
    public int GlobalExperience { get; init; } = 0;
    public int GlobalFreeSpecialist { get; init; } = 0;
    public int GlobalGreatPeopleRateModifier { get; init; } = 0;
    public int GlobalHappiness { get; init; } = 0;
    public int GlobalHealth { get; init; } = 0;
    public int GlobalHurryModifier { get; init; } = 0;
    public int GlobalMaintenanceModifier { get; init; } = 0;
    public int GreatPeopleRateChange { get; init; } = 0;
    public int GreatPeopleRateModifier { get; init; } = 0;
    public int Happiness { get; init; } = 0;
    public int HappinessPercentPerPopulation { get; init; } = 0;
    public int HealRateChange { get; init; } = 0;
    public int Health { get; init; } = 0;
    public int HealthPercentPerPopulation { get; init; } = 0;
    public int HurryAngerModifier { get; init; } = 0;
    public int HurryCostModifier { get; init; } = 0;
    public int MaintenanceModifier { get; init; } = 0;
    public int MaxLatitude { get; init; } = 90;
    public int MilitaryProductionModifier { get; init; } = 0;
    public int MinAreaSize { get; init; } = 0;
    public int MinLatitude { get; init; } = 0;
    public int NukeExplosionRand { get; init; } = 0;
    public int NukeModifier { get; init; } = 0;
    public int CityCountMaintenancePercent { get; init; } = 0;
    public int FreeBonusCount { get; init; } = 0;
    public int OccupationTimeModifier { get; init; } = 0;
    public int PopulationGrowthPercent { get; init; } = 0;
    public int MinimumPopulation { get; init; } = 0;
    public int WonderCapacityIncrement { get; init; } = 0;
    public int WorkableRadius { get; init; } = 0;
    [ContentReference(typeof(BuildingDef))]
    public string DefaultBuilding { get; init; } = "";
    public ClassLimits Limits { get; init; } = new();
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BonusProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FreeSpecialistCounts { get; init; } = ImmutableDictionary<string, int>.Empty;
}
