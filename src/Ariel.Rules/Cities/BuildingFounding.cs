using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Cities;

/// <summary>The base half of founding by building (<see cref="IBuildingFounder"/>): which founder claims a building,
/// what Cities refuses before any founder is asked, and handing a completed founding building to its founder.</summary>
internal static class BuildingFounding
{
    /// <summary>Whether the building names something it founds, whether or not a loaded feature claims it.</summary>
    internal static bool NamesFounding(S.BuildingDef building) => building.Founds is not null;

    /// <summary>The first registered founder that claims the building, or null.</summary>
    internal static IBuildingFounder? Founder(Simulation sim, S.BuildingDef building)
    {
        foreach (var founder in sim.Rules().Founders)
            if (founder.Claims(sim.Content, building)) return founder;
        return null;
    }

    /// <summary>Why the city may not found by this building now, or null; a building that founds nothing is never
    /// refused here.</summary>
    internal static string? Refuse(Simulation sim, City city, S.BuildingDef building)
    {
        var founder = Founder(sim, building);
        if (founder is null) return NamesFounding(building) ? "Nothing founds what this building would found" : null;
        if (!PlayerRules.IsNation(sim, city.Owner)) return "Barbarians cannot found anything";
        return founder.Refuse(sim, city, building);
    }

    /// <summary>Hands a completed building to the founder that claims it.</summary>
    internal static void Completed(Simulation sim, BuildingCompleted message)
    {
        if (sim.State.FindCity(message.City) is not { } city || !sim.Content.TryGet<S.BuildingDef>(message.Building, out var building)) return;
        Founder(sim, building)?.Found(sim, city, building);
    }
}
