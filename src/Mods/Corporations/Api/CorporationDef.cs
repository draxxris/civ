using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Corporation rules, imported from the source's corporation records: the industrial and modern
/// corporations and the medieval guilds, which follow the same rules (see <see cref="Seat"/>).
/// <para><see cref="Id"/> is the one id of the corporation, in services, effects, saves and content alike: the stores it
/// gates name it in <c>corporation</c> requirement leaves (this type declares that kind), its founding building in
/// <c>Founds</c>, executives in <see cref="UnitCorporationsDef.CorporationSpreads"/>. All of those rows are this mod's
/// own, so the base game names no corporation.</para>
/// <para>The corporation is founded by completing this mod's building whose <c>Founds</c> names it: that building's
/// own requirements (its technology, and for a guild the buildings of its trade) say when it can be founded.</para></summary>
[ContentLeafKind("corporation")]
public sealed class CorporationDef : GameContentDef, Ariel.Game.IDescribedContent
{
    /// <summary>What this corporation's founding building is called as a kind of building, wherever the player reads
    /// about it: "headquarters" for a corporation, "Master Guild" for a guild. The rules are the same; only the words
    /// differ, and the source's special building type of the founding building picks them: a guild is a corporation of
    /// an older era. The common building named Guild Hall is a different building. <see cref="SeatTitle"/> gives the
    /// form for a heading.</summary>
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
    /// <summary>Corporations that compete with this one although they share no consumed resource (the
    /// guilds of one trade group). Competition is mutual, either list or a shared resource is enough: opening a
    /// branch evicts a competitor's branch from the city, and nobody opens one, or founds one, in a competitor's
    /// headquarters city.</summary>
    [ContentReference(typeof(CorporationDef), ReferencePart.Values)]
    public ImmutableArray<string> CompetingCorporations { get; init; } = [];
    /// <summary>The resource a branch produces for its own city: one copy per consumed copy (one when it consumes none),
    /// once the city's team can trade that resource and while it is not obsolete. The copies serve the city like
    /// connected ones (happiness, health, buildings, units, other corporations) but cannot be traded.</summary>
    [ContentReference(typeof(BonusDef))]
    public string? BonusProduced { get; init; }
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
    /// <summary>Base gold to open a branch. What a spread actually costs is <see cref="ICorporationService.SpreadCost"/>:
    /// scaled by inflation, by the foreign spread percent in another team's city, and by each active competitor's
    /// <see cref="SpreadFactorPercent"/>.</summary>
    public int SpreadCost { get; init; }
    /// <summary>Percent this corporation's active branch adds to the spread cost of a competitor opening in its city
    /// (200 for every source corporation: a takeover costs three times as much).</summary>
    public int SpreadFactorPercent { get; init; }
    /// <summary>Maintenance per consumed resource copy, hundredths of gold.</summary>
    public int MaintenanceHundredths { get; init; }
    /// <summary>The source's <c>iSpread</c>: percent of a holding city's influence that reaches out to open branches on
    /// their own with Realistic Guilds/Corporations (<see cref="CorporationMode"/>). Zero would never spread.</summary>
    public int NaturalSpreadPercent { get; init; }
    /// <summary>For a guild that consumes no resource, which therefore has no resource to gate on: buildings, by class
    /// (the class's default building), and how many of each a nation must hold across its empire for the guild to be
    /// founded on its own with Realistic Guilds/Corporations, and to keep its Master Guild. They are the buildings of its
    /// trade its Master Guild needs when built by hand, counted as that building's requirement counts them. Empty for
    /// every corporation that consumes a resource, which its connected resource gates instead.</summary>
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> FoundingBuildingClasses { get; init; } = ImmutableDictionary<string, int>.Empty;

    /// <summary>The seat's name as a title: "Headquarters", "Master Guild".</summary>
    public string SeatTitle() => Seat.Length == 0 ? Seat : char.ToUpperInvariant(Seat[0]) + Seat[1..];

    /// <summary>What an active branch does to its city beyond its yields and commerce, for the encyclopedia.</summary>
    public IEnumerable<string> Describe(ContentDb content)
    {
        if (BonusProduced is { } bonus) yield return $"Produces {Name(content, bonus)} in each city with a branch, one per resource consumed";
        if (Happiness != 0) yield return $"{Ariel.Presentation.Labels.Signed(Happiness)} happiness in each city with a branch";
        if (Health != 0) yield return $"{Ariel.Presentation.Labels.Signed(Health)} health in each city with a branch";
        if (MilitaryProductionPercent != 0) yield return $"{Ariel.Presentation.Labels.Signed(MilitaryProductionPercent)}% military unit production in each city with a branch";
        if (ObsoleteTech is { } tech) yield return $"Obsolete with {Name(content, tech)}";
    }

    private static string Name(ContentDb content, string id) => content.Find(id)?.Name is { Length: > 0 } name ? name : id;
}
