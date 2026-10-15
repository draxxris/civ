using Civ.Content;
using Civ.Game;

namespace Civ.Mods.Cities;

internal sealed class CityCulture(ICityYieldService yields, ICityWorkService work) : ICityCultureService
{
    internal void Accumulate(Simulation sim, City city)
    {
        var state = CityData.Edit(sim, city);
        long rate = Rate(sim, city);
        state.LastCultureRateHundredths = rate;
        long total = state.CultureRemainder + rate;
        city.Culture += total / 100;
        state.CultureRemainder = total % 100;
        state.CultureHundredths[city.Owner] = city.Culture * 100 + state.CultureRemainder;
    }
    public int LevelIndex(Simulation sim, int cityId) => CityData.LevelIndex(sim, sim.State.CityById(cityId));
    public Civ.Content.Schema.CultureLevelDef Level(Simulation sim, int cityId) => CityData.Level(sim, sim.State.CityById(cityId));

    public void AddCulture(Simulation sim, int cityId, long culture, int spreadTurns)
    {
        var city = sim.State.CityById(cityId);
        var state = CityData.Edit(sim, city);
        long savedRate = state.LastCultureRateHundredths;
        int turns = Math.Max(1, spreadTurns);
        long total = Math.Max(0, culture) * 100;
        for (int i = 0; i < turns; i++)
        {
            long amount = total / turns + (i == turns - 1 ? total % turns : 0);
            long stored = state.CultureRemainder + amount;
            city.Culture += stored / 100;
            state.CultureRemainder = stored % 100;
            state.CultureHundredths[city.Owner] = city.Culture * 100 + state.CultureRemainder;
            state.LastCultureRateHundredths = amount;
            Spread(sim, city);
        }
        state.LastCultureRateHundredths = savedRate;
    }
    private long Rate(Simulation sim, City city)
    {
        if (GovernmentStatus.IsDisorder(sim, city)) return 0;
        var source = sim.Content.Services.Get<ICityCultureSource>().SingleOrDefault();
        if (source is not null) return Math.Max(0, source.CultureHundredths(sim, city.Id));
        var result = yields.Calculate(sim, city.Id);
        return Math.Max(0, result.DirectCommerceHundredths.Culture * Math.Max(1, 100 + result.CommercePercent.Culture) / 100);
    }
    internal void Spread(Simulation sim, City city)
    {
        if (city.Culture <= 0 || GovernmentStatus.IsDisorder(sim, city)) return;
        int level = CityData.LevelIndex(sim, city);
        long rate = CityData.Read(sim, city).LastCultureRateHundredths / 100;
        foreach (var tile in sim.State.Grid.Range(CityData.Tile(sim, city), level))
        {
            int distance = sim.State.Grid.Distance(CityData.Tile(sim, city), tile);
            long amount = (level - distance + 1) * CityData.Constants(sim).CityFreeCultureGrowthFactor + rate + 1;
            var plot = sim.State.Plots[tile.Value];
            plot.Culture[city.Owner] = plot.Culture.GetValueOrDefault(city.Owner) + amount;
            Resolve(sim, tile);
        }
        work.Assign(sim, city.Id);
    }
    internal static void Initialize(Simulation sim, City city)
    {
        var center = CityData.Tile(sim, city);
        foreach (var tile in sim.State.Grid.Range(center, 1))
        {
            var plot = sim.State.Plots[tile.Value];
            int free = tile == center ? CityData.Constants(sim).FreeCityCulture : CityData.Constants(sim).FreeCityAdjacentCulture;
            plot.Culture[city.Owner] = Math.Max(plot.Culture.GetValueOrDefault(city.Owner), free);
            Resolve(sim, tile);
        }

        long extraLevels = CityData.Effect(CityData.Effects(sim, city), "extra_culture_level");
        var levels = CityData.Levels(sim, city.Owner, city);
        for (long step = 0; step < extraLevels; step++)
        {
            var next = levels.FirstOrDefault(l => l.SpeedThresholds[sim.Settings.GameSpeed] > city.Culture);
            if (next is null) break;
            long threshold = next.SpeedThresholds[sim.Settings.GameSpeed];
            city.Culture = threshold;
            var state = CityData.Edit(sim, city);
            state.CultureRemainder = 0;
            state.CultureHundredths[city.Owner] = threshold * 100;
        }

        if (extraLevels > 0)
        {
            int radius = CultureRadius(sim, city);
            long growth = CityData.Constants(sim).CityFreeCultureGrowthFactor;
            foreach (var tile in sim.State.Grid.Range(center, radius))
            {
                int distance = sim.State.Grid.Distance(center, tile);
                long amount = (radius - distance + 1) * growth + 1;
                var plot = sim.State.Plots[tile.Value];
                plot.Culture[city.Owner] = plot.Culture.GetValueOrDefault(city.Owner) + amount;
                Resolve(sim, tile);
            }
        }
    }
    internal static void Resolve(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (sim.State.CityAt(sim.State.Grid.HexOf(tile)) is { } city) { plot.Owner = city.Owner; return; }
        // Only players with a living city whose cultural radius covers the tile may own it.
        var eligible = plot.Culture.Where(p => p.Value > 0 && InRange(sim, p.Key, tile)).ToArray();
        if (eligible.Length == 0) { plot.Owner = null; return; }
        long maximum = eligible.Max(p => p.Value);
        // Preserve an incumbent on equal culture; otherwise lowest player id breaks ties.
        if (plot.Owner is { } owner && eligible.Any(p => p.Key == owner && p.Value == maximum)) return;
        plot.Owner = eligible.First(p => p.Value == maximum).Key;
    }
    private static bool InRange(Simulation sim, int player, TileId tile)
    {
        IReadOnlyList<Civ.Content.Schema.CultureLevelDef>? levels = null;
        foreach (var c in sim.State.Cities)
        {
            if (c.Owner != player) continue;
            int distance = sim.State.Grid.Distance(CityData.Tile(sim, c), tile);
            if (distance <= 1) return true;
            levels ??= CityData.Levels(sim);
            if (distance < levels.Count && distance <= Math.Max(1, CityData.LevelIndex(sim, c, levels))) return true;
        }
        return false;
    }
    internal static int CultureRadius(Simulation sim, City city) => Math.Max(1, CityData.LevelIndex(sim, city));
}
