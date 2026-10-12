using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Religion;

/// <summary>
/// Religion as a plain mod on the public API: Faith (a player-scoped resource) comes from priests, temples, shrines and
/// cathedrals; it pays for founding religions, missionaries and cathedrals. Religions are institutions, so founding and
/// spreading use the shared core rules. The code parts are the culture each religion gives and the holy city's tithe.
/// </summary>
public sealed class ReligionMod : IMod
{
    public const string Faith = "religion:faith";
    public const string Category = "religion";

    public void Register(IModContext ctx)
    {
        foreach (var r in ctx.Data.ReadJson<ResourceDef[]>("data/resources.json")) ctx.Resources.Register(r);
        foreach (var t in ctx.Data.ReadJson<TechDef[]>("data/techs.json")) ctx.Techs.Register(t);
        foreach (var b in ctx.Data.ReadJson<BuildingDef[]>("data/buildings.json")) ctx.Buildings.Register(b);
        foreach (var u in ctx.Data.ReadJson<UnitDef[]>("data/units.json")) ctx.Units.Register(u);
        foreach (var c in ctx.Data.ReadJson<CivicDef[]>("data/civics.json")) ctx.Civics.Register(c);
        foreach (var r in ctx.Data.ReadJson<InstitutionDef[]>("data/religions.json")) ctx.Institutions.Register(r);

        ctx.YieldSources.Register(new ReligiousCulture());
        ctx.ResourceSources.Register(new HolyCityTithe());
    }
}

/// <summary>+1 culture for every religion a city follows, +2 more in a religion's holy city.</summary>
public sealed class ReligiousCulture : ICityYieldSource
{
    public string Id => "religion:culture";

    public CityYield YieldFor(Simulation sim, City city)
    {
        int culture = 0;
        foreach (var id in city.Institutions)
        {
            if (sim.Content.Institutions.Get(id).Category != ReligionMod.Category) continue;
            culture += 1;
            if (sim.State.Institutions.TryGetValue(id, out var f) && f.HomeCity == city.Id) culture += 2;
        }
        return new CityYield(0, 0, 0, culture);
    }
}

/// <summary>A holy city collects 1 Faith for every other city, anyone's, that follows its religion.</summary>
public sealed class HolyCityTithe : IResourceSource
{
    public string Id => "religion:tithe";

    public void AddCityRates(Simulation sim, City city, IDictionary<string, long> rates)
    {
        foreach (var (id, founded) in sim.State.Institutions)
        {
            if (founded.HomeCity != city.Id || sim.Content.Institutions.Get(id).Category != ReligionMod.Category) continue;
            long followers = Institutions.CitiesWith(sim, id).Count(c => c.Id != city.Id);
            if (followers > 0) rates[ReligionMod.Faith] = rates.TryGetValue(ReligionMod.Faith, out var v) ? v + followers : followers;
        }
    }
}
