using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Founds corporations with the right great person and spreads founded ones to the empire's own cities for
/// gold. Runs before the great person planner, so a great person able to found a corporation does that first.
/// Spreading stops while income is negative, because every branch adds maintenance.</summary>
internal sealed class CorporationPlanner(AiContext ctx)
{
    internal const int SpreadsPerTurn = 1;
    private ICorporationService? Corporations => ctx.Sim.Content.Services.Get<ICorporationService>().SingleOrDefault();

    public void Plan()
    {
        if (Corporations is not { } corporations || ctx.Cities.Count == 0) return;
        Found(corporations);
        Spread(corporations);
    }

    private IEnumerable<CorporationDef> Unfounded(ICorporationService corporations) => ctx.Sim.Content.Get<CorporationDef>().All
        .Where(c => corporations.Headquarters(ctx.Sim, c.Id) is null && (c.FoundingTech is not { } tech || ctx.Team.Techs.Contains(tech)))
        .OrderByDescending(Worth).ThenBy(c => c.Id, StringComparer.Ordinal);

    /// <summary>Yield and commerce a branch produces, in the same points the city planner uses.</summary>
    public static long Worth(CorporationDef c) => c.YieldProduced.Food * 8 + c.YieldProduced.Production * 8 + c.YieldProduced.Commerce * 5
        + c.CommerceProduced.Gold * 5 + c.CommerceProduced.Research * 6 + c.CommerceProduced.Culture * 3 + c.HeadquartersCommerce.Gold * 5
        - c.MaintenanceHundredths / 20;

    private void Found(ICorporationService corporations)
    {
        var open = Unfounded(corporations).ToList();
        if (open.Count == 0) return;
        foreach (var unit in ctx.Units.Where(u => ctx.Alive(u) && u.Health > 0 && u.MovesLeft > 0 && !ctx.Done.Contains(u.Id)).OrderBy(u => u.Id).ToList())
        {
            var def = ctx.Def(unit);
            var fits = open.Where(c => c.RequiredGreatPersonClass == def.DefaultUnit).ToList();
            if (fits.Count == 0) continue;
            if (FoundHere(unit, fits)) { ctx.Done.Add(unit.Id); open = Unfounded(corporations).ToList(); continue; }
            // Walk to the nearest own city with a resource the best fitting corporation consumes.
            var site = ctx.Cities.Where(c => fits.Any(corp => corporations.ConsumedBonusCount(ctx.Sim, c.Id, corp.Id) > 0))
                .OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenBy(c => c.Id).FirstOrDefault();
            if (site is null || site.Position == unit.Position) continue;
            if (ctx.MoveToward(unit, site.Position))
            {
                ctx.Done.Add(unit.Id);
                if (ctx.Alive(unit) && unit.Position == site.Position && unit.MovesLeft > 0) FoundHere(unit, fits);
            }
        }
    }

    private bool FoundHere(Unit unit, IEnumerable<CorporationDef> corporations)
    {
        foreach (var corporation in corporations)
            if (ctx.Issue(new FoundCorporationCommand { Player = ctx.PlayerId, Unit = unit.Id, Corporation = corporation.Id })) return true;
        return false;
    }

    private void Spread(ICorporationService corporations)
    {
        int spread = 0;
        foreach (var corporation in ctx.Sim.Content.Get<CorporationDef>().All.Where(c => corporations.Headquarters(ctx.Sim, c.Id) is not null)
            .OrderByDescending(Worth).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            if (Worth(corporation) <= 0) continue;
            foreach (var city in ctx.Cities.OrderByDescending(c => c.Population).ThenBy(c => c.Id).ToList())
            {
                if (spread >= SpreadsPerTurn || !Affordable(corporation)) return;
                if (ctx.Issue(new SpreadCorporationCommand { Player = ctx.PlayerId, City = city.Id, Corporation = corporation.Id })) spread++;
            }
        }
    }

    private bool Affordable(CorporationDef corporation) =>
        ctx.Self.Treasury - corporation.SpreadCost >= 2 * EconomyPlanner.Reserve(ctx.Cities.Count)
        && ctx.Service<IPlayerEconomyService>().Calculate(ctx.Sim, ctx.PlayerId).NetGold >= 0;
}
