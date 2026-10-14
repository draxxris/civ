using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Use the Cities governor for growth. When soldiers are urgently needed, emphasize production
/// without assigning more citizens or less food than the city can support.</summary>
internal sealed class CitizenPlanner(AiContext ctx, ArmyPlan army)
{
    private sealed record Choice(TileId Tile, YieldVector Yield);

    public void Plan()
    {
        foreach (var city in ctx.Cities.OrderBy(c => c.Id))
        {
            var wellbeing = ctx.Service<ICityWellbeingService>().Calculate(ctx.Sim, city.Id);
            bool avoid = wellbeing.Happy <= wellbeing.Unhappy && city.Population >= 4;
            ctx.Issue(new SetCityAssignmentsCommand { Player = ctx.PlayerId, CityId = city.Id, Automatic = true, AvoidGrowth = avoid });
            var view = CityView.Build(ctx, city);
            if (view.Garrison < army.DesiredGarrison(view) || TrainingSoldiers(city)) EmphasizeProduction(city, wellbeing, avoid);
        }
    }

    private bool TrainingSoldiers(City city) => city.ProductionQueue.FirstOrDefault() is { Kind: "cities:unit" } head
        && ctx.Catalog.Roles.GetValueOrDefault(head.ContentId) is UnitRole.Melee or UnitRole.Siege;

    private void EmphasizeProduction(City city, CityWellbeing wellbeing, bool avoid)
    {
        int citizens = Math.Max(0, city.Population - wellbeing.Angry);
        var choices = Choices(city);
        var chosen = choices.OrderByDescending(c => c.Yield.Production * 12 + c.Yield.Food * 4 + c.Yield.Commerce)
            .ThenBy(c => c.Tile.Value).Take(citizens).ToList();
        // Repair a production-heavy assignment before it starves the city. Consumption and center minima come from content.
        var rules = ctx.Sim.Content.Get<RuleConstantsDef>().All.Single().Values;
        long needed = Math.Max(0, (long)rules.FoodConsumptionPerPopulation * city.Population + wellbeing.FoodPenalty - CenterFood(city));
        SupplyFood(chosen, choices, needed);
        ctx.Issue(new SetCityAssignmentsCommand
        {
            Player = ctx.PlayerId, CityId = city.Id, Tiles = chosen.Select(c => ctx.Grid.HexOf(c.Tile)).ToArray(), AvoidGrowth = avoid,
        });
    }

    private long CenterFood(City city)
    {
        long natural = ctx.Service<IPlotYieldService>().Calculate(ctx.Sim, ctx.PlayerId, ctx.Tile(city.Position)).Yields.Food;
        return Math.Max(natural, ctx.Sim.Content.Get<YieldDef>("foundation:yield_food").MinCity);
    }

    private List<Choice> Choices(City city)
    {
        var blocked = ctx.Cities.Where(c => c.Id != city.Id).SelectMany(c => c.Worked).Select(ctx.Tile).ToHashSet();
        var yields = ctx.Service<IPlotYieldService>();
        return ctx.Service<ICityWorkService>().WorkArea(ctx.Sim, city.Id)
            .Where(t => !blocked.Contains(t) && ctx.Sim.State.Plots[t.Value].Owner == ctx.PlayerId && ctx.Sim.State.CityAt(ctx.Grid.HexOf(t)) is null)
            .Select(t => new Choice(t, yields.Calculate(ctx.Sim, ctx.PlayerId, t).Yields))
            .Where(c => c.Yield != default).ToList();
    }

    private static void SupplyFood(List<Choice> chosen, List<Choice> available, long needed)
    {
        while (chosen.Sum(c => c.Yield.Food) < needed && chosen.Count > 0)
        {
            var least = chosen.OrderBy(c => c.Yield.Food).ThenBy(c => c.Yield.Production).ThenBy(c => c.Tile.Value).First();
            var more = available.Where(c => !chosen.Contains(c) && c.Yield.Food > least.Yield.Food)
                .OrderByDescending(c => c.Yield.Food).ThenByDescending(c => c.Yield.Production).ThenBy(c => c.Tile.Value).FirstOrDefault();
            if (more is null) break;
            chosen.Remove(least); chosen.Add(more);
        }
    }
}
