using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Military;

internal static class CombatGenerals
{
    internal static void Award(Simulation sim, Unit winner)
    {
        var service = sim.Content.Services.Get<IGreatPeopleService>().SingleOrDefault();
        if (service is null) return;
        var civ = sim.Content.Get<S.CivilizationDef>(sim.State.PlayerById(winner.Owner).Civilization);
        var general = sim.Content.Get<S.UnitDef>().All.Where(u => u.GreatGeneral
            && civ.UnitOverrides.GetValueOrDefault(u.DefaultUnit, u.DefaultUnit) == u.Id).OrderBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault();
        if (general is null) return;
        var state = MilitaryData.EditPlayer(sim, winner.Owner);
        long threshold = service.GeneralThreshold(sim, winner.Owner);
        while (threshold > 0 && state.GeneralPoints >= threshold)
        {
            var city = BirthCity(sim, winner.Owner);
            var unit = MilitaryData.Spawn(sim, winner.Owner, general.Id, city?.Position ?? winner.Position);
            state.GeneralPoints -= threshold;
            state.GreatGeneralsCreated++;
            state.GeneralThresholdModifier += MilitaryData.Raw(sim, "GREAT_GENERALS_THRESHOLD_INCREASE", 30) * (state.GreatGeneralsCreated / 10 + 1);
            IncreaseTeamThreshold(sim, winner.Owner);
            sim.Events.Publish(new GreatGeneralBorn(winner.Owner, city?.Id ?? -1, unit.Id, general.Id));
            sim.Events.Publish(new GreatPersonBorn(winner.Owner, city?.Id ?? -1, unit.Id, general.DefaultUnit));
            threshold = service.GeneralThreshold(sim, winner.Owner);
        }
    }
    private static void IncreaseTeamThreshold(Simulation sim, int player)
    {
        var team = sim.TeamOf(player).Members;
        int scale = team.Sum(m => MilitaryData.Player(sim, m).GreatGeneralsCreated) / 10 + 1;
        foreach (int member in team)
            MilitaryData.EditPlayer(sim, member).GeneralThresholdModifier += MilitaryData.Raw(sim, "GREAT_GENERALS_THRESHOLD_INCREASE_TEAM", 30) * scale;
    }
    private static City? BirthCity(Simulation sim, int player)
    {
        var cities = sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id).ToArray();
        if (cities.Length == 0) return null;
        var yields = sim.Content.Services.Get<ICityYieldService>().SingleOrDefault();
        var values = cities.Select(c => (City: c, Yield: yields?.Calculate(sim, c.Id).Yields ?? default)).ToArray();
        var ranks = cities.ToDictionary(c => c.Id, _ => 0);
        AddRanks(values.OrderByDescending(v => v.Yield.Food).ThenBy(v => v.City.Id).Select(v => v.City), ranks);
        AddRanks(values.OrderByDescending(v => v.Yield.Production).ThenBy(v => v.City.Id).Select(v => v.City), ranks);
        AddRanks(values.OrderByDescending(v => v.Yield.Commerce).ThenBy(v => v.City.Id).Select(v => v.City), ranks);
        AddRanks(cities.OrderByDescending(c => c.Population).ThenBy(c => c.Id), ranks);
        return cities.Select(c => (City: c, Score: 4 * sim.Rng.Next(cities.Length) + ranks[c.Id]))
            .OrderBy(v => v.Score).ThenBy(v => v.City.Id).First().City;
    }
    private static void AddRanks(IEnumerable<City> cities, Dictionary<int, int> ranks)
    {
        int rank = 1;
        foreach (var city in cities) ranks[city.Id] += rank++;
    }
}
