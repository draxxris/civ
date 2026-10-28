using System.Globalization;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using CivicDef = Ariel.Content.Schema.CivicDef;

namespace Ariel.Mods.Economy;

internal static class EconomyData
{
    internal static RuleConstantsDef Rules(Simulation sim) => sim.Content.Get<RuleConstantsDef>().All.Single();
    internal static int Raw(Simulation sim, string key, int fallback) => Rules(sim).Raw.TryGetValue(key, out var text)
        && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    internal static GameSpeedDef Speed(Simulation sim) => sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed);
    internal static HandicapDef Handicap(Simulation sim) => sim.Content.Get<HandicapDef>(sim.Settings.Handicap);
    internal static IEnumerable<City> Cities(Simulation sim, int player) => sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id);
    internal static CivicDef[] Civics(Simulation sim, int player) => sim.State.PlayerById(player).Civics.Values.Order(StringComparer.Ordinal).Select(sim.Content.Get<CivicDef>).ToArray();
    internal static ModifierSink Effects(Simulation sim, int player) => ModifierSink.Collect(new(sim, ModifierScope.Player, player, Team: sim.TeamOf(player)));
    internal static EconomyPlayerState Read(Simulation sim, int player) => sim.State.PlayerById(player).Extensions.Get<EconomyPlayerState>(sim.Content) ?? new() { Rates = InitialRates(sim) };
    internal static EconomyPlayerState Edit(Simulation sim, int player)
    {
        var state = Read(sim, player); sim.State.PlayerById(player).Extensions.Set(sim.Content, state); return state;
    }
    private static CommerceVector InitialRates(Simulation sim) => new(
        sim.Content.Get<CommerceDef>("foundation:commerce_gold").InitialPercent,
        sim.Content.Get<CommerceDef>("foundation:commerce_research").InitialPercent,
        sim.Content.Get<CommerceDef>("foundation:commerce_culture").InitialPercent,
        sim.Content.Get<CommerceDef>("foundation:commerce_espionage").InitialPercent);
    internal static int EraIndex(Simulation sim, int player)
    {
        string current = sim.State.PlayerById(player).Extensions.Get<CityPlayerState>(sim.Content)?.CurrentEra ?? sim.Settings.StartingEra;
        return sim.Content.Get<EraDef>().All.OrderBy(e => e.StartPercent).ThenBy(e => e.Id, StringComparer.Ordinal).TakeWhile(e => e.Id != current).Count();
    }
    internal static long AiScale(Simulation sim, int player, long value, int percent)
    {
        if (sim.State.PlayerById(player).Ai is null) return value;
        value = value * percent / 100;
        return value * Math.Max(0, 100 + Handicap(sim).AiPerEraModifier * EraIndex(sim, player)) / 100;
    }
}
