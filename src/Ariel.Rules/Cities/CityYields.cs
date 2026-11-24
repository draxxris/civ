using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Cities;

internal sealed class CityYields : ICityYieldService
{
    public CityYieldResult Calculate(Simulation sim, int cityId) => CityReadScope.Run(sim, _ => CalculateScoped(sim, cityId));
    private CityYieldResult CalculateScoped(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var sink = CityData.Effects(sim, city);
        var plots = sim.Rules().PlotYields;
        AddPlot(sim, city, CityData.Tile(sim, city), true, plots, sink);
        foreach (var tile in city.Worked) AddPlot(sim, city, tile, false, plots, sink);
        Specialists.Contribute(sim, city, sink);
        var yields = sink.FlatYield.WithPercent(sink.PercentYield);
        yields = new(Math.Max(0, yields.Food), Math.Max(0, yields.Production), Math.Max(0, yields.Commerce));
        var commerce = sim.Rules().Commerce;
        var directKeys = commerce.Names("cities:direct_", "_hundredths");
        var direct = sink.FlatCommerce * 100;
        for (int i = 0; i < directKeys.Length; i++) direct = direct.Plus(commerce.Types[i].Key, sink.Total(directKeys[i]));
        if (GovernmentStatus.IsDisorder(sim, city)) { yields = default; direct = commerce.Zero; }
        sink.Add("cities:total", "cities:food", yields.Food); sink.Add("cities:total", "cities:production", yields.Production);
        sink.Add("cities:total", "cities:commerce", yields.Commerce);
        var totalKeys = commerce.Names("cities:", "_hundredths");
        for (int i = 0; i < totalKeys.Length; i++) sink.Add("cities:total", totalKeys[i], direct[commerce.Types[i]]);
        return new(sink.FlatYield, yields, direct, sink.PercentCommerce, sink.Ledger);
    }
    private static void AddPlot(Simulation sim, City city, TileId tile, bool center, IPlotYieldService plots, ModifierSink sink)
    {
        if (!center && sim.Rules().CityConnections.IsTileBlockaded(sim, tile, sim.TeamOf(city.Owner).Id)) return;
        var yield = plots.Calculate(sim, city.Owner, tile).Yields;
        if (center) yield = Center(sim, city, yield);
        sink.AddYield("cities:plot:" + tile.Value, yield);
        var plot = sim.State.Plots[tile.Value];
        foreach (var b in CityData.Active(sim, city))
        {
            if (sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater) sink.AddYield(b.Id + ":sea", b.SeaPlotYieldChanges);
            if (plot.RiverEdges != 0) sink.AddYield(b.Id + ":river", b.RiverPlotYieldChanges);
        }
        if (!sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater) return;
        foreach (var other in CityData.Owned(sim, city.Owner))
            foreach (var b in CityData.Active(sim, other)) sink.AddYield(b.Id + ":global_sea", b.GlobalSeaPlotYieldChanges);
    }
    private static YieldVector Center(Simulation sim, City city, YieldVector natural)
    {
        long Minimum(string id, long value)
        {
            var def = sim.Content.Get<S.YieldDef>("foundation:yield_" + id);
            long population = def.PopulationChangeDivisor == 0 ? 0 : (city.Population + def.PopulationChangeOffset) / def.PopulationChangeDivisor;
            return Math.Max(def.MinCity, value + def.CityChange + population);
        }
        return new(Minimum("food", natural.Food), Minimum("production", natural.Production), Minimum("commerce", natural.Commerce));
    }
}
