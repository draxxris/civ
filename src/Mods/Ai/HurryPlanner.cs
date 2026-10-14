using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Spends surplus gold on finishing production, never dipping below a reserve.</summary>
internal sealed class HurryPlanner(AiContext ctx, CityPlanner cities)
{
    private const string HurryGold = "cities:hurry_gold";
    private const int MaxPerTurn = 2;

    public void Plan()
    {
        long spare = ctx.Self.Treasury - 2 * EconomyPlanner.Reserve(ctx.Cities.Count);
        if (spare <= 0) return;
        var hurry = ctx.Service<ICityHurryService>();
        int done = 0;
        foreach (var view in cities.Views.OrderByDescending(v => v.Population).ThenBy(v => v.City.Id))
        {
            if (done >= MaxPerTurn || spare <= 0) return;
            if (view.City.ProductionQueue.FirstOrDefault() is not { } head || head.Kind == "economy:process") continue;
            if (hurry.Validate(ctx.Sim, ctx.PlayerId, view.City.Id, HurryGold) is not null) continue;
            long gold = hurry.Calculate(ctx.Sim, view.City.Id, HurryGold).Gold;
            if (gold <= 0 || gold > spare || !ctx.Issue(new HurryCityCommand { Player = ctx.PlayerId, CityId = view.City.Id, HurryId = HurryGold })) continue;
            spare -= gold; done++;
        }
    }
}
