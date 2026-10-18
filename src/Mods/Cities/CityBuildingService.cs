using Civ.Game;

namespace Civ.Mods.Cities;

internal sealed class CityBuildingService : ICityBuildingService
{
    public IReadOnlyList<string> EffectiveBuildings(Simulation sim, int city) =>
        CityData.EffectiveBuildings(sim, sim.State.CityById(city)).Select(b => b.Id).ToArray();
    public IReadOnlyList<string> ActiveBuildings(Simulation sim, int city) =>
        CityData.Active(sim, sim.State.CityById(city)).Select(b => b.Id).ToArray();
    public bool IsPowered(Simulation sim, int city) => CityData.IsPowered(sim, sim.State.CityById(city));
    public bool IsDirtyPower(Simulation sim, int city) => CityData.IsDirtyPower(sim, sim.State.CityById(city));
    public bool ProvidesPower(Simulation sim, int cityId, string building)
    {
        var city = sim.State.CityById(cityId);
        var definition = sim.Content.Get<Civ.Content.Schema.BuildingDef>(building);
        var bonuses = BuildingEffects.Bonuses(sim, city, CityData.ActiveWithoutPower(sim, city));
        return definition.AreaCleanPower || CityData.ProvidesLocalPower(sim, city, definition, bonuses);
    }
}
