using Civ.Content;
using Civ.Game;

namespace Civ.Mods.Cities;

internal sealed class CityCulture(ICityYieldService yields, ICityWorkService work)
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
    private long Rate(Simulation sim, City city)
    {
        if (CityData.Read(sim, city).Disorder) return 0;
        var source = sim.Content.Services.Get<ICityCultureSource>().SingleOrDefault();
        if (source is not null) return Math.Max(0, source.CultureHundredths(sim, city.Id));
        var result = yields.Calculate(sim, city.Id);
        return Math.Max(0, result.DirectCommerceHundredths.Culture * Math.Max(1, 100 + result.CommercePercent.Culture) / 100);
    }
    internal void Spread(Simulation sim, City city)
    {
        if (city.Culture <= 0) return;
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
        foreach (var tile in sim.State.Grid.Range(CityData.Tile(sim, city), 1))
        {
            var plot = sim.State.Plots[tile.Value];
            int free = tile == CityData.Tile(sim, city) ? CityData.Constants(sim).FreeCityCulture : CityData.Constants(sim).FreeCityAdjacentCulture;
            plot.Culture[city.Owner] = Math.Max(plot.Culture.GetValueOrDefault(city.Owner), free);
            Resolve(sim, tile);
        }
    }
    internal static void Resolve(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (sim.State.CityAt(sim.State.Grid.HexOf(tile)) is { } city) { plot.Owner = city.Owner; return; }
        long maximum = plot.Culture.Values.DefaultIfEmpty().Max();
        if (maximum <= 0) { plot.Owner = null; return; }
        // Preserve an incumbent on equal culture; otherwise lowest player id breaks ties.
        if (plot.Owner is { } owner && plot.Culture.GetValueOrDefault(owner) == maximum) return;
        plot.Owner = plot.Culture.First(p => p.Value == maximum).Key;
    }
}
