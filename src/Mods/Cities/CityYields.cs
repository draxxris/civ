using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityYields : ICityYieldService
{
    public CityYieldResult Calculate(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var sink = CityData.Effects(sim, city);
        var plots = sim.Content.Services.Get<IPlotYieldService>().Single();
        AddPlot(sim, city, CityData.Tile(sim, city), true, plots, sink);
        foreach (var tile in city.Worked.Select(sim.State.Grid.TileAt).OrderBy(t => t.Value)) AddPlot(sim, city, tile, false, plots, sink);
        Specialists.Contribute(sim, city, sink);
        var yields = sink.FlatYield.WithPercent(sink.PercentYield);
        yields = new(Math.Max(0, yields.Food), Math.Max(0, yields.Production), Math.Max(0, yields.Commerce));
        var direct = sink.FlatCommerce * 100 + new CommerceVector(CityData.Effect(sink, "direct_gold_hundredths"),
            CityData.Effect(sink, "direct_research_hundredths"), CityData.Effect(sink, "direct_culture_hundredths"), CityData.Effect(sink, "direct_espionage_hundredths"));
        if (GovernmentStatus.IsDisorder(sim, city)) { yields = default; direct = default; }
        sink.Add("cities:total", "cities:food", yields.Food); sink.Add("cities:total", "cities:production", yields.Production);
        sink.Add("cities:total", "cities:commerce", yields.Commerce);
        sink.Add("cities:total", "cities:gold_hundredths", direct.Gold); sink.Add("cities:total", "cities:research_hundredths", direct.Research);
        sink.Add("cities:total", "cities:culture_hundredths", direct.Culture); sink.Add("cities:total", "cities:espionage_hundredths", direct.Espionage);
        return new(sink.FlatYield, yields, direct, sink.PercentCommerce, sink.Ledger);
    }
    private static void AddPlot(Simulation sim, City city, TileId tile, bool center, IPlotYieldService plots, ModifierSink sink)
    {
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
