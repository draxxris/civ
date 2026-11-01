using Ariel.Game;

namespace Ariel.Mods.Ai;

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
        foreach (var view in cities.Views.OrderByDescending(Urgency).ThenByDescending(v => v.Population).ThenBy(v => v.City.Id))
        {
            if (done >= MaxPerTurn || spare <= 0) return;
            if (view.City.ProductionQueue.FirstOrDefault() is not { } head || head.Kind == "economy:process") continue;
            if (hurry.Validate(ctx.Sim, ctx.PlayerId, view.City.Id, HurryGold) is not null) continue;
            long gold = hurry.Calculate(ctx.Sim, view.City.Id, HurryGold).Gold;
            if (gold <= 0 || gold > spare || !ctx.Issue(new HurryCityCommand { Player = ctx.PlayerId, CityId = view.City.Id, HurryId = HurryGold })) continue;
            spare -= gold; done++;
        }
    }

    /// <summary>Defenders in threatened cities first, then settlers, then wonders, then everything else.</summary>
    public int Urgency(CityView view)
    {
        if (view.City.ProductionQueue.FirstOrDefault() is not { } head) return 0;
        if (head.Kind == "cities:unit")
        {
            var role = ctx.Catalog.Roles.GetValueOrDefault(head.ContentId);
            if (view.Threat > 0 && role is UnitRole.Melee) return 3;
            if (role == UnitRole.Settler) return 2;
            return 0;
        }
        return head.Kind == "cities:building"
            && CityBuildingRules.IsWonder(ctx.Sim.Content.Get<Ariel.Content.Schema.BuildingDef>(head.ContentId)) ? 1 : 0;
    }
}
