using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Trade;

internal static class TradeData
{
    internal static S.RuleConstantsDef Constants(Simulation sim) => sim.Content.Get<S.RuleConstantsDef>().All.Single();
    internal static int Raw(Simulation sim, string key, int fallback) => int.TryParse(Constants(sim).Raw.GetValueOrDefault(key), out int n) ? n : fallback;
    internal static IEnumerable<S.CivicDef> Civics(Simulation sim, int player) => sim.State.PlayerById(player).Civics.Values.Order(StringComparer.Ordinal).Select(sim.Content.Get<S.CivicDef>);
    internal static IEnumerable<S.BuildingDef> Active(Simulation sim, City city) => city.Buildings.Order(StringComparer.Ordinal)
        .Select(sim.Content.Get<S.BuildingDef>).Where(b => Active(sim, city, b));
    private static bool Active(Simulation sim, City city, S.BuildingDef b)
    {
        if (b.ObsoleteTech is { } tech && sim.KnowsTech(city.Owner, tech)) return false;
        return !b.RequiresActiveCivics || sim.Content.Requirements.EvaluateKind(b.Requirements, new(sim, city.Owner, city), "civic").Success;
    }
    internal static bool Coastal(Simulation sim, City city)
    {
        return sim.State.Grid.Neighbors(sim.State.Grid.TileAt(city.Position)).Any(t =>
        {
            var plot = sim.State.Plots[t.Value];
            return !plot.IsLake && sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater;
        });
    }
    internal static ModifierSink Effects(Simulation sim, City city)
    {
        var sink = new ModifierSink();
        foreach (var scope in new[] { ModifierScope.City, ModifierScope.Player, ModifierScope.Team })
        {
            var collected = new ModifierSink();
            var context = new ModifierContext(sim, scope, city.Owner, city, Team: sim.TeamOf(city.Owner));
            // Route output is not an input to its own calculation. This also prevents query recursion.
            foreach (var source in sim.Content.Services.Get<IModifierSource>().Where(s => s is not TradeEffects)) source.Contribute(context, collected);
            foreach (var line in collected.Ledger.Lines.Where(l => l.Key.StartsWith("trade:", StringComparison.Ordinal)))
                sink.Add(line.Source, line.Key, line.Amount);
        }
        return sink;
    }
    internal static IEnumerable<S.TraitDef> Traits(Simulation sim, int player) =>
        sim.Content.Services.Get<IPeoplesService>().SingleOrDefault()?.Traits(sim, player) ?? [];
}
