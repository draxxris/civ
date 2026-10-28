using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Trait content. All authoritative numbers are integers.</summary>
public sealed class TraitDef : GameContentDef
{
    [ContentReference(typeof(BonusDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> BonusHappinessChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    public CommerceVector CommerceChanges { get; init; } = default;
    public CommerceVector CommerceModifiers { get; init; } = default;
    public YieldVector ExtraYieldThresholds { get; init; } = default;
    [ContentReference(typeof(PromotionDef), ReferencePart.Keys)]
    [ContentReference(typeof(UnitCombatDef), ReferencePart.Values)]
    public ImmutableDictionary<string, ImmutableArray<string>> CombatTypePromotions { get; init; } = ImmutableDictionary<string, ImmutableArray<string>>.Empty;
    public string ShortDescription { get; init; } = "";
    public YieldVector TradeYieldModifiers { get; init; } = default;
    public YieldVector YieldChanges { get; init; } = default;
    public bool ExtraCultureLevel { get; init; } = false;
    public bool NonStateReligionCommerce { get; init; } = false;
    public bool UpgradeAnywhere { get; init; } = false;
    public int DomesticGreatGeneralRateModifier { get; init; } = 0;
    public int GreatGeneralRateModifier { get; init; } = 0;
    public int GreatPeopleRateModifier { get; init; } = 0;
    public int Happiness { get; init; } = 0;
    public int Health { get; init; } = 0;
    public int ExperienceThresholdPercent { get; init; } = 0;
    public int MaximumAnarchyTurns { get; init; } = 0;
    public int WorldWonderProductionPercent { get; init; } = 0;
    public int NationalWonderProductionPercent { get; init; } = 0;
    public int TeamWonderProductionPercent { get; init; } = 0;
    public int UpkeepModifier { get; init; } = 0;
}
