using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Promotion content. All authoritative numbers are integers.</summary>
public sealed class PromotionDef : GameContentDef
{
    [ContentReference(typeof(DomainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> DomainMods { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FeatureAttacks { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FeatureDefenses { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(FeatureDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> FeatureDoubleMoves { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(TerrainDef), ReferencePart.Values)]
    public string? IgnoreTerrainDamage { get; init; } = null;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? ObsoleteTech { get; init; } = null;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> TerrainAttacks { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> TerrainDefenses { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> TerrainDoubleMoves { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CombatTypeModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> CombatTypes { get; init; } = ImmutableDictionary<string, bool>.Empty;
    public bool AlwaysHeal { get; init; } = false;
    public bool Amphibious { get; init; } = false;
    public bool Blitz { get; init; } = false;
    public bool CanLeadThroughPeaks { get; init; } = false;
    public bool CanMovePeaks { get; init; } = false;
    public bool EnemyRoute { get; init; } = false;
    public bool HillsDoubleMove { get; init; } = false;
    public bool ImmuneToFirstStrikes { get; init; } = false;
    public bool IsPirate { get; init; } = false;
    public bool Leader { get; init; } = false;
    public bool Onslaught { get; init; } = false;
    public bool RiverAttack { get; init; } = false;
    public bool ZoneOfControl { get; init; } = false;
    public int AdjacentTileHealChange { get; init; } = 0;
    public int AirRangeChange { get; init; } = 0;
    public int BombardRateChange { get; init; } = 0;
    public int CargoChange { get; init; } = 0;
    public int ChanceFirstStrikesChange { get; init; } = 0;
    public int CityAttack { get; init; } = 0;
    public int CityDefense { get; init; } = 0;
    public int CollateralDamageChange { get; init; } = 0;
    public int CollateralDamageProtection { get; init; } = 0;
    public int CombatPercent { get; init; } = 0;
    public int CommandRange { get; init; } = 0;
    public int ControlPoints { get; init; } = 0;
    public int EnemyHealChange { get; init; } = 0;
    public int EvasionChange { get; init; } = 0;
    public int ExperiencePercent { get; init; } = 0;
    public int FirstStrikesChange { get; init; } = 0;
    public int FriendlyHealChange { get; init; } = 0;
    public int HillsAttack { get; init; } = 0;
    public int HillsDefense { get; init; } = 0;
    public int InterceptChange { get; init; } = 0;
    public int KamikazePercent { get; init; } = 0;
    public int MoveDiscountChange { get; init; } = 0;
    public int MovesChange { get; init; } = 0;
    public int NeutralHealChange { get; init; } = 0;
    public int PillageChange { get; init; } = 0;
    public int SameTileHealChange { get; init; } = 0;
    public int UpgradeDiscount { get; init; } = 0;
    public int VisibilityChange { get; init; } = 0;
    public int WithdrawalChange { get; init; } = 0;
    public int RevoltProtectionPercent { get; init; }
}
