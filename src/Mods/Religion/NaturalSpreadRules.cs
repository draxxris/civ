using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Religion;

internal sealed partial class ReligionService
{
    public ReligionSpreadOdds NaturalSpreadOdds(Simulation sim, int cityId, string religion)
    {
        var city = sim.State.CityById(cityId);
        int range = Math.Max(1, ReligionData.Raw(sim, "RELIGION_SPREAD_RAND", 1000) * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).GrowthPercent / 100);
        if (ReligionData.Has(sim, city, religion) || ReligionData.BlocksSpread(sim, city, religion)) return new(0, range);
        int count = ReligionData.Present(sim, city).Count;
        if (count > 0) return new(0, range);
        var connections = sim.Content.Services.Get<ICityConnectionService>().SingleOrDefault();
        if (connections is null) return new(0, range);
        var def = sim.Content.Get<ReligionDef>(religion);
        int threshold = 0;
        foreach (var source in sim.State.Cities.OrderBy(c => c.Id))
        {
            int spread = Influence(sim, source, religion) * def.SpreadFactor;
            if (spread <= 0) continue;
            if (!connections.Connected(sim, city.Id, source.Id)) continue;
            int distance = sim.State.Grid.Distance(city.Position, source.Position);
            int divisor = Math.Max(1, ReligionData.Raw(sim, "RELIGION_SPREAD_DISTANCE_DIVISOR", 100) * distance / Math.Max(1, sim.State.Grid.MaximumDistance) - 5);
            spread = spread / divisor / (count + 1);
            threshold = Math.Max(threshold, spread + 1);
        }
        int modifier = 100 + ReligionData.Civics(sim, city.Owner).Sum(c => c.ReligionSpreadRate);
        threshold = threshold * Math.Max(0, modifier) / 100;
        threshold = AgeAdjustment(sim, city, religion, threshold);
        return new(threshold / 4, range);
    }
    private int Influence(Simulation sim, City city, string religion)
    {
        int holy = HolyCity(sim, religion) == city.Id ? ReligionData.Raw(sim, "HOLY_CITY_INFLUENCE", 1) : 0;
        return Math.Max(0, holy + ReligionData.Active(sim, city).Sum(b => b.ReligionChanges.GetValueOrDefault(religion)));
    }
    private static int AgeAdjustment(Simulation sim, City city, string religion, int threshold)
    {
        var founded = ReligionData.Game(sim).Founded.GetValueOrDefault(religion);
        if (founded is null) return threshold;
        // Elapsed age, not a negative age: the original subtraction inverted the documented age policy.
        int age = Math.Max(0, sim.State.Turn - founded.Turn);
        int growth = sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).GrowthPercent;
        int young = ReligionData.Raw(sim, "NEW_RELIGION_SPREAD_TURN_THRESHOLD", 50) * growth / 100;
        if (age < young)
        {
            threshold = threshold * ReligionData.Raw(sim, "BOOST_NEW_RELIGION_SPREAD_PERCENT", 250) / 100;
            var era = sim.State.PlayerById(city.Owner).Extensions.Get<CityPlayerState>(sim.Content)?.CurrentEra ?? sim.Settings.StartingEra;
            int rank = sim.Content.Get<EraDef>().All.OrderBy(e => e.StartPercent).ThenBy(e => e.Id, StringComparer.Ordinal).Select(e => e.Id).ToList().IndexOf(era);
            if (rank < 2) threshold /= 20;
            else if (rank == 2) threshold /= 10;
        }
        int old = ReligionData.Raw(sim, "OLD_RELIGION_SPREAD_TURN_THRESHOLD", 250) * growth / 100;
        if (age > old) threshold = (int)((long)threshold * old / age);
        return threshold;
    }
    internal void NaturalSpread(Simulation sim, City city)
    {
        // Baseline profile: ordinary spread stops once a city has a religion. Missionaries can add more.
        if (Present(sim, city.Id).Count > 0) return;
        var founded = ReligionData.Game(sim).Founded;
        foreach (var religion in sim.Content.Get<ReligionDef>().All.Where(r => founded.ContainsKey(r.Id)).OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var odds = NaturalSpreadOdds(sim, city.Id, religion.Id);
            if (odds.Threshold <= 0) continue;
            if (sim.Rng.Next(odds.RandomRange) >= odds.Threshold) continue;
            ReligionData.Add(sim, city, religion.Id);
            break;
        }
    }
}
