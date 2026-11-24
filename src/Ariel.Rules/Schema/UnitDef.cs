using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Unit content. All authoritative numbers are integers.</summary>
[ContentLeafKind("unit"), ContentLeafKind("unit-class")]
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
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Values)]
    public string? CombatType { get; init; } = null;
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
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FlankingStrikes { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, int> Flavors { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? ForceObsoleteTech { get; init; } = null;
    [ContentReference(typeof(PromotionDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> FreePromotions { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(InvisibilityDef), ReferencePart.Values)]
    public string? Invisibility { get; init; } = null;
    [ContentReference(typeof(TraitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> TraitProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
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
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public ImmutableDictionary<string, string?> TerrainPassableTechs { get; init; } = ImmutableDictionary<string, string?>.Empty;
    [ContentReference(typeof(UnitAiDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> UnitAiTypes { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitAttackModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitDefenseModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Upgrades { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CollateralImmuneCombatTypes { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CombatTypeModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    public bool AlwaysHostile { get; init; } = false;
    public bool Animal { get; init; } = false;
    public bool CanMoveAllTerrain { get; init; } = false;
    public bool CanMoveImpassable { get; init; } = false;
    public bool FirstStrikeImmune { get; init; } = false;
    public bool FlatMovementCost { get; init; } = false;
    public bool FoodProduction { get; init; } = false;
    public bool CanFoundCity { get; init; } = false;
    public bool GoldenAge { get; init; } = false;
    public bool HiddenNationality { get; init; } = false;
    public bool IgnoreBuildingDefense { get; init; } = false;
    public bool IgnoreTerrainCost { get; init; } = false;
    public bool Inquisitor { get; init; } = false;
    public bool Invisible { get; init; } = false;
    public bool MilitaryHappiness { get; init; } = false;
    public bool MilitaryProduction { get; init; } = false;
    public bool MilitarySupport { get; init; } = false;
    public bool NoBadGoodies { get; init; } = false;
    public bool NoCapture { get; init; } = false;
    public bool NoDefensiveBonus { get; init; } = false;
    public bool NoRevealMap { get; init; } = false;
    public bool NukeImmune { get; init; } = false;
    public bool OnlyDefensive { get; init; } = false;
    public bool Pillage { get; init; } = false;
    public bool RivalTerritory { get; init; } = false;
    public bool Suicide { get; init; } = false;
    public int AdvancedStartCost { get; init; } = 0;
    public int AdvancedStartCostIncrease { get; init; } = 0;
    public int AirCombat { get; init; } = 0;
    public int AirUnitCap { get; init; } = 0;
    public int AnimalCombat { get; init; } = 0;
    public int BaseTrade { get; init; } = 0;
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
    public int Conscription { get; init; } = 0;
    public int Cost { get; init; } = 0;
    public int BombRange { get; init; } = 0;
    public int ExtraCost { get; init; } = 0;
    public int FirstStrikes { get; init; } = 0;
    public int HillsAttack { get; init; } = 0;
    public int HillsDefense { get; init; } = 0;
    public int HurryCostModifier { get; init; } = 0;
    public int InterceptionPercent { get; init; } = 0;
    public int Moves { get; init; } = 0;
    public int NukeRange { get; init; } = 0;
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
}
