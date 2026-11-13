using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using BuildingDef = Ariel.Content.Schema.BuildingDef;

namespace Ariel.Mods.Religion;

/// <summary>The religion rules' answer to the base game's questions (<see cref="ICityReligions"/>). Base content names
/// religions by identity, so every identity is translated to the religion it stands for first.</summary>
internal sealed class CityReligions(ReligionService service) : ICityReligions
{
    public bool Has(Simulation sim, int city, string identity) =>
        ReligionIdentities.Religion(sim.Content, identity) is { } religion && sim.State.FindCity(city) is { } target
        && ReligionData.Has(sim, target, religion);

    public bool IsStateReligion(Simulation sim, int player, string identity) =>
        service.StateReligion(sim, player) is { } state && ReligionIdentities.Identity(sim.Content, state) == identity;

    public bool IsHolyCityOf(Simulation sim, int city, string identity) =>
        ReligionIdentities.Religion(sim.Content, identity) is { } religion && service.HolyCity(sim, religion) == city;

    public bool IsHolyCity(Simulation sim, int city) =>
        sim.Content.Get<ReligionDef>().All.Any(religion => service.HolyCity(sim, religion.Id) == city);

    public bool HasAnyReligion(Simulation sim, int city) => service.Present(sim, city).Count > 0;

    public bool HasStateReligion(Simulation sim, int player, int city) =>
        service.StateReligion(sim, player) is { } state && sim.State.FindCity(city) is { } target && ReligionData.Has(sim, target, state);

    public string? RefuseBuilding(Simulation sim, City city, BuildingDef building)
    {
        if (ReligionIdentities.Religion(sim.Content, building.FoundsReligion) is { } founds)
        {
            if (PlayerRules.IsBarbarian(sim, city.Owner)) return "Barbarians cannot found a religion";
            if (ReligionData.Game(sim).Founded.ContainsKey(founds)) return "Religion is already founded";
        }
        if (building.FreeFromStateReligion) return null;
        var state = service.StateReligion(sim, city.Owner);
        // A building that requires a state religion cannot be built without one, even when it names no religion.
        if (state is null) return building.RequiresStateReligion ? "Requires an active state religion" : null;
        if (ReligionData.Civics(sim, city.Owner).Any(c => c.Rules.FreeReligion)) return null;
        var required = ContentReferences.Leaves(building.Requirements ?? new Requirement.All([]))
            .FirstOrDefault(l => l.Kind == "religion" && l.Param is null);
        return required is null || required.Id == ReligionIdentities.Identity(sim.Content, state) ? null
            : "Requires matching state religion: " + required.Id;
    }

    public string? RefuseUnit(Simulation sim, City city, UnitDef unit) =>
        unit.Inquisitor && !service.IsInquisitionEnabled(sim, city.Owner) ? "Civics and state religion do not allow inquisitions" : null;
}

/// <summary>Followers of a religion remember who razed its holy city. The founding record keeps the holy city's id after
/// the city is gone, so the religions are read from it.</summary>
internal static class HolyCityRazing
{
    internal static void Razed(Simulation sim, CityRazed razed)
    {
        var founded = ReligionData.Game(sim).Founded;
        var relations = sim.Rules().Relations;
        foreach (var religion in sim.Content.Get<ReligionDef>().All.Where(r => founded.GetValueOrDefault(r.Id)?.HolyCity == razed.City))
            foreach (var player in sim.State.Players.OrderBy(p => p.Id).Where(p => ReligionData.StateReligion(sim, p.Id) == religion.Id))
                relations.Remember(sim, player.Id, razed.Player, ReligionMemories.RazedHolyCity);
    }
}
