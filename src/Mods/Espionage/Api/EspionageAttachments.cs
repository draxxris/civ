using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Espionage;

/// <summary>What a unit means to espionage, kept in this mod's own rows (see "Content attachments" in
/// docs/DESIGN.md): whether it runs spy missions, and whether it defends its team's cities against them. A unit
/// without a row is neither: read it as <see cref="None"/>.</summary>
public sealed class UnitEspionageDef : ContentAttachment<UnitDef>
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitEspionageDef None = new();
    /// <summary>The unit runs spy missions.</summary>
    public bool Spy { get; init; }
    /// <summary>The unit counts as a defender against spy missions in its team's cities.</summary>
    public bool CounterSpy { get; init; }
}

// The rows below put espionage into other modules' definitions: each field adds to the target's field of the same name
// when content freezes (see "Content amendments" in docs/DESIGN.md), so the base rows name no espionage and every
// reader of a building, civic, trait or specialist sees the espionage it gives while this mod is loaded.

/// <summary>The espionage a building gives: the amounts of the espionage commerce type in its commerce vectors.</summary>
public sealed class BuildingEspionageDef : ContentAttachment<BuildingDef>
{
    [AmendsTarget] public CommerceVector CommerceChanges { get; init; }
    [AmendsTarget] public CommerceVector CommerceModifiers { get; init; }
    [AmendsTarget] public CommerceVector GlobalCommerceModifiers { get; init; }
    [AmendsTarget] public CommerceVector ObsoleteSafeCommerceChanges { get; init; }
    [AmendsTarget] public CommerceVector SpecialistExtraCommerces { get; init; }
    [AmendsTarget] public ImmutableDictionary<string, CommerceVector> GlobalBuildingExtraCommerces { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
    [AmendsTarget] public ImmutableDictionary<string, CommerceVector> SpecialistCommercePercentChanges { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
    [AmendsTarget] public ImmutableDictionary<string, CommerceVector> TechCommerceChanges { get; init; } = ImmutableDictionary<string, CommerceVector>.Empty;
}

/// <summary>The espionage a civic gives: modifiers added to its plain effects.</summary>
public sealed class CivicEspionageDef : ContentAttachment<CivicDef>
{
    [AmendsTarget] public ImmutableArray<ModifierEffect> Effects { get; init; } = [];
}

/// <summary>The espionage a trait gives: the amounts of the espionage commerce type in its commerce vectors.</summary>
public sealed class TraitEspionageDef : ContentAttachment<TraitDef>
{
    [AmendsTarget] public CommerceVector CommerceChanges { get; init; }
    [AmendsTarget] public CommerceVector CommerceModifiers { get; init; }
}

/// <summary>The espionage each specialist of a kind produces.</summary>
public sealed class SpecialistEspionageDef : ContentAttachment<SpecialistDef>
{
    [AmendsTarget] public CommerceVector Commerces { get; init; }
}
