using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal static class CityConquestBuildings
{
    internal static void Transfer(Simulation sim, City source, City target)
    {
        bool recapture = IsRecapture(sim, target);
        foreach (var id in source.Buildings.Order(StringComparer.Ordinal))
        {
            var original = sim.Content.Get<S.BuildingDef>(id);
            if (original.Capital || original.NeverCapture) continue;
            string? selected = IsWorldWonder(original) ? id : CityData.ResolveBuilding(sim, target, original.DefaultBuilding);
            if (selected is null || !sim.Content.TryGet<S.BuildingDef>(selected, out var building)) continue;
            if (!CanTransfer(sim, target, building)) continue;
            if (Survives(sim, building, recapture)) target.Buildings.Add(selected);
        }
    }
    private static bool CanTransfer(Simulation sim, City city, S.BuildingDef building) =>
        !city.Buildings.Contains(building.Id) && HasLocation(sim, city, building) && WithinLimits(sim, city, building);
    private static bool IsWorldWonder(S.BuildingDef building) => building.Limits.World >= 0 && !building.Limits.NoLimit;
    private static bool IsRecapture(Simulation sim, City city)
    {
        var culture = sim.State.PlotAt(city.Position).Culture;
        if (culture.Count == 0) return false;
        long highest = culture.Values.Max();
        var leaders = culture.Where(p => p.Value == highest).ToArray();
        return highest > 0 && leaders.Length == 1 && sim.TeamOf(leaders[0].Key).Id == sim.TeamOf(city.Owner).Id;
    }
    private static bool Survives(Simulation sim, S.BuildingDef building, bool recapture)
    {
        if (recapture || building.CaptureSurvivalPercent >= 100) return true;
        return building.CaptureSurvivalPercent > 0 && sim.Rng.Next(100) < building.CaptureSurvivalPercent;
    }
    private static bool WithinLimits(Simulation sim, City city, S.BuildingDef building)
    {
        var limits = building.Limits;
        if (limits.NoLimit) return true;
        var owners = sim.State.Cities.SelectMany(c => c.Buildings
            .Where(id => sim.Content.Get<S.BuildingDef>(id).DefaultBuilding == building.DefaultBuilding).Select(_ => c.Owner)).ToArray();
        if (limits.World >= 0 && owners.Length >= limits.World) return false;
        if (limits.Team >= 0 && owners.Count(p => sim.TeamOf(p).Id == sim.TeamOf(city.Owner).Id) >= limits.Team) return false;
        int playerLimit = limits.Player < 0 ? -1 : limits.Player + limits.ExtraPlayer;
        return playerLimit < 0 || owners.Count(p => p == city.Owner) < playerLimit;
    }
    private static bool HasLocation(Simulation sim, City city, S.BuildingDef building)
    {
        bool coast = building.CoastalLocation && CityData.Coastal(sim, city, building.MinAreaSize);
        bool river = building.RiverLocation && sim.State.PlotAt(city.Position).RiverEdges != 0;
        if ((building.CoastalLocation || building.RiverLocation) && !coast && !river) return false;
        int latitude = Math.Abs(2 * (CityData.Tile(sim, city).Value / sim.State.Map.Width) - (sim.State.Map.Height - 1)) * 90 / Math.Max(1, sim.State.Map.Height - 1);
        return latitude >= building.MinLatitude && latitude <= building.MaxLatitude;
    }
}
