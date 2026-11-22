using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>A corporation is founded by completing its headquarters, one building per corporation in the mod's
/// hand-written <c>data/founding-headquarters.json</c> (<see cref="BuildingDef.FoundsCorporation"/> names the
/// corporation's identity). The building's requirement is the corporation's technology and its world limit is one;
/// this founder adds what founding needs in the city (no founded copy, civics that allow it, a connected resource the
/// corporation consumes, no competing headquarters) and founds it there on completion. The imported headquarters rows
/// of the base game name the same identities but are not claimed, so Cities refuses them.</summary>
internal sealed class CorporationFounder(CorporationService service, IReadOnlyDictionary<string, string> headquarters) : IBuildingFounder
{
    /// <summary>The Corporations mod's founder in this content, or null without the mod.</summary>
    internal static CorporationFounder? Of(ContentDb content) =>
        content.Services.Get<IBuildingFounder>().OfType<CorporationFounder>().SingleOrDefault();

    /// <summary>The headquarters that founds a corporation, or null.</summary>
    internal static BuildingDef? Headquarters(ContentDb content, string corporation) =>
        Of(content)?.HeadquartersOf(corporation) is { } id ? content.Get<BuildingDef>(id) : null;

    private string? HeadquartersOf(string corporation) => headquarters.FirstOrDefault(p => p.Value == corporation).Key;

    /// <summary>The corporation a claimed headquarters founds.</summary>
    internal string? Corporation(BuildingDef building) => headquarters.GetValueOrDefault(building.Id);

    public bool Claims(ContentDb content, BuildingDef building) => headquarters.ContainsKey(building.Id);

    public string? Refuse(Simulation sim, City city, BuildingDef building) =>
        service.RefuseFounding(sim, city, sim.Content.Get<CorporationDef>(headquarters[building.Id]));

    public void Found(Simulation sim, City city, BuildingDef building) =>
        service.Found(sim, city, sim.Content.Get<CorporationDef>(headquarters[building.Id]));
}
