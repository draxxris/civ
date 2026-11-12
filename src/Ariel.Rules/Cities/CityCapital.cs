using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Cities;

internal static class CityCapital
{
    internal static bool IsCapital(Simulation sim, City city) => city.Buildings.Any(id => sim.Content.Get<S.BuildingDef>(id).Capital);
    internal static void RelocateIfNeeded(Simulation sim, int player)
    {
        var remaining = CityData.Owned(sim, player).ToArray();
        if (remaining.Length == 0 || remaining.Any(c => IsCapital(sim, c))) return;
        var best = remaining.OrderByDescending(c => Score(sim, c)).ThenBy(c => c.Id).First();
        var palace = sim.Content.Get<S.BuildingDef>().All.Where(b => b.Capital).OrderBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
        if (palace is null) return;
        var civilization = sim.Content.Get<S.CivilizationDef>(sim.State.PlayerById(player).Civilization);
        var id = civilization.BuildingOverrides.GetValueOrDefault(palace.DefaultBuilding, palace.Id);
        if (id is not null && !best.Buildings.Contains(id)) best.Buildings.Add(id);
    }
    private static long Score(Simulation sim, City city)
    {
        var yields = sim.Rules().CityYields.Calculate(sim, city.Id).Yields;
        int religions = sim.Rules().Religion?.Present(sim, city.Id).Count ?? 0;
        int corporations = sim.Rules().CityCorporations?.Count(sim, city.Id) ?? 0;
        int settled = sim.Rules().GreatPeople?.SettledSpecialists(sim, city.Id).Values.Sum() ?? 0;
        long value = city.Population * 4L + yields.Food + yields.Production * 3L + yields.Commerce * 2L
            + CityData.LevelIndex(sim, city) + religions + corporations + settled * 2L;
        return value * (CityData.OwnerCulturePercent(sim, city) + 100L) / 100L;
    }
}
