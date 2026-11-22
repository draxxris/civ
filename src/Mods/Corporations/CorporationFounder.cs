using System.Runtime.CompilerServices;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>A corporation or guild is founded by completing its founding building: the base building whose
/// <see cref="BuildingDef.FoundsCorporation"/> names the corporation's identity, its headquarters or (for a guild) its
/// Master Guild. The building's own requirements say when it can be built (its technology, and for a guild a number of
/// buildings of its trade) and its world limit is one; this founder adds what founding needs in the city (no founded
/// copy, civics that allow it, a connected resource when the corporation consumes any, no competing headquarters)
/// and founds it there on completion.</summary>
internal sealed class CorporationFounder(CorporationService service) : IBuildingFounder
{
    /// <summary>The building that founds the corporation (by rules id), or null when no building names it.</summary>
    internal static BuildingDef? Headquarters(ContentDb content, string corporation) =>
        CorporationIndex.Of(content).FoundingBuildings.GetValueOrDefault(corporation);

    /// <summary>The corporation a building founds, or null when it founds none of this mod's corporations.</summary>
    internal static CorporationDef? Corporation(ContentDb content, BuildingDef building) =>
        CorporationIndex.Of(content).ByIdentity.GetValueOrDefault(building.FoundsCorporation ?? "");

    public bool Claims(ContentDb content, BuildingDef building) => Corporation(content, building) is not null;

    public string? Refuse(Simulation sim, City city, BuildingDef building) =>
        service.RefuseFounding(sim, city, Corporation(sim.Content, building)!);

    public void Found(Simulation sim, City city, BuildingDef building) =>
        service.Found(sim, city, Corporation(sim.Content, building)!);
}

/// <summary>The corporations by identity and the building that founds each, built once per frozen content. Every
/// question the rules, the AI and the client ask about which building founds what reads it, so they cannot disagree.
/// <see cref="CorporationContentValidation.FoundingBuildings"/> refuses content where a corporation has no founding
/// building or more than one, so <see cref="FoundingBuildings"/> names exactly one per corporation in loaded content.</summary>
internal sealed class CorporationIndex
{
    private static readonly ConditionalWeakTable<ContentDb, CorporationIndex> Cache = new();

    /// <summary>Each corporation by its identity id.</summary>
    public IReadOnlyDictionary<string, CorporationDef> ByIdentity { get; }
    /// <summary>The founding building of each corporation, by the corporation's rules id; the first by id when content
    /// names more than one (validation reports that).</summary>
    public IReadOnlyDictionary<string, BuildingDef> FoundingBuildings { get; }

    private CorporationIndex(ContentDb content)
    {
        var byIdentity = new Dictionary<string, CorporationDef>(StringComparer.Ordinal);
        foreach (var corporation in content.Get<CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal))
            byIdentity.TryAdd(corporation.Identity, corporation);
        ByIdentity = byIdentity;
        var buildings = new Dictionary<string, BuildingDef>(StringComparer.Ordinal);
        foreach (var building in content.Get<BuildingDef>().All.OrderBy(b => b.Id, StringComparer.Ordinal))
            if (building.FoundsCorporation is { } identity && byIdentity.TryGetValue(identity, out var corporation))
                buildings.TryAdd(corporation.Id, building);
        FoundingBuildings = buildings;
    }

    public static CorporationIndex Of(ContentDb content) => Cache.GetValue(content, c => new CorporationIndex(c));
}
