using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.GreatPeople;

internal static class GreatPeopleData
{
    internal static GreatPeopleCityState Read(Simulation sim, City city) => city.Extensions.Get<GreatPeopleCityState>(sim.Content) ?? new();
    internal static GreatPeopleCityState Edit(Simulation sim, City city)
    {
        var state = Read(sim, city);
        city.Extensions.Set(sim.Content, state);
        return state;
    }
    internal static GreatPeoplePlayerState Read(Simulation sim, int player) => sim.State.PlayerById(player).Extensions.Get<GreatPeoplePlayerState>(sim.Content) ?? new();
    internal static GreatPeoplePlayerState Edit(Simulation sim, int player)
    {
        var state = Read(sim, player);
        sim.State.PlayerById(player).Extensions.Set(sim.Content, state);
        return state;
    }
    internal static S.RuleConstantsDef Constants(Simulation sim) => sim.Content.Get<S.RuleConstantsDef>().All.Single();
    internal static int Raw(Simulation sim, string key, int fallback) => int.TryParse(Constants(sim).Raw.GetValueOrDefault(key), out int n) ? n : fallback;
    internal static S.GameSpeedDef Speed(Simulation sim) => sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed);
    internal static IEnumerable<S.BuildingDef> Active(Simulation sim, City city) => city.Buildings.Order(StringComparer.Ordinal)
        .Select(sim.Content.Get<S.BuildingDef>).Where(b => Active(sim, city, b));
    private static bool Active(Simulation sim, City city, S.BuildingDef b)
    {
        if (b.ObsoleteTech is { } tech && sim.KnowsTech(city.Owner, tech)) return false;
        return !b.RequiresActiveCivics || sim.Content.Requirements.EvaluateKind(b.Requirements, new(sim, city.Owner, city), "civic").Success;
    }
    internal static ModifierSink Effects(Simulation sim, ModifierScope scope, int player, City? city = null) =>
        ModifierSink.Collect(new(sim, scope, player, city, Team: sim.TeamOf(player)));
    internal static string? ResolveUnit(Simulation sim, int player, string unitClass)
    {
        if (sim.Content.TryGet<S.CivilizationDef>(sim.State.PlayerById(player).Civilization, out var civ)
            && civ.UnitOverrides.TryGetValue(unitClass, out var replacement)) return replacement;
        return sim.Content.TryGet<S.UnitDef>(unitClass, out var unit) ? unit.Id : null;
    }
}
