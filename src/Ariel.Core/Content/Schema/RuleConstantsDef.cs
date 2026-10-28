using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Shared rule constants. Raw values retain exact source strings; typed values use integers.</summary>
public sealed class RuleConstantsDef : GameContentDef
{
    public RuleConstants Values { get; init; } = new();
    public ImmutableDictionary<string, string> Raw { get; init; } = ImmutableDictionary<string, string>.Empty;
}

public sealed record RuleConstants
{
    public int AirCombatDamage { get; init; }
    public int AmphibAttackModifier { get; init; }
    public int AtWarCultureAngerModifier { get; init; }
    public int BaseCityGrowthThreshold { get; init; }
    public int BaseResearchRate { get; init; }
    public int BaseUnitUpgradeCost { get; init; }
    public int CityDefenseDamageHealRate { get; init; }
    public int CityFreeCultureGrowthFactor { get; init; }
    public int CityGrowthMultiplier { get; init; }
    public int CityHealRate { get; init; }
    public int CityThirdRingExtraGrowthThresholdPercent { get; init; }
    public int CollateralCombatDamage { get; init; }
    public int CombatDamage { get; init; }
    public int CombatDieSides { get; init; }
    public int CombatExperienceInBordersPercent { get; init; }
    public int CultureDecayTurns { get; init; }
    public int CulturePercentAnger { get; init; }
    public int DirtyPowerHealthChange { get; init; }
    public int EnemyHealRate { get; init; }
    public int FoodConsumptionPerPopulation { get; init; }
    public int FreeCityAdjacentCulture { get; init; }
    public int FreeCityCulture { get; init; }
    public int FreshWaterHealthChange { get; init; }
    public int FriendlyHealRate { get; init; }
    public int HillsExtraDefense { get; init; }
    public int InitialCityMaintenance { get; init; }
    public int MaxDistanceCityMaintenance { get; init; }
    public int MaxExperienceAfterUpgrade { get; init; }
    public int MaxExperiencePerCombat { get; init; }
    public int MaxHitPoints { get; init; }
    public int MinCityRange { get; init; }
    public int MinExperiencePerCombat { get; init; }
    public int MoveDenominator { get; init; }
    public int NeutralHealRate { get; init; }
    public int PowerHealthChange { get; init; }
    public int RiverAttackModifier { get; init; }
    public int UnitUpgradeCostPerProduction { get; init; }
    /// <summary>Source growth factor multiplied by 1000, with no rounding.</summary>
    public int FoodWastageGrowthPermille { get; init; }
    public int ResearchExtraTeamMemberPercent { get; init; }
    public int ResearchFirstPrerequisitePercent { get; init; }
    public int ResearchKnownPrerequisitePercent { get; init; }
    public int ResearchCostPercent { get; init; }
    public int ResearchKnownTeamsPercent { get; init; }
    public int StartYear { get; init; }
    public int ResearchDiffusionEnabled { get; init; }
    public int ResearchDiffusionKnownTeamsPercent { get; init; }
    public int ResearchDiffusionWelfarePercent { get; init; }
    public int ResearchDiffusionWelfareThreshold { get; init; }
    public int RevolutionResearchCostPercent { get; init; } = 100;
    public int BaseCivicAnarchyTurns { get; init; }
    public int BaseReligionAnarchyTurns { get; init; }
    public int MaximumAnarchyTurns { get; init; }
    public int GoldenAgeTurns { get; init; }
    public int GoldenAgeGreatPeoplePercent { get; init; }
    public int InitialFreeUnits { get; init; }
    public int InitialFreeUnitsPopulationPercent { get; init; }
    public int UpkeepCityOffset { get; init; }
    public int UpkeepPopulationOffset { get; init; }
    public int HurryAngerDivisor { get; init; }
    public int HurryPopulationAnger { get; init; }
    public int NewHurryCostPercent { get; init; }
    public int MinimumConscriptPopulation { get; init; }
    public int ConscriptAngerDivisor { get; init; }
    public int ConscriptPopulationAnger { get; init; }
    public int PercentAngerDivisor { get; init; }
    public int NoMilitaryPercentAnger { get; init; }
    public int MaximumCityDefenseDamage { get; init; }
    public int TradeProfitPercent { get; init; }
}
