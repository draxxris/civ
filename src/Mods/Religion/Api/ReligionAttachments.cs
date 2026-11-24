using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Religion;

// What base definitions mean to religion, kept in this mod's own rows (see "Content attachments" in docs/DESIGN.md).
// A definition without a row means nothing to religion: read it as None, whose values are all zero or empty.

/// <summary>What a building means to religion: its faith, whether it needs a religion or a state religion, how it draws
/// faiths to its city, what it yields in state religion cities, whether it ignores the state religion, and the faith
/// whose shrine it is.</summary>
public sealed class BuildingReligionDef : ContentAttachment<BuildingDef>
{
    /// <summary>The row of a building nothing attached to.</summary>
    public static readonly BuildingReligionDef None = new();
    /// <summary>The faith the building belongs to (a temple, monastery, cathedral or shrine): a city builds it only with
    /// that state religion, unless it is free from it, and loses it when the faith leaves.</summary>
    [ContentReference(typeof(ReligionDef))]
    public string? Religion { get; init; }
    /// <summary>The building needs some religion in its city.</summary>
    public bool RequiresReligion { get; init; }
    /// <summary>The building needs its owner to have a state religion.</summary>
    public bool RequiresStateReligion { get; init; }
    /// <summary>Influence the building adds to each named faith's natural spread into its city.</summary>
    [ContentReference(typeof(ReligionDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ReligionChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    /// <summary>Commerce the building adds, across its owner's cities, to each building of the state religion.</summary>
    public CommerceVector StateReligionCommerces { get; init; }
    /// <summary>The building needs no matching state religion.</summary>
    public bool FreeFromStateReligion { get; init; }
    /// <summary>Happiness when the building's religion is its owner's state religion.</summary>
    public int StateReligionHappiness { get; init; }
    /// <summary>The faith whose shrine the building is: it earns that faith's commerce for every city holding it.</summary>
    [ContentReference(typeof(ReligionDef), ReferencePart.Values)]
    public string? GlobalReligionCommerce { get; init; }
}

/// <summary>What a base building grants that is religion's, filled into its row: the faith's promotion its city's new
/// units start with (the Shaolin Temple's martial arts) in its empty <see cref="BuildingDef.FreePromotion"/>.</summary>
public sealed class BuildingGrantsReligionDef : ContentAttachment<BuildingDef>
{
    [AmendsTarget] public string? FreePromotion { get; init; }
}

/// <summary>What a unit means to religion: whether it is an inquisitor, and the chance, per faith, that its spread
/// mission succeeds.</summary>
public sealed class UnitReligionDef : ContentAttachment<UnitDef>
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitReligionDef None = new();
    /// <summary>The unit performs inquisitions, where civics and the state religion allow them.</summary>
    public bool Inquisitor { get; init; }
    [ContentReference(typeof(ReligionDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ReligionSpreads { get; init; } = ImmutableDictionary<string, int>.Empty;
}

/// <summary>What a base unit gets that is religion's, added to its row: the shrines it builds (the great prophet) and
/// the faith's promotions it starts with (the crusader's, the Shaolin monk's).</summary>
public sealed class UnitGrantsReligionDef : ContentAttachment<UnitDef>
{
    [AmendsTarget] public ImmutableDictionary<string, bool> ConstructibleBuildings { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [AmendsTarget] public ImmutableDictionary<string, bool> FreePromotions { get; init; } = ImmutableDictionary<string, bool>.Empty;
}

/// <summary>What a civilization means to religion: the religious buildings and units it builds in place of the usual
/// ones, or not at all (the barbarians build none), added to its overrides.</summary>
public sealed class CivilizationReligionDef : ContentAttachment<CivilizationDef>
{
    [AmendsTarget] public ImmutableDictionary<string, string?> BuildingOverrides { get; init; } = ImmutableDictionary<string, string?>.Empty;
    [AmendsTarget] public ImmutableDictionary<string, string?> UnitOverrides { get; init; } = ImmutableDictionary<string, string?>.Empty;
}

/// <summary>The religion rule constants, attached to the base game's <see cref="RuleConstantsDef"/>.</summary>
public sealed class RuleConstantsReligionDef : ContentAttachment<RuleConstantsDef>
{
    /// <summary>The row when nothing attached to the rule constants.</summary>
    public static readonly RuleConstantsReligionDef None = new();
    /// <summary>Anarchy turns of a state religion change, before the player's anarchy modifiers.</summary>
    public int BaseReligionAnarchyTurns { get; init; }
}
