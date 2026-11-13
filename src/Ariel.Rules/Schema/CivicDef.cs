using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Civic content. All authoritative numbers are integers.</summary>
/// <remarks>
/// A civic's plain modifiers are data in <see cref="Effects"/>: Government adds each one, with the civic's id as the
/// ledger source, to every calculation of its scope for the player who runs the civic, so a civic can give any
/// declared modifier key, including one a mod defines and reads. The typed fields below are the rules whose effect
/// depends on more than the calculation's subject (capital, buildings, work area, other players) or that a base
/// module reads directly. A feature mod says what a civic means to it by attaching its own data
/// (<see cref="ContentAttachment"/>), never through a field here.
/// </remarks>
public sealed class CivicDef : GameContentDef
{
    /// <summary>Plain modifiers, in key then target order.</summary>
    public ImmutableArray<ModifierEffect> Effects { get; init; } = [];
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, CommerceVector> BuildingClassCommerceChanges { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BuildingHappinessChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BuildingHealthChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    public CommerceVector CapitalCommerceModifiers { get; init; } = default;
    public YieldVector CapitalYieldModifiers { get; init; } = default;
    [ContentReference(typeof(CivicOptionDef), ReferencePart.Values)]
    public string? Option { get; init; } = null;
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
    public CommerceVector SpecialistExtraCommerces { get; init; } = default;
    [ContentReference(typeof(UpkeepDef), ReferencePart.Values)]
    public string? Upkeep { get; init; } = null;
    public bool FixedBorders { get; init; } = false;
    public bool NoCapitalUnhappiness { get; init; } = false;
    public bool NoLandmarkAnger { get; init; } = false;
    public bool UpgradeAnywhere { get; init; } = false;
    public int AiWeight { get; init; } = 0;
    public int AnarchyLength { get; init; } = 0;
    public int BaseFreeMilitaryUnits { get; init; } = 0;
    public int BaseFreeUnits { get; init; } = 0;
    public int CityLimit { get; init; } = 0;
    public int CivicPercentAnger { get; init; } = 0;
    public int DistantUnitSupportCostModifier { get; init; } = 0;
    public int EnslavementChance { get; init; } = 0;
    public int FractionalXPEarnedInCity { get; init; } = 0;
    public int FreeMilitaryUnitsPopulationPercent { get; init; } = 0;
    public int FreeUnitsPopulationPercent { get; init; } = 0;
    public int GoldPerMilitaryUnit { get; init; } = 0;
    public int GoldPerUnit { get; init; } = 0;
    public int HappyPerMilitaryUnit { get; init; } = 0;
    public int HomeAreaMaintenanceModifier { get; init; } = 0;
    public int HurryCostModifier { get; init; } = 0;
    public int HurryInflationModifier { get; init; } = 0;
    public int Inflation { get; init; } = 0;
    public int LandmarkHappiness { get; init; } = 0;
    public int LargestCityHappiness { get; init; } = 0;
    public int MaxConscript { get; init; } = 0;
    public int OtherAreaMaintenanceModifier { get; init; } = 0;
    public int TaxRateUnhappiness { get; init; } = 0;
}
