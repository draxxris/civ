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

/// <summary>What a base building grants or accepts that is religion's, filled into its row: the faith's promotion its
/// city's new units start with (the Shaolin Temple's martial arts) in its empty <see cref="BuildingDef.FreePromotion"/>,
/// and the religious buildings its requirements accept beside a base alternative (the Apadana Palace and the Alhambra
/// as the throne room's palace).</summary>
public sealed class BuildingGrantsReligionDef : ContentAttachment<BuildingDef>
{
    [AmendsTarget] public string? FreePromotion { get; init; }
    /// <summary>By the id of an alternative already in one of the building's <c>any</c> groups, the alternatives that join
    /// that group.</summary>
    [AmendsTarget(nameof(Requirements))]
    public ImmutableDictionary<string, ImmutableArray<Requirement>> RequirementAlternatives { get; init; } =
        ImmutableDictionary<string, ImmutableArray<Requirement>>.Empty;
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
/// ones, or not at all (the barbarians build none), added to its overrides, and its starting civic in the religion
/// category (Folklore), added to its starting civics.</summary>
public sealed class CivilizationReligionDef : ContentAttachment<CivilizationDef>
{
    [AmendsTarget] public ImmutableArray<string> StartingCivics { get; init; } = [];
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
    /// <summary>Divides a purged building's cost into the gold an inquisition pays for it.</summary>
    public int InquisitionBuildingGoldDivisor { get; init; } = 4;
    /// <summary>Gold an inquisition pays for purging a holy city's faith.</summary>
    public int HolyCityRemovalGold { get; init; } = 500;
    /// <summary>Gold an inquisition pays for purging any other faith.</summary>
    public int RemovalGold { get; init; } = 50;
    /// <summary>The roll natural spread and decay draw from, before the game speed's growth percent.</summary>
    public int SpreadRange { get; init; } = 1000;
    /// <summary>Scales the distance to a faith's source against the map's largest distance.</summary>
    public int SpreadDistanceDivisor { get; init; } = 100;
    /// <summary>Subtracted from the scaled distance before it divides the spread.</summary>
    public int SpreadDistanceFloor { get; init; } = 5;
    /// <summary>Added to the number of faiths already present before it divides the spread.</summary>
    public int SpreadCountOffset { get; init; } = 1;
    /// <summary>The least a reachable source adds to the spread threshold.</summary>
    public int SpreadThresholdBump { get; init; } = 1;
    /// <summary>Divides the spread threshold into the chance a city rolls against.</summary>
    public int SpreadThresholdUnit { get; init; } = 4;
    /// <summary>Spread influence of a faith's holy city.</summary>
    public int HolyCityInfluence { get; init; } = 1;
    /// <summary>Turns, at normal speed, a faith counts as young: it spreads faster and decays slower.</summary>
    public int NewSpreadTurns { get; init; } = 50;
    /// <summary>Spread threshold of a young faith, in percent.</summary>
    public int NewSpreadBoostPercent { get; init; } = 250;
    /// <summary>Turns, at normal speed, after which a faith counts as old and decays faster.</summary>
    public int OldSpreadTurns { get; init; } = 250;
    /// <summary>Weighs the faiths of the city's own empire in its decay roll.</summary>
    public int DecayDomesticNumerator { get; init; } = 9;
    /// <summary>Added to that weighing.</summary>
    public int DecayDomesticOffset { get; init; } = 10;
    /// <summary>Divides the decay of a young faith.</summary>
    public int DecayYoungDivisor { get; init; } = 10;
    /// <summary>Multiplies the decay of an old faith, with the denominator below.</summary>
    public int DecayOldNumerator { get; init; } = 4;
    /// <summary>Divides the decay of an old faith.</summary>
    public int DecayOldDenominator { get; init; } = 3;
    /// <summary>Happiness of a city where its owner's state religion is present.</summary>
    public int StateReligionHappiness { get; init; } = 1;
    /// <summary>Happiness of a city per other faith present.</summary>
    public int NonStateReligionHappiness { get; init; } = 0;
    /// <summary>Health of a city where its owner's state religion is present.</summary>
    public int StateReligionHealth { get; init; } = 0;
    /// <summary>Turns, at normal speed, before a player may change state religion again.</summary>
    public int MinimumConversionTurns { get; init; } = 5;
}

/// <summary>What a trait means to religion: whether a non-state faith's buildings keep their commerce.</summary>
public sealed class TraitReligionDef : ContentAttachment<TraitDef>, Ariel.Game.IDescribedContent
{
    /// <summary>The trait's player keeps the commerce of every faith's buildings, not only the state religion's.</summary>
    public bool NonStateReligionCommerce { get; init; }

    public IEnumerable<string> Describe(ContentDb content)
    {
        if (NonStateReligionCommerce) yield return "non-state religion buildings keep their commerce";
    }
}

/// <summary>How a leader regards a player of its own state religion and one of another (each attitude, its growth
/// divisor and its limit), and its weights for the memories of accepting or refusing a faith, added to the leader's
/// memory tables (<see cref="LeaderDef.MemoryAttitudePercents"/>, <see cref="LeaderDef.MemoryDecays"/>), and its
/// favourite civic when that is one of the religion category's, filled into its empty <see cref="LeaderDef.FavoriteCivic"/>.</summary>
public sealed class LeaderReligionDef : ContentAttachment<LeaderDef>
{
    /// <summary>The row of a leader nothing attached to.</summary>
    public static readonly LeaderReligionDef None = new();
    public int SameReligionAttitudeChange { get; init; }
    public int SameReligionAttitudeChangeLimit { get; init; }
    public int SameReligionAttitudeDivisor { get; init; }
    public int DifferentReligionAttitudeChange { get; init; }
    public int DifferentReligionAttitudeChangeLimit { get; init; }
    public int DifferentReligionAttitudeDivisor { get; init; }
    [AmendsTarget] public ImmutableDictionary<string, int> MemoryAttitudePercents { get; init; } = ImmutableDictionary<string, int>.Empty;
    [AmendsTarget] public ImmutableDictionary<string, int> MemoryDecays { get; init; } = ImmutableDictionary<string, int>.Empty;
    [AmendsTarget] public string? FavoriteCivic { get; init; }
}
