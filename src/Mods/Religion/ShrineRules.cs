using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Religion;

internal sealed partial class ReligionService
{
    public string? ValidateShrine(Simulation sim, int player, int unitId, string building)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Not your unit";
        if (unit.MovesLeft <= 0) return "No moves left";
        var city = sim.State.CityAt(unit.Position);
        if (city is null || city.Owner != player) return "Unit must be in your city";
        if (!sim.Content.TryGet<S.BuildingDef>(building, out var def) || def.GlobalReligionCommerce is null) return "Not a shrine";
        var unitDef = sim.Content.Get<S.UnitDef>(unit.Type);
        if (!unitDef.ConstructibleBuildings.GetValueOrDefault(building)) return "Unit cannot construct this shrine";
        return ShrineEligibility(sim, city, def);
    }
    private string? ShrineEligibility(Simulation sim, City city, S.BuildingDef shrine)
    {
        if (HolyCity(sim, shrine.GlobalReligionCommerce!) != city.Id) return "Requires the holy city";
        // Prophets bypass the negative production cost, not requirements, replacements or class limits.
        var result = sim.Content.Services.Get<ICityProductionService>().Single().Validate(sim, city.Id, "cities:building", shrine.Id);
        return result.Failures.FirstOrDefault(f => f.Explanation != "Building cannot be constructed")?.Explanation;
    }
    public void BuildShrine(Simulation sim, int unitId, string building)
    {
        var unit = sim.State.UnitById(unitId);
        var city = sim.State.CityAt(unit.Position)!;
        city.Buildings.Add(building);
        sim.Events.Publish(new BuildingCompleted(city.Id, building));
        sim.State.RemoveUnit(unitId);
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
    }
}
