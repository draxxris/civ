using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

// What base definitions mean to corporations, kept in this mod's own rows (see "Content attachments" in
// docs/DESIGN.md). A definition without a row means nothing to corporations: read it as None, whose values are all
// zero or empty.

/// <summary>What a unit means to corporations: the corporations, by identity, it can spread as an executive, each with
/// its base percent chance. Executives are not trained with Realistic Guilds/Corporations (<see cref="CorporationMode"/>).</summary>
public sealed class UnitCorporationsDef : ContentAttachment<UnitDef>, Ariel.Game.IDescribedContent
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitCorporationsDef None = new();
    [ContentReference(typeof(CorporationIdentityDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CorporationSpreads { get; init; } = ImmutableDictionary<string, int>.Empty;

    public IEnumerable<string> Describe(ContentDb content)
    {
        var spreads = CorporationSpreads.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
        if (spreads.Length == 0) yield break;
        string Name(string id) => content.Find(id)?.Name is { Length: > 0 } name ? name : id;
        yield return "Spreads " + string.Join(", ", spreads.Select(p => $"{Name(p.Key)} ({p.Value}% chance)"));
        yield return $"Not used with {CorporationMode.OptionName(content)}: guilds and corporations spread on their own";
    }
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
