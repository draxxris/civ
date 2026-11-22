using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Corporation rules, imported from the source's corporation records: the industrial and modern
/// corporations and the medieval guilds, which follow the same rules (see <see cref="Kind"/>).
/// <para><see cref="Id"/> is the rules id used by services, effects and saves. <see cref="Identity"/> is the
/// imported corporation id, and it is the id every other content record uses: buildings reference it through
/// <c>corporation</c> requirement leaves, the founding building through <c>FoundsCorporation</c>, executives through
/// <see cref="UnitCorporationsDef.CorporationSpreads"/>. Anything outside the corporation rules therefore keys on the identity, never on
/// <see cref="Id"/>; the rules mod is the only place that reads both.</para>
/// <para>The corporation is founded by completing the base building whose <c>FoundsCorporation</c> names its
/// identity: that building's own requirements (its technology, and for a guild the buildings of its trade) say when
/// it can be founded.</para></summary>
public sealed class CorporationDef : GameContentDef
{
    [ContentReference(typeof(CorporationIdentityDef))]
    public string Identity { get; init; } = "";
    /// <summary>A corporation or a guild. The rules are the same; the kind names the founding building, a
    /// headquarters or a guild hall, wherever the player reads about it.</summary>
    public CorporationKind Kind { get; init; }
    /// <summary>The resources a branch consumes. Each connected copy adds the produced yield and commerce and the
    /// per-copy maintenance. Empty for a corporation that consumes none: it needs no resource to be founded or to open
    /// a branch, and its branches give only <see cref="YieldChanges"/> and <see cref="CommerceChanges"/>.</summary>
    [ContentReference(typeof(BonusDef), ReferencePart.Values)]
    public ImmutableArray<string> PrereqBonuses { get; init; } = [];
    /// <summary>Yield per consumed resource copy, in hundredths of a point.</summary>
    public YieldVector YieldProducedHundredths { get; init; }
    /// <summary>Commerce per consumed resource copy, in hundredths of a point.</summary>
    public CommerceVector CommerceProducedHundredths { get; init; }
    /// <summary>Yield every active branch adds, whole points, whatever it consumes.</summary>
    public YieldVector YieldChanges { get; init; }
    /// <summary>Commerce every active branch adds, whole points, whatever it consumes.</summary>
    public CommerceVector CommerceChanges { get; init; }
    /// <summary>Commerce the headquarters city earns for every active branch, whole points.</summary>
    public CommerceVector HeadquartersCommerce { get; init; }
    public int SpreadCost { get; init; }
    /// <summary>Maintenance per consumed resource copy, hundredths of gold.</summary>
    public int MaintenanceHundredths { get; init; }
}

/// <summary>Whether a corporation is a modern corporation or a guild, its earlier-era form.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<CorporationKind>))]
public enum CorporationKind
{
    /// <summary>Founded by building its headquarters.</summary>
    Corporation,
    /// <summary>Founded by building its guild hall.</summary>
    Guild,
}

/// <summary>What each kind of corporation calls its founding building, which is also its seat.</summary>
public static class CorporationKinds
{
    /// <summary>The founding building's common name in lower case, for running text: "the guild hall of Masons Guild".</summary>
    public static string Seat(CorporationKind kind) => kind == CorporationKind.Guild ? "guild hall" : "headquarters";

    /// <summary>The founding building's common name as a title: "Guild Hall", "Headquarters".</summary>
    public static string SeatTitle(CorporationKind kind) => kind == CorporationKind.Guild ? "Guild Hall" : "Headquarters";
}
