using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Corporations;

/// <summary>
/// Corporations as a plain mod on the public API. Capital (player-scoped) comes from merchants, markets and exchanges and
/// pays for founding and executives. Each branch consumes goods (flow resources from worked terrain) and Capital upkeep,
/// and turns the owner's remaining goods into food, production or commerce; its headquarters earns Capital per branch.
/// Corporations that use the same good cannot share a city (Civ4 rule), which the mod works out from its own data.
/// </summary>
public sealed class CorporationsMod : IMod
{
    public const string Capital = "corporations:capital";
    public const string Category = "corporation";
    public const string Tech = "corporations:joint_stock";
    public const string Exchange = "corporations:exchange";
    public const string Executive = "corporations:executive";

    public void Register(IModContext ctx)
    {
        foreach (var r in ctx.Data.ReadJson<ResourceDef[]>("data/resources.json")) ctx.Resources.Register(r);
        foreach (var t in ctx.Data.ReadJson<TechDef[]>("data/techs.json")) ctx.Techs.Register(t);
        foreach (var b in ctx.Data.ReadJson<BuildingDef[]>("data/buildings.json")) ctx.Buildings.Register(b);
        foreach (var u in ctx.Data.ReadJson<UnitDef[]>("data/units.json")) ctx.Units.Register(u);

        var corps = ctx.Data.ReadJson<CorporationDef[]>("data/corporations.json");
        foreach (var c in corps)
        {
            ctx.Institutions.Register(new InstitutionDef
            {
                Id = c.Id, Name = c.Name, Description = c.Description, Color = c.Color, Category = Category,
                RequiresTech = Tech, RequiresBuilding = Exchange,
                FoundCost = new() { [Capital] = c.FoundCost },
                Spreaders = [Executive],
                Conflicts = corps.Where(o => o.Id != c.Id && o.Consumes.Intersect(c.Consumes).Any()).Select(o => o.Id).ToArray(),
            });
        }

        var byId = corps.ToDictionary(c => c.Id, StringComparer.Ordinal);
        ctx.ResourceSources.Register(new Branches(byId));
        ctx.YieldSources.Register(new BranchYields(byId));
    }

    /// <summary>A branch works while the owner's net supply of every good it consumes is not negative and Capital upkeep can be met.</summary>
    public static bool Operating(Simulation sim, int playerId, CorporationDef corp)
    {
        var rates = Resources.PlayerRates(sim, playerId);
        if (corp.Consumes.Any(g => rates.GetValueOrDefault(g) < 0)) return false;
        return sim.State.PlayerById(playerId).Resources.GetValueOrDefault(Capital) > 0 || rates.GetValueOrDefault(Capital) >= 0;
    }
}

/// <summary>The mod's own data schema; turned into an <see cref="InstitutionDef"/> plus the behaviour below.</summary>
public sealed class CorporationDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Color { get; init; } = "#cccccc";
    public string[] Consumes { get; init; } = [];
    public int FoodPercent { get; init; }
    public int ProductionPercent { get; init; }
    public int CommercePercent { get; init; }
    public int Upkeep { get; init; }
    public int HeadquartersCapital { get; init; }
    public long FoundCost { get; init; }
}

/// <summary>Per branch: -1 of each consumed good and -upkeep Capital. The headquarters gains Capital for every branch, itself included.</summary>
public sealed class Branches(IReadOnlyDictionary<string, CorporationDef> corps) : IResourceSource
{
    public string Id => "corporations:branches";

    public void AddCityRates(Simulation sim, City city, IDictionary<string, long> rates)
    {
        foreach (var id in city.Institutions)
        {
            if (!corps.TryGetValue(id, out var corp)) continue;
            foreach (var good in corp.Consumes) Add(rates, good, -1);
            Add(rates, CorporationsMod.Capital, -corp.Upkeep);
        }
        foreach (var (id, founded) in sim.State.Institutions)
            if (founded.HomeCity == city.Id && corps.TryGetValue(id, out var corp))
                Add(rates, CorporationsMod.Capital, corp.HeadquartersCapital * (long)Institutions.CitiesWith(sim, id).Count());
    }

    private static void Add(IDictionary<string, long> rates, string id, long amount) =>
        rates[id] = rates.TryGetValue(id, out var v) ? v + amount : amount;
}

/// <summary>Each operating branch yields its percentages of the owner's remaining (net) supply of the goods it consumes.</summary>
public sealed class BranchYields(IReadOnlyDictionary<string, CorporationDef> corps) : ICityYieldSource
{
    public string Id => "corporations:yields";

    public CityYield YieldFor(Simulation sim, City city)
    {
        var total = new CityYield();
        foreach (var id in city.Institutions)
        {
            if (!corps.TryGetValue(id, out var corp) || !CorporationsMod.Operating(sim, city.Owner, corp)) continue;
            long goods = corp.Consumes.Sum(g => Math.Max(0, Resources.PlayerRate(sim, city.Owner, g)));
            total += new CityYield((int)(goods * corp.FoodPercent / 100), (int)(goods * corp.ProductionPercent / 100), (int)(goods * corp.CommercePercent / 100), 0);
        }
        return total;
    }
}
