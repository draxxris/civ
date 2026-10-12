using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Guilds;

/// <summary>
/// Guilds as a plain mod on the public API. Renown is a city-scoped resource: each city earns and keeps its own from
/// engineers, artists, workshops and guildhalls, and spends it there (guilds, journeymen, masterwork forges, Huscarls);
/// guild techs draw it from all your cities. What each guild yields is the mod's own sidecar schema, read in code.
/// </summary>
public sealed class GuildsMod : IMod
{
    public const string Renown = "guilds:renown";
    public const string Category = "guild";

    public void Register(IModContext ctx)
    {
        foreach (var r in ctx.Data.ReadJson<ResourceDef[]>("data/resources.json")) ctx.Resources.Register(r);
        foreach (var t in ctx.Data.ReadJson<TechDef[]>("data/techs.json")) ctx.Techs.Register(t);
        foreach (var b in ctx.Data.ReadJson<BuildingDef[]>("data/buildings.json")) ctx.Buildings.Register(b);
        foreach (var u in ctx.Data.ReadJson<UnitDef[]>("data/units.json")) ctx.Units.Register(u);
        foreach (var g in ctx.Data.ReadJson<InstitutionDef[]>("data/guilds.json")) ctx.Institutions.Register(g);

        var effects = ctx.Data.ReadJson<GuildEffect[]>("data/guild_effects.json");
        foreach (var e in effects)
            if (!ctx.Institutions.Contains(e.Guild)) throw new InvalidDataException($"guild_effects.json names unknown guild '{e.Guild}'");
        ctx.YieldSources.Register(new GuildYields(effects));
    }
}

/// <summary>What a guild gives every member city, plus a bonus in the city that founded it.</summary>
public sealed class GuildEffect
{
    public string Guild { get; init; } = "";
    public int Food { get; init; }
    public int Production { get; init; }
    public int Commerce { get; init; }
    public int Culture { get; init; }
    public int HomeProduction { get; init; }
    public int HomeCulture { get; init; }
}

public sealed class GuildYields(IReadOnlyList<GuildEffect> effects) : ICityYieldSource
{
    private readonly Dictionary<string, GuildEffect> _byGuild = effects.ToDictionary(e => e.Guild, StringComparer.Ordinal);

    public string Id => "guilds:yields";

    public CityYield YieldFor(Simulation sim, City city)
    {
        var total = new CityYield();
        foreach (var id in city.Institutions)
        {
            if (!_byGuild.TryGetValue(id, out var e)) continue;
            total += new CityYield(e.Food, e.Production, e.Commerce, e.Culture);
            if (Institutions.HomeCity(sim, id)?.Id == city.Id) total += new CityYield(0, e.HomeProduction, 0, e.HomeCulture);
        }
        return total;
    }
}
