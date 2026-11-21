using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.GreatPeople;

// What base definitions mean to great people and golden ages, kept in this mod's own rows (see "Content attachments"
// in docs/DESIGN.md). A definition without a row means nothing to great people: read it as None, whose values are
// all zero or empty.

/// <summary>What a building means to great people: the great person its points accumulate towards, and how it
/// lengthens golden ages and enriches trade missions.</summary>
public sealed class BuildingGreatPeopleDef : ContentAttachment<BuildingDef>
{
    /// <summary>The row of a building nothing attached to.</summary>
    public static readonly BuildingGreatPeopleDef None = new();
    /// <summary>The great person the building's great people points count towards.</summary>
    [ContentReference(typeof(UnitDef), ReferencePart.Values)]
    public string? GreatPersonUnit { get; init; }
    /// <summary>Percent length of the owner's golden ages.</summary>
    public int GoldenAgeModifier { get; init; }
    /// <summary>Percent gold of the owner's team's trade missions.</summary>
    public int TradeMissionModifier { get; init; }
}

/// <summary>What a unit means to great people: the specialists it can settle as, the buildings it can found, its
/// leader and great general traits, and the size of its discovery, hurry and great work actions.</summary>
public sealed class UnitGreatPeopleDef : ContentAttachment<UnitDef>
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitGreatPeopleDef None = new();
    /// <summary>Specialists the unit can settle in a city as.</summary>
    [ContentReference(typeof(SpecialistDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> GreatPeoples { get; init; } = ImmutableDictionary<string, bool>.Empty;
    /// <summary>Buildings the unit founds regardless of their requirements.</summary>
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> ForceBuildings { get; init; } = ImmutableDictionary<string, bool>.Empty;
    /// <summary>Born from great general points.</summary>
    public bool GreatGeneral { get; init; }
    /// <summary>The promotion the unit gives the unit it leads.</summary>
    [ContentReference(typeof(PromotionDef), ReferencePart.Values)]
    public string? LeaderPromotion { get; init; }
    public int LeaderExperience { get; init; }
    public int BaseDiscover { get; init; }
    public int DiscoverMultiplier { get; init; }
    public int BaseHurry { get; init; }
    public int HurryMultiplier { get; init; }
    public int GreatWorkCulture { get; init; }
}

/// <summary>What a technology means to great people: percent gold of the team's trade missions.</summary>
public sealed class TechGreatPeopleDef : ContentAttachment<TechDef>
{
    /// <summary>The row of a technology nothing attached to.</summary>
    public static readonly TechGreatPeopleDef None = new();
    public int TradeMissionModifier { get; init; }
}

/// <summary>What a specialist means to great people: the great person its points count towards.</summary>
public sealed class SpecialistGreatPeopleDef : ContentAttachment<SpecialistDef>
{
    /// <summary>The row of a specialist nothing attached to.</summary>
    public static readonly SpecialistGreatPeopleDef None = new();
    [ContentReference(typeof(UnitDef), ReferencePart.Values)]
    public string? GreatPersonUnit { get; init; }
}

/// <summary>What a yield means to golden ages: the bonus a plot earns at the threshold, and its weight when the
/// computer player prices a golden age.</summary>
public sealed class YieldGreatPeopleDef : ContentAttachment<YieldDef>
{
    /// <summary>The row of a yield nothing attached to.</summary>
    public static readonly YieldGreatPeopleDef None = new();
    public int GoldenAgeYield { get; init; }
    public int GoldenAgeYieldThreshold { get; init; }
    public int AiWeightPercent { get; init; }
}

/// <summary>What a game speed means to great people: percent great people thresholds and action sizes.</summary>
public sealed class GameSpeedGreatPeopleDef : ContentAttachment<GameSpeedDef>
{
    /// <summary>The row of a game speed nothing attached to.</summary>
    public static readonly GameSpeedGreatPeopleDef None = new();
    public int GreatPeoplePercent { get; init; }
    public int UnitDiscoverPercent { get; init; }
    public int UnitGreatWorkPercent { get; init; }
    public int UnitHurryPercent { get; init; }
    public int UnitTradePercent { get; init; }
}

/// <summary>What a starting era means to great people: percent great people thresholds.</summary>
public sealed class EraGreatPeopleDef : ContentAttachment<EraDef>
{
    /// <summary>The row of an era nothing attached to.</summary>
    public static readonly EraGreatPeopleDef None = new();
    public int GreatPeoplePercent { get; init; }
}

/// <summary>The golden age rule constants, attached to the base game's <see cref="RuleConstantsDef"/>.</summary>
public sealed class RuleConstantsGreatPeopleDef : ContentAttachment<RuleConstantsDef>
{
    /// <summary>The row when nothing attached to the rule constants.</summary>
    public static readonly RuleConstantsGreatPeopleDef None = new();
    public int GoldenAgeTurns { get; init; }
    /// <summary>Percent great people rate during a golden age.</summary>
    public int GoldenAgeGreatPeoplePercent { get; init; }
}
