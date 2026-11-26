using Ariel.Game;

namespace Ariel.Rules.Cities;

internal sealed class CityBuildingService : ICityBuildingService
{
    public IReadOnlyList<string> EffectiveBuildings(Simulation sim, int city) =>
        CityData.EffectiveBuildings(sim, sim.State.CityById(city)).Select(b => b.Id).ToArray();
    public IReadOnlyList<string> ActiveBuildings(Simulation sim, int city) =>
        CityData.Active(sim, sim.State.CityById(city)).Select(b => b.Id).ToArray();
    public IReadOnlyList<string> CandidateBuildings(Simulation sim, int city) =>
        CityData.Candidates(sim, sim.State.CityById(city)).ToArray();
    public bool IsPowered(Simulation sim, int city) => CityData.IsPowered(sim, sim.State.CityById(city));
    public bool IsDirtyPower(Simulation sim, int city) => CityData.IsDirtyPower(sim, sim.State.CityById(city));
    public bool ProvidesPower(Simulation sim, int cityId, string building)
    {
        var city = sim.State.CityById(cityId);
        var definition = sim.Content.Get<Ariel.Content.Schema.BuildingDef>(building);
        // Only a building powered by a resource needs the city's resource set, which resolves its running buildings.
        if (definition.AreaCleanPower || definition.ProducesPower) return true;
        if (definition.PowerBonus is null) return false;
        var bonuses = BuildingEffects.Bonuses(sim, city, CityData.ActiveWithoutPower(sim, city));
        return CityData.ProvidesLocalPower(sim, city, definition, bonuses);
    }
}
