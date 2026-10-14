using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Unit content. All authoritative numbers are integers.</summary>
public sealed class UnitDef : GameContentDef
{
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BonusProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ConstructibleBuildings { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(BuildDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Builds { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Values)]
    public string? Capture { get; init; } = null;
    [ContentReference(typeof(CivilizationDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, string> CivilizationNames { get; init; } = ImmutableDictionary<string, string>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Values)]
    public string? CombatType { get; init; } = null;
    [ContentReference(typeof(CorporationIdentityDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CorporationSpreads { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(DomainDef), ReferencePart.Values)]
    public string? Domain { get; init; } = null;
    [ContentReference(typeof(DomainDef), ReferencePart.Values)]
    public string? CargoDomain { get; init; } = null;
    [ContentReference(typeof(DomainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> DomainMods { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FeatureAttacks { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FeatureDefenses { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> FeatureImpassables { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> FeatureNatives { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FlankingStrikes { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, int> Flavors { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ForceBuildings { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? ForceObsoleteTech { get; init; } = null;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ForceObsoleteUnitClasses { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(PromotionDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> FreePromotions { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> GreatPeoples { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(InvisibilityDef), ReferencePart.Values)]
    public string? Invisibility { get; init; } = null;
    [ContentReference(typeof(PromotionDef), ReferencePart.Values)]
    public string? LeaderPromotion { get; init; } = null;
    [ContentReference(typeof(UnitAiDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ExcludedUnitAiTypes { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(BuildingDef), ReferencePart.Values)]
    public ImmutableArray<string> OverrideResourceBuilding { get; init; } = [];
    [ContentReference(typeof(TraitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> TraitProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(ReligionDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ReligionSpreads { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(InvisibilityDef), ReferencePart.Values)]
    public ImmutableArray<string> VisibleInvisibilityTypes { get; init; } = [];
    [ContentReference(typeof(UnitCategoryDef), ReferencePart.Values)]
    public ImmutableArray<string> Categories { get; init; } = [];
    [ContentReference(typeof(UnitCategoryDef), ReferencePart.Values)]
    public ImmutableArray<string> CargoCategories { get; init; } = [];
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> TerrainAttacks { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> TerrainImpassables { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> TerrainNatives { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public ImmutableDictionary<string, string?> TerrainPassableTechs { get; init; } = ImmutableDictionary<string, string?>.Empty;
    public ImmutableArray<string> UniqueNames { get; init; } = [];
    [ContentReference(typeof(UnitAiDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> UnitAiTypes { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitAttackModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> DefenderUnits { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitDefenseModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> TargetUnits { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Upgrades { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CollateralImmuneCombatTypes { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> DefenderCombatTypes { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CombatTypeModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> TargetCombatTypes { get; init; } = ImmutableDictionary<string, bool>.Empty;
    public bool AlwaysHostile { get; init; } = false;
    public bool Animal { get; init; } = false;
    public bool BarbariansColonisator { get; init; } = false;
    public bool BribeBarbarian { get; init; } = false;
    public bool CanMoveAllTerrain { get; init; } = false;
    public bool CanMoveImpassable { get; init; } = false;
    public bool CounterSpy { get; init; } = false;
    public bool AirBomb1 { get; init; } = false;
    public bool AirBomb2 { get; init; } = false;
    public bool AirBomb3 { get; init; } = false;
    public bool AirBomb4 { get; init; } = false;
    public bool AirBomb5 { get; init; } = false;
    public bool FighterEngagement { get; init; } = false;
    public bool Destroy { get; init; } = false;
    public bool FirstStrikeImmune { get; init; } = false;
    public bool FlatMovementCost { get; init; } = false;
    public bool FoodProduction { get; init; } = false;
    public bool ForcePeace { get; init; } = false;
    public bool CanFoundCity { get; init; } = false;
    public bool GoldenAge { get; init; } = false;
    public bool Goodwill { get; init; } = false;
    public bool GreatGeneral { get; init; } = false;
    public bool HiddenNationality { get; init; } = false;
    public bool IgnoreBuildingDefense { get; init; } = false;
    public bool IgnoreTerrainCost { get; init; } = false;
    public bool Inquisitor { get; init; } = false;
    public bool Investigate { get; init; } = false;
    public bool Invisible { get; init; } = false;
    public bool LineOfSight { get; init; } = false;
    public bool Mechanized { get; init; } = false;
    public bool MilitaryHappiness { get; init; } = false;
    public bool MilitaryProduction { get; init; } = false;
    public bool MilitarySupport { get; init; } = false;
    public bool MilitaryTrade { get; init; } = false;
    public bool NoBadGoodies { get; init; } = false;
    public bool NoCapture { get; init; } = false;
    public bool NoDefensiveBonus { get; init; } = false;
    public bool NoRevealMap { get; init; } = false;
    public bool NukeImmune { get; init; } = false;
    public bool OnlyDefensive { get; init; } = false;
    public bool Pillage { get; init; } = false;
    public bool RequiresResourceArea { get; init; } = false;
    public bool RequiresReligion { get; init; } = false;
    public bool QuickCombat { get; init; } = false;
    public bool RivalTerritory { get; init; } = false;
    public bool Sabotage { get; init; } = false;
    public bool Spy { get; init; } = false;
    public bool RequiresStateReligion { get; init; } = false;
    public bool StealPlans { get; init; } = false;
    public bool Suicide { get; init; } = false;
    public bool UpgradeImprovements { get; init; } = false;
    public bool WorkerTrade { get; init; } = false;
    public int AiWeight { get; init; } = 0;
    public int AdvancedStartCost { get; init; } = 0;
    public int AdvancedStartCostIncrease { get; init; } = 0;
    public int AirCombat { get; init; } = 0;
    public int AirCombatLimit { get; init; } = 0;
    public int AirRange { get; init; } = 0;
    public int AirUnitCap { get; init; } = 0;
    public int AnimalCombat { get; init; } = 0;
    public int Asset { get; init; } = 0;
    public int BaseDiscover { get; init; } = 0;
    public int BaseFoodChange { get; init; } = 0;
    public int BaseHurry { get; init; } = 0;
    public int BaseTrade { get; init; } = 0;
    public int BombRate { get; init; } = 0;
    public int BombardRate { get; init; } = 0;
    public int Cargo { get; init; } = 0;
    public int ChanceFirstStrikes { get; init; } = 0;
    public int CityAttack { get; init; } = 0;
    public int CityDefense { get; init; } = 0;
    public int CollateralDamage { get; init; } = 0;
    public int CollateralDamageLimit { get; init; } = 0;
    public int CollateralDamageMaxUnits { get; init; } = 0;
    public int Strength { get; init; } = 0;
    public int CombatLimit { get; init; } = 0;
    public int CommandRange { get; init; } = 0;
    public int Conscription { get; init; } = 0;
    public int ControlPoints { get; init; } = 0;
    public int Cost { get; init; } = 0;
    public int CultureGarrison { get; init; } = 0;
    public int BombAccuracyPercent { get; init; } = 0;
    public int BombRange { get; init; } = 0;
    public int DiscoverMultiplier { get; init; } = 0;
    public int DropRange { get; init; } = 0;
    public int EspionagePoints { get; init; } = 0;
    public int EvasionPercent { get; init; } = 0;
    public int ExtraCost { get; init; } = 0;
    public int FirstStrikes { get; init; } = 0;
    public int GreatWorkCulture { get; init; } = 0;
    public int HillsAttack { get; init; } = 0;
    public int HillsDefense { get; init; } = 0;
    public int HurryCostModifier { get; init; } = 0;
    public int HurryMultiplier { get; init; } = 0;
    public int InterceptionPercent { get; init; } = 0;
    public int LeaderExperience { get; init; } = 0;
    public int MinAreaSize { get; init; } = 0;
    public int Moves { get; init; } = 0;
    public int NukeRange { get; init; } = 0;
    public int OrderPriority { get; init; } = 0;
    public int Power { get; init; } = 0;
    public ImmutableArray<int> SpecialCargoCapacity { get; init; } = [];
    public int TotalCargo { get; init; } = 0;
    public int TradeMultiplier { get; init; } = 0;
    public int WithdrawalPercent { get; init; } = 0;
    public int WorkRate { get; init; } = 0;
    public int AttackExperience { get; init; } = 0;
    public int DefenseExperience { get; init; } = 0;
    [ContentReference(typeof(UnitDef))]
    public string DefaultUnit { get; init; } = "";
    public ClassLimits Limits { get; init; } = new();
    [ContentReference(typeof(UnitAiDef))]
    public string? DefaultUnitAi { get; init; }
    public ImmutableArray<OutcomeEffect> KillOutcomes { get; init; } = [];
    [ContentReference(typeof(MissionDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, ImmutableArray<OutcomeEffect>> Actions { get; init; } = ImmutableDictionary<string, ImmutableArray<OutcomeEffect>>.Empty;
}
