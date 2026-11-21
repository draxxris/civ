using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

// What base definitions mean to corporations, kept in this mod's own rows (see "Content attachments" in
// docs/DESIGN.md). A definition without a row means nothing to corporations: read it as None, whose values are all
// zero or empty.

/// <summary>What a unit means to corporations: the corporations, by identity, it can spread as an executive.</summary>
public sealed class UnitCorporationsDef : ContentAttachment<UnitDef>
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitCorporationsDef None = new();
    [ContentReference(typeof(CorporationIdentityDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CorporationSpreads { get; init; } = ImmutableDictionary<string, int>.Empty;
}

/// <summary>What a handicap means to corporations.</summary>
public sealed class HandicapCorporationsDef : ContentAttachment<HandicapDef>
{
    /// <summary>The row of a handicap nothing attached to.</summary>
    public static readonly HandicapCorporationsDef None = new();
    /// <summary>Percent of each corporation's maintenance a player on this handicap pays.</summary>
    public int CorporationMaintenancePercent { get; init; }
}

/// <summary>What a world size means to corporations.</summary>
public sealed class WorldSizeCorporationsDef : ContentAttachment<WorldSizeDef>
{
    /// <summary>The row of a world size nothing attached to.</summary>
    public static readonly WorldSizeCorporationsDef None = new();
    /// <summary>Percent of the per-resource corporation maintenance on this world size.</summary>
    public int CorporationMaintenancePercent { get; init; }
}
