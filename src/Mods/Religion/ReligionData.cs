using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

internal static class ReligionData
{
    internal static ReligionGameState Game(Simulation sim) => sim.State.Extensions.Get<ReligionGameState>(sim.Content) ?? new();
    internal static ReligionGameState EditGame(Simulation sim)
    {
        var state = sim.State.Extensions.Get<ReligionGameState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.Extensions.Set(sim.Content, state); return state;
    }
    internal static ReligionPlayerState Player(Simulation sim, int player) => sim.State.PlayerById(player).Extensions.Get<ReligionPlayerState>(sim.Content) ?? new();
    internal static ReligionPlayerState EditPlayer(Simulation sim, int player)
    {
        var owner = sim.State.PlayerById(player);
        var state = owner.Extensions.Get<ReligionPlayerState>(sim.Content);
        if (state is not null) return state;
        state = new(); owner.Extensions.Set(sim.Content, state); return state;
    }
    internal static IReadOnlyList<string> Present(Simulation sim, City city) => city.Extensions.Get<ReligionCityState>(sim.Content)?.Religions.ToArray() ?? [];
    internal static bool Has(Simulation sim, City city, string religion) => city.Extensions.Get<ReligionCityState>(sim.Content)?.Religions.Contains(religion) == true;
    internal static void Add(Simulation sim, City city, string religion)
    {
        var state = city.Extensions.Get<ReligionCityState>(sim.Content);
        if (state is null) { state = new(); city.Extensions.Set(sim.Content, state); }
        if (state.Religions.Add(religion)) sim.Events.Publish(new ReligionSpread(religion, city.Id));
    }
    internal static void Remove(Simulation sim, City city, string religion, string reason)
    {
        if (city.Extensions.Get<ReligionCityState>(sim.Content)?.Religions.Remove(religion) == true)
            sim.Events.Publish(new ReligionRemoved(religion, city.Id, reason));
    }
    internal static BuildingDef[] ReligiousBuildings(Simulation sim, City city, IReadOnlyList<string> religions) =>
        city.Buildings.Order(StringComparer.Ordinal).Select(sim.Content.Get<BuildingDef>)
            .Where(b => b.Limits.World < 0 && b.Limits.Team < 0 && b.Limits.Player < 0)
            .Where(b => RequiresReligion(b, religions)).ToArray();
    private static bool RequiresReligion(BuildingDef building, IReadOnlyList<string> religions) =>
        religions.Contains(building.ReligionType ?? "", StringComparer.Ordinal)
        || Ariel.Content.ContentReferences.Leaves(building.Requirements ?? new Ariel.Content.Requirement.All([]))
            .Any(l => l.Kind == "religion" && religions.Contains(l.Id, StringComparer.Ordinal));
    internal static IEnumerable<CivicDef> Civics(Simulation sim, int player) => sim.State.PlayerById(player).Civics.Values.Order(StringComparer.Ordinal).Select(sim.Content.Get<CivicDef>);
    internal static string? StateReligion(Simulation sim, int player) => Civics(sim, player).Any(c => c.RequiresStateReligion) ? Player(sim, player).LastStateReligion : null;
    internal static IEnumerable<BuildingDef> Active(Simulation sim, City city) => city.Buildings.Order(StringComparer.Ordinal).Select(sim.Content.Get<BuildingDef>).Where(b => Active(sim, city, b));
    private static bool Active(Simulation sim, City city, BuildingDef b)
    {
        if (b.ObsoleteTech is { } obsolete && sim.KnowsTech(city.Owner, obsolete)) return false;
        return !b.RequiresActiveCivics || sim.Content.Requirements.EvaluateKind(b.Requirements, new(sim, city.Owner, city), "civic").Success;
    }
    internal static int Raw(Simulation sim, string key, int fallback) => sim.Content.Get<RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var text) && int.TryParse(text, out var value) ? value : fallback;
    internal static bool BlocksSpread(Simulation sim, City city, string religion) => religion != StateReligion(sim, city.Owner) && Civics(sim, city.Owner).Any(c => c.NoNonStateReligionSpread);
}
