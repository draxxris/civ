using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Corporation rules, imported from the source's corporation records: the industrial and modern
/// corporations and the medieval guilds, which follow the same rules (see <see cref="Seat"/>).
/// <para><see cref="Id"/> is the rules id used by services, effects and saves. <see cref="Identity"/> is the
/// imported corporation id, and it is the id every other content record uses: buildings reference it through
/// <c>corporation</c> requirement leaves, the founding building through <c>FoundsCorporation</c>, executives through
/// <see cref="UnitCorporationsDef.CorporationSpreads"/>. Anything outside the corporation rules therefore keys on the identity, never on
/// <see cref="Id"/>; the rules mod is the only place that reads both.</para>
/// <para>The corporation is founded by completing the base building whose <c>FoundsCorporation</c> names its
/// identity: that building's own requirements (its technology, and for a guild the buildings of its trade) say when
/// it can be founded.</para></summary>
public sealed class CorporationDef : GameContentDef, Ariel.Game.IDescribedContent
{
    [ContentReference(typeof(CorporationIdentityDef))]
    public string Identity { get; init; } = "";
    /// <summary>What this corporation's founding building is called as a kind of building, wherever the player reads
    /// about it: "headquarters" for a corporation, "Master Guild" for a guild. The rules are the same; only the words
    /// differ, and the source's special building type of the founding building picks them. The common building named
    /// Guild Hall is a different building. <see cref="SeatTitle"/> gives the form for a heading.</summary>
    public string Seat { get; init; } = "headquarters";
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
    /// <summary>Corporations, by identity, that compete with this one although they share no consumed resource (the
    /// guilds of one Guild Hall group). Competition is mutual, either list or a shared resource is enough: opening a
    /// branch evicts a competitor's branch from the city, and nobody opens one, or founds one, in a competitor's
    /// headquarters city.</summary>
    [ContentReference(typeof(CorporationIdentityDef), ReferencePart.Values)]
    public ImmutableArray<string> CompetingCorporations { get; init; } = [];
    /// <summary>Happiness every active branch gives its city, whole points; negative is unhappiness.</summary>
    public int Happiness { get; init; }
    /// <summary>Health every active branch gives its city, whole points; negative is unhealthiness.</summary>
    public int Health { get; init; }
    /// <summary>Percent change to the production of military units in a city with an active branch.</summary>
    public int MilitaryProductionPercent { get; init; }
    /// <summary>The technology that ends the corporation for a team that knows it (Corporation, for every guild): its
    /// branches in that team's cities close and stop working, a headquarters there dissolves the corporation, and the
    /// team can neither found it nor open or receive a branch.</summary>
    [ContentReference(typeof(TechDef))]
    public string? ObsoleteTech { get; init; }
    /// <summary>Buildings, by class (the class's default building), and how many of each the owner of a city must hold
    /// across its empire before a branch opens there: a guild's branches need its group's Guild Hall.</summary>
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> PrereqBuildingClasses { get; init; } = ImmutableDictionary<string, int>.Empty;
    public int SpreadCost { get; init; }
    /// <summary>Maintenance per consumed resource copy, hundredths of gold.</summary>
    public int MaintenanceHundredths { get; init; }

    /// <summary>The seat's name as a title: "Headquarters", "Master Guild".</summary>
    public string SeatTitle() => Seat.Length == 0 ? Seat : char.ToUpperInvariant(Seat[0]) + Seat[1..];

    /// <summary>What an active branch does to its city beyond its yields and commerce, for the encyclopedia.</summary>
    public IEnumerable<string> Describe(ContentDb content)
    {
        if (Happiness != 0) yield return $"{Ariel.Presentation.Labels.Signed(Happiness)} happiness in each city with a branch";
        if (Health != 0) yield return $"{Ariel.Presentation.Labels.Signed(Health)} health in each city with a branch";
        if (MilitaryProductionPercent != 0) yield return $"{Ariel.Presentation.Labels.Signed(MilitaryProductionPercent)}% military unit production in each city with a branch";
        foreach (var (building, count) in PrereqBuildingClasses.OrderBy(p => p.Key, StringComparer.Ordinal))
            yield return $"A branch needs {count} {Name(content, building)} in the city owner's empire";
        if (ObsoleteTech is { } tech) yield return $"Obsolete with {Name(content, tech)}";
    }

    private static string Name(ContentDb content, string id) => content.Find(id)?.Name is { Length: > 0 } name ? name : id;
}
