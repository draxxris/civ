using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Cities;

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
        var state = CityData.Edit(sim, city);
        state.OriginalCivilization = sim.State.PlayerById(player).Civilization;
        state.TurnAcquired = sim.State.Turn;
        state.HighestPopulation = city.Population;
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
    public string? ValidateQueue(Simulation sim, int player, int cityId, string kind, string content, bool append, int? remove)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Owner != player) return "Not your city";
        if (CityData.Read(sim, city).IsPuppet) return "Puppet cities manage their own production";
        if (remove is { } index) return index >= 0 && index < city.ProductionQueue.Count ? null : "Invalid queue index";
        var result = production.Validate(sim, cityId, kind, content);
        if (!result.Success) return string.Join("; ", result.Failures.Select(f => f.Explanation));
        // The queued-copy rule is enforced here as well as in the choice list, so a caller that bypasses the screen
        // cannot queue more copies than the content allows. Reordering has its own command, so an entry already in the
        // queue never has to pass this check again, and a replacement drops this city's queue first, so its entries do
        // not count against the room.
        if (KindOf(sim, kind) is not { } owner) return null;
        return QueueBlocker.Of(sim, owner, city, owner.Definition(sim.Content, content), replace: !append);
    }
    public string? ValidateQueueMove(Simulation sim, int player, int cityId, int from, int to)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Owner != player) return "Not your city";
        if (CityData.Read(sim, city).IsPuppet) return "Puppet cities manage their own production";
        if (from < 0 || from >= city.ProductionQueue.Count || to < 0 || to >= city.ProductionQueue.Count || from == to)
            return "Invalid queue index";
        // Nothing else is checked: a swap moves two entries the queue already holds, so it can neither add an entry nor
        // make one less buildable. Re-checking the entries would refuse every reorder in a city whose queue still holds
        // an entry that has since gone obsolete, which is exactly the entry a player wants to move out of the way.
        return null;
    }

    public void MoveQueue(Simulation sim, int cityId, int from, int to)
    {
        // Swapping the entries keeps each one's progress where it is: a move never changes what is being built.
        var queue = sim.State.CityById(cityId).ProductionQueue;
        (queue[from], queue[to]) = (queue[to], queue[from]);
    }

    private static IProductionKind? KindOf(Simulation sim, string kind) =>
        sim.Content.Services.Get<IProductionKind>().FirstOrDefault(k => k.Id == kind);

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
    public string? ValidateConquestChoice(Simulation sim, int player, int cityId, CityConquestOption option)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Owner != player) return "Not your city";
        var state = CityData.Read(sim, city);
        if (option == CityConquestOption.Annex)
            return state.PendingConquestChoice || state.IsPuppet ? null : "City is already annexed";
        if (!Enum.IsDefined(option)) return "Unknown conquest option";
        if (!state.PendingConquestChoice) return "No pending conquest choice";
        return option == CityConquestOption.Raze
            ? sim.Content.Services.Get<ICityCaptureService>().Single().ValidateRaze(sim, cityId, player) : null;
    }
    public void ResolveConquestChoice(Simulation sim, int player, int cityId, CityConquestOption option)
    {
        if (ValidateConquestChoice(sim, player, cityId, option) is not null) return;
        var city = sim.State.CityById(cityId);
        var state = CityData.Edit(sim, city);
        state.PendingConquestChoice = false;
        if (option == CityConquestOption.Raze)
        {
            sim.Content.Services.Get<ICityCaptureService>().Single().Raze(sim, cityId);
            return;
        }
        state.IsPuppet = option == CityConquestOption.Puppet;
        state.ManualWork = false;
        work.Assign(sim, cityId);
    }
    private static void Pause(CityRuleState state, ProductionOrder order) => state.PausedProduction[order.Kind + "/" + order.ContentId] = Math.Max(state.PausedProduction.GetValueOrDefault(order.Kind + "/" + order.ContentId), order.Progress);
}
