using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Religion;

// What base definitions mean to religion, kept in this mod's own rows (see "Content attachments" in docs/DESIGN.md).
// A definition without a row means nothing to religion: read it as None, whose values are all zero or empty.

/// <summary>What a building means to religion: how it draws faiths to its city, what it yields in state religion
/// cities, whether it ignores the state religion, and the faith whose shrine it is.</summary>
public sealed class BuildingReligionDef : ContentAttachment<BuildingDef>
{
    /// <summary>The row of a building nothing attached to.</summary>
    public static readonly BuildingReligionDef None = new();
    /// <summary>Influence the building adds to each named faith's natural spread into its city.</summary>
    [ContentReference(typeof(ReligionIdentityDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ReligionChanges { get; init; } = ImmutableDictionary<string, int>.Empty;
    /// <summary>Commerce the building adds, across its owner's cities, to each building of the state religion.</summary>
    public CommerceVector StateReligionCommerces { get; init; }
    /// <summary>The building needs no matching state religion.</summary>
    public bool FreeFromStateReligion { get; init; }
    /// <summary>Happiness when the building's religion is its owner's state religion.</summary>
    public int StateReligionHappiness { get; init; }
    /// <summary>The faith whose shrine the building is: it earns that faith's commerce for every city holding it.</summary>
    [ContentReference(typeof(ReligionIdentityDef), ReferencePart.Values)]
    public string? GlobalReligionCommerce { get; init; }
}

/// <summary>What a unit means to religion: the chance, per faith, that its spread mission succeeds.</summary>
public sealed class UnitReligionDef : ContentAttachment<UnitDef>
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitReligionDef None = new();
    [ContentReference(typeof(ReligionIdentityDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ReligionSpreads { get; init; } = ImmutableDictionary<string, int>.Empty;
}

/// <summary>The religion rule constants, attached to the base game's <see cref="RuleConstantsDef"/>.</summary>
public sealed class RuleConstantsReligionDef : ContentAttachment<RuleConstantsDef>
{
    /// <summary>The row when nothing attached to the rule constants.</summary>
    public static readonly RuleConstantsReligionDef None = new();
    /// <summary>Anarchy turns of a state religion change, before the player's anarchy modifiers.</summary>
    public int BaseReligionAnarchyTurns { get; init; }
}
