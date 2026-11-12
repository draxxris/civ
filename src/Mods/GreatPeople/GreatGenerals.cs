using System.Globalization;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.GreatPeople;

/// <summary>Great Generals emerge from the general points combat wins earn (<see cref="MilitaryPlayerState"/>). Combat
/// asks right after adding the points, so a birth city's random draw falls inside the fight, as it always has.</summary>
internal sealed class GreatGenerals(IGreatPeopleService service) : ICombatLeaders
{
    public void Emerge(Simulation sim, Unit winner)
    {
        var civ = sim.Content.Get<S.CivilizationDef>(sim.State.PlayerById(winner.Owner).Civilization);
        var general = sim.Content.Get<S.UnitDef>().All.Where(u => u.GreatGeneral
            && civ.UnitOverrides.GetValueOrDefault(u.DefaultUnit, u.DefaultUnit) == u.Id).OrderBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault();
        if (general is null) return;
        var state = EditPlayer(sim, winner.Owner);
        long threshold = service.GeneralThreshold(sim, winner.Owner);
        while (threshold > 0 && state.GeneralPoints >= threshold)
        {
            var city = BirthCity(sim, winner.Owner);
            var unit = Spawn(sim, winner.Owner, general.Id, city?.Position ?? winner.Position);
            state.GeneralPoints -= threshold;
            state.GreatGeneralsCreated++;
            state.GeneralThresholdModifier += Raw(sim, "GREAT_GENERALS_THRESHOLD_INCREASE", 30) * (state.GreatGeneralsCreated / 10 + 1);
            IncreaseTeamThreshold(sim, winner.Owner);
            sim.Events.Publish(new GreatGeneralBorn(winner.Owner, city?.Id ?? -1, unit.Id, general.Id));
            sim.Events.Publish(new GreatPersonBorn(winner.Owner, city?.Id ?? -1, unit.Id, general.DefaultUnit));
            threshold = service.GeneralThreshold(sim, winner.Owner);
        }
    }

    private static void IncreaseTeamThreshold(Simulation sim, int player)
    {
        var team = sim.TeamOf(player).Members;
        int scale = team.Sum(m => Player(sim, m).GreatGeneralsCreated) / 10 + 1;
        foreach (int member in team)
            EditPlayer(sim, member).GeneralThresholdModifier += Raw(sim, "GREAT_GENERALS_THRESHOLD_INCREASE_TEAM", 30) * scale;
    }

    private static City? BirthCity(Simulation sim, int player)
    {
        var cities = sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id).ToArray();
        if (cities.Length == 0) return null;
        var yields = sim.Rules().CityYields;
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

    private static Unit Spawn(Simulation sim, int owner, string type, Hex position)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = position };
        sim.State.AddUnit(unit);
        sim.Events.Publish(new UnitCreated(unit.Id, owner, type, position));
        return unit;
    }

    private static MilitaryPlayerState Player(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.Get<MilitaryPlayerState>(sim.Content) ?? new();

    private static MilitaryPlayerState EditPlayer(Simulation sim, int player)
    {
        var extensions = sim.State.PlayerById(player).Extensions;
        if (extensions.Get<MilitaryPlayerState>(sim.Content) is { } state) return state;
        state = new();
        extensions.Set(sim.Content, state);
        return state;
    }

    private static int Raw(Simulation sim, string key, int fallback) =>
        sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : fallback;
}

/// <summary>Great people settled in a city weigh toward making it the new capital: two points each.</summary>
internal sealed class SettledCapitalWeight(IGreatPeopleService service) : ICapitalWeight
{
    public int Weight(Simulation sim, int city) => service.SettledSpecialists(sim, city).Values.Sum() * 2;
}
