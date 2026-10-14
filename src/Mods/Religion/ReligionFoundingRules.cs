using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Religion;

internal static class ReligionFoundingRules
{
    internal static void Discovered(Simulation sim, TechDiscovered message)
    {
        foreach (var religion in sim.Content.Get<ReligionDef>().All.Where(r => r.FoundingTech == message.Tech).OrderBy(r => r.Id, StringComparer.Ordinal))
            Reserve(sim, religion, message.Team, true);
    }
    private static void Reserve(Simulation sim, ReligionDef religion, int team, bool awardUnits)
    {
        var state = ReligionData.EditGame(sim);
        if (state.Founded.ContainsKey(religion.Id)) return;
        if (state.PendingTeams.TryAdd(religion.Id, team) && awardUnits) state.PendingAwards.Add(religion.Id);
        Found(sim, religion, state.PendingTeams[religion.Id]);
    }
    internal static void CityFounded(Simulation sim, CityFounded message)
    {
        foreach (var religion in sim.Content.Get<ReligionDef>().All.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var state = ReligionData.Game(sim);
            if (state.PendingTeams.TryGetValue(religion.Id, out var team)) Found(sim, religion, team);
            else if (religion.FoundingTech is null || sim.KnowsTech(message.Player, religion.FoundingTech))
                Reserve(sim, religion, sim.TeamOf(message.Player).Id, false);
        }
    }
    private static void Found(Simulation sim, ReligionDef religion, int team)
    {
        var candidates = sim.State.Players.Where(p => p.TeamId == team && p.Ai != "military:barbarians")
            .Where(p => sim.State.Cities.Any(c => c.Owner == p.Id)).OrderBy(p => p.Id).ToArray();
        int best = int.MaxValue;
        Player? owner = null;
        foreach (var player in candidates)
        {
            int presence = sim.State.Cities.Where(c => c.Owner == player.Id).Sum(c => ReligionData.Present(sim, c).Count);
            int score = sim.Rng.Next(10) + presence * 20 + (player.Ai is null ? 0 : 10);
            if (score >= best) continue;
            best = score; owner = player;
        }
        if (owner is null) return;
        var city = HolyCity(sim, owner.Id);
        var state = ReligionData.EditGame(sim);
        state.Founded[religion.Id] = new(city.Id, sim.State.Turn);
        state.PendingTeams.Remove(religion.Id);
        ReligionData.Add(sim, city, religion.Id);
        sim.Events.Publish(new ReligionFounded(religion.Id, city.Id, owner.Id));
        if (state.PendingAwards.Remove(religion.Id)) AwardUnits(sim, owner, city, religion);
    }
    private static void AwardUnits(Simulation sim, Player player, City city, ReligionDef religion)
    {
        if (religion.Missionary is not { } defaultUnit) return;
        var civilization = sim.Content.Get<CivilizationDef>(player.Civilization);
        var unitType = civilization.UnitOverrides.GetValueOrDefault(defaultUnit, defaultUnit);
        if (unitType is null) return;
        for (int i = 0; i < religion.FreeUnits; i++)
        {
            var unit = new Unit { Id = sim.State.NextUnitId++, Owner = player.Id, Type = unitType, Position = city.Position };
            sim.State.AddUnit(unit);
            sim.Events.Publish(new UnitCreated(unit.Id, player.Id, unit.Type, city.Position));
        }
    }
    private static City HolyCity(Simulation sim, int player)
    {
        City? chosen = null;
        int best = 0;
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id))
        {
            int value = (10 + city.Population + sim.Rng.Next(Math.Max(1, ReligionData.Raw(sim, "FOUND_RELIGION_CITY_RAND", 10))))
                / (ReligionData.Present(sim, city).Count + 1);
            if (city.Buildings.Any(b => sim.Content.Get<BuildingDef>(b).Capital)) value /= 8;
            value = Math.Max(1, value);
            if (value <= best) continue;
            best = value; chosen = city;
        }
        return chosen!;
    }
}
