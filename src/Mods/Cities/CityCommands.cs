using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityCommands(ICityProductionService production, ICityWorkService work) : ICityCommandService
{
    public string? ValidateFound(Simulation sim, int player, int settler)
    {
        var unit = sim.State.FindUnit(settler);
        if (unit is null || unit.Owner != player) return "Not your settler";
        if (!sim.Content.Get<S.UnitDef>(unit.Type).CanFoundCity) return "Unit cannot found a city";
        var plot = sim.State.PlotAt(unit.Position);
        if (plot.Owner is { } owner && owner != player) return "Foreign territory";
        if (!ValidTerrain(sim, sim.State.Grid.TileAt(unit.Position))) return "Invalid founding terrain";
        int minimum = CityData.Constants(sim).MinCityRange;
        if (sim.State.Cities.Any(c => sim.State.Grid.Distance(CityData.Tile(sim, c), sim.State.Grid.TileAt(unit.Position)) <= minimum)) return "City is too close to another city";
        return null;
    }
    private static bool ValidTerrain(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        if (terrain.IsWater || terrain.Impassable || plot.Kind == PlotKind.Peak) return false;
        if (plot.Feature is { } feature && sim.Content.Get<S.FeatureDef>(feature).NoCity) return false;
        if (terrain.CanFoundCity) return true;
        if (terrain.FoundCoast && CityData.Coastal(sim, tile)) return true;
        return terrain.FoundFreshWater && sim.Content.Services.Get<IWaterService>().Single().IsFreshWater(sim, tile);
    }
    public void Found(Simulation sim, int player, int settler)
    {
        var unit = sim.State.UnitById(settler);
        var civilization = sim.Content.Get<S.CivilizationDef>(sim.State.PlayerById(player).Civilization);
        bool first = !CityData.Owned(sim, player).Any();
        var city = new City { Id = sim.State.NextCityId++, Owner = player, Position = unit.Position,
            Name = Name(sim, civilization), Population = CityData.Raw(sim, "INITIAL_CITY_POPULATION", 1) + CityData.Era(sim, player).FreePopulation };
        CityData.Edit(sim, city);
        sim.State.AddCity(city); sim.State.RemoveUnit(settler);
        var plot = sim.State.PlotAt(city.Position);
        plot.Improvement = null;
        if (plot.Feature != "world:flood_plains") plot.Feature = null;
        if (first) GrantBuildings(sim, city, civilization);
        CityCulture.Initialize(sim, city);
        work.Assign(sim, city.Id);
        sim.Events.Publish(new CityFounded(city.Id, player, city.Position));
    }
    private static string Name(Simulation sim, S.CivilizationDef civilization)
    {
        var used = sim.State.Cities.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var name in civilization.CityNames) if (!used.Contains(name)) return name;
        return civilization.ShortDescription + " " + sim.State.NextCityId;
    }
    private static void GrantBuildings(Simulation sim, City city, S.CivilizationDef civilization)
    {
        foreach (var pair in civilization.StartingBuildings.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!pair.Value) continue;
            var id = civilization.BuildingOverrides.GetValueOrDefault(pair.Key, pair.Key);
            if (id is not null) city.Buildings.Add(id);
        }
        if (city.Buildings.Any(b => sim.Content.Get<S.BuildingDef>(b).Capital)) return;
        var palace = sim.Content.Get<S.BuildingDef>().All.FirstOrDefault(b => b.Capital);
        if (palace is not null)
        {
            var id = civilization.BuildingOverrides.GetValueOrDefault(palace.DefaultBuilding, palace.Id);
            if (id is not null && !city.Buildings.Contains(id)) city.Buildings.Add(id);
        }
    }
    public string? ValidateQueue(Simulation sim, int player, int cityId, string kind, string content, int? remove)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Owner != player) return "Not your city";
        if (remove is { } index) return index >= 0 && index < city.ProductionQueue.Count ? null : "Invalid queue index";
        var result = production.Validate(sim, cityId, kind, content);
        return result.Success ? null : string.Join("; ", result.Failures.Select(f => f.Explanation));
    }
    public void Queue(Simulation sim, int cityId, string kind, string content, bool append, int? remove)
    {
        var city = sim.State.CityById(cityId);
        var state = CityData.Edit(sim, city);
        if (remove is { } index)
        {
            Pause(state, city.ProductionQueue[index]); city.ProductionQueue.RemoveAt(index); return;
        }
        if (!append)
        {
            foreach (var old in city.ProductionQueue) Pause(state, old);
            city.ProductionQueue.Clear();
        }
        string key = kind + "/" + content;
        long progress = state.PausedProduction.GetValueOrDefault(key);
        state.PausedProduction.Remove(key);
        city.ProductionQueue.Add(new(kind, content, progress));
    }
    private static void Pause(CityRuleState state, ProductionOrder order) => state.PausedProduction[order.Kind + "/" + order.ContentId] = Math.Max(state.PausedProduction.GetValueOrDefault(order.Kind + "/" + order.ContentId), order.Progress);
}
