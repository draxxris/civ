using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

internal static class ReligionData
{
    /// <summary>The unit AI role that marks a unit as a missionary, kept in one place: a rename must not silently
    /// disable validation in a second file.</summary>
    internal const string MissionaryUnitAi = "military:unitai_missionary";
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
    /// <summary>Buildings whose religion presence requirements <paramref name="removed"/> no longer satisfies, which
    /// is what the production validator would now reject for religion reasons. Wonders are included, as in the source
    /// rules.</summary>
    internal static BuildingDef[] InvalidatedBuildings(Simulation sim, City city, IReadOnlyList<string> removed)
    {
        // Buildings name religions by identity, so the comparison is between identities.
        var remaining = Present(sim, city).Where(r => !removed.Contains(r, StringComparer.Ordinal))
            .Select(r => ReligionIdentities.Identity(sim.Content, r)).ToArray();
        return city.Buildings.Order(StringComparer.Ordinal).Select(sim.Content.Get<BuildingDef>)
            .Where(b => !ReligionSupported(b, remaining)).ToArray();
    }
    /// <summary>Only religion requirements can be broken by a faith leaving; lost resources never destroy buildings,
    /// so every other branch counts as satisfied here. Religion alternatives are honoured: one surviving faith keeps
    /// a building whose requirement is "this religion or that one".</summary>
    private static bool ReligionSupported(BuildingDef building, IReadOnlyList<string> remaining)
    {
        // FreeFromStateReligion only relaxes state matching; the faith itself must still be present.
        if (building.ReligionType is { } type && !remaining.Contains(type, StringComparer.Ordinal)) return false;
        return ReligionSupported(building.Requirements ?? new Ariel.Content.Requirement.All([]), remaining);
    }
    private static bool ReligionSupported(Ariel.Content.Requirement requirement, IReadOnlyList<string> remaining) => requirement switch
    {
        // Only presence leaves can be broken by a faith leaving the city. State-religion (1), holy-city (2) and
        // unknown parameters describe player choices or other states, which the source rules do not purge for either
        // (CvCity.cpp:20736-20744 selects buildings by getPrereqReligion, the presence requirement).
        Ariel.Content.Requirement.Leaf leaf => leaf.Kind != "religion" || leaf.Param is not (null or 0)
            || remaining.Contains(leaf.Id, StringComparer.Ordinal),
        Ariel.Content.Requirement.All all => all.Items.All(item => ReligionSupported(item, remaining)),
        Ariel.Content.Requirement.Any any => any.Items.Any(item => ReligionSupported(item, remaining)),
        Ariel.Content.Requirement.Not not => !ReligionSupported(not.Item, remaining),
        _ => true,
    };
    internal static int? HolyCity(Simulation sim, string religion)
    {
        var founding = Game(sim).Founded.GetValueOrDefault(religion);
        return founding is not null && sim.State.FindCity(founding.HolyCity) is not null ? founding.HolyCity : null;
    }
    private static readonly CivicReligionDef NoRules = new();
    /// <summary>The player's civics in ordinal id order, each with what it means to religion (its
    /// <see cref="CivicReligionDef"/> attachment, or no effect at all without one).</summary>
    internal static IEnumerable<HeldCivic> Civics(Simulation sim, int player) => sim.State.PlayerById(player).Civics.Values
        .Order(StringComparer.Ordinal).Select(id => new HeldCivic(id, sim.Content.Attachment<CivicReligionDef>(id) ?? NoRules));
    internal static string? StateReligion(Simulation sim, int player) => Civics(sim, player).Any(c => c.Rules.RequiresStateReligion) ? Player(sim, player).LastStateReligion : null;
    /// <summary>Buildings the city actually runs, empire grants, replacement and power included.</summary>
    internal static IReadOnlyList<BuildingDef> Active(Simulation sim, City city) => CityBuildingRules.Running(sim, city);
    internal static int Raw(Simulation sim, string key, int fallback) => sim.Content.Get<RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var text) && int.TryParse(text, out var value) ? value : fallback;
    internal static bool BlocksSpread(Simulation sim, City city, string religion) => religion != StateReligion(sim, city.Owner) && Civics(sim, city.Owner).Any(c => c.Rules.NoNonStateReligionSpread);
}

/// <summary>A civic the player holds and what it means to religion.</summary>
internal sealed record HeldCivic(string Id, CivicReligionDef Rules);
