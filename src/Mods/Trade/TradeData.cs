using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Trade;

internal static class TradeData
{
    internal static S.RuleConstantsDef Constants(Simulation sim) => sim.Content.Get<S.RuleConstantsDef>().All.Single();
    internal static int Raw(Simulation sim, string key, int fallback) => int.TryParse(Constants(sim).Raw.GetValueOrDefault(key), out int n) ? n : fallback;
    /// <summary>The trade rule constants, attached to the base game's rule constants.</summary>
    internal static RuleConstantsTradeDef Rules(Simulation sim) =>
        sim.Content.Attachment<RuleConstantsTradeDef>(Constants(sim).Id) ?? RuleConstantsTradeDef.None;
    internal static BuildingTradeDef Building(Simulation sim, string building) =>
        sim.Content.Attachment<BuildingTradeDef>(building) ?? BuildingTradeDef.None;
    /// <summary>What the player's civics mean to trade, in civic id order; a civic without an attachment means nothing.</summary>
    internal static IEnumerable<CivicTradeDef> Civics(Simulation sim, int player)
    {
        foreach (var civic in sim.State.PlayerById(player).Civics.Values.Order(StringComparer.Ordinal))
            if (sim.Content.Attachment<CivicTradeDef>(civic) is { } trade) yield return trade;
    }
    internal static IEnumerable<S.BuildingDef> Active(Simulation sim, City city) => CityBuildingRules.Running(sim, city);
    internal static bool Coastal(Simulation sim, City city)
    {
        return sim.State.Grid.Neighbors(sim.State.Grid.TileAt(city.Position)).Any(t =>
        {
            var plot = sim.State.Plots[t.Value];
            return !plot.IsLake && sim.Content.Get<S.TerrainDef>(plot.Terrain).IsWater;
        });
    }
    private static readonly ModifierScope[] EffectScopes = [ModifierScope.City, ModifierScope.Player, ModifierScope.Team];
    /// <summary>The trade lines of the city's, its owner's and its team's calculations, every source but the routes'
    /// own: route output is not an input to its own calculation, which also prevents query recursion. The sums come
    /// from the kept contributions (<see cref="ModifierSink.Collect(ModifierContext, IModifierSource)"/>).</summary>
    internal static ModifierSink Effects(Simulation sim, City city)
    {
        var sink = new ModifierSink();
        var routes = RouteYields(sim);
        var team = sim.TeamOf(city.Owner);
        foreach (var scope in EffectScopes)
        {
            var collected = ModifierSink.Collect(new ModifierContext(sim, scope, city.Owner, city, Team: team), routes);
            foreach (var line in collected.Lines)
                if (line.Key.StartsWith("trade:", StringComparison.Ordinal)) sink.Add(line.Source, line.Key, line.Amount);
        }
        return sink;
    }
    private static IModifierSource RouteYields(Simulation sim)
    {
        foreach (var source in sim.Content.Services.Get<IModifierSource>())
            if (source is TradeEffects) return source;
        throw new InvalidOperationException("The trade module's route yields source is not registered");
    }
    internal static IEnumerable<S.TraitDef> Traits(Simulation sim, int player) =>
        sim.Rules().Peoples?.Traits(sim, player) ?? [];
}
