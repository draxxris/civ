using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Mods.Corporations;

namespace Ariel.Mods.Ai;

/// <summary>Founds corporations with the right great person and spreads founded ones to the empire's own cities for
/// gold. Runs before the great person planner, so a great person able to found a corporation does that first.
/// Spreading stops while income is negative, because every branch adds maintenance.</summary>
internal sealed class CorporationPlanner(AiContext ctx)
{
    internal const int SpreadsPerTurn = 1;
    private ICorporationService? Corporations => ctx.Sim.Service<ICorporationService>();
    private readonly Dictionary<(int City, string Corporation), long> _consumed = [];

    public void Plan()
    {
        if (Corporations is not { } corporations || ctx.Cities.Count == 0) return;
        _consumed.Clear();
        Found(corporations);
        Spread(corporations);
    }

    private IEnumerable<CorporationDef> Unfounded(ICorporationService corporations) => ctx.Sim.Content.Get<CorporationDef>().All
        .Where(c => !corporations.IsFounded(ctx.Sim, c.Id) && (c.FoundingTech is not { } tech || ctx.Team.Techs.Contains(tech)))
        .OrderByDescending(Worth).ThenBy(c => c.Id, StringComparer.Ordinal);

    /// <summary>Everything one branch and its share of headquarters income produce, in the same points the city
    /// planner uses, minus the maintenance of one resource copy.</summary>
    public static long Worth(CorporationDef c) => PlannerPoints.Yields(c.YieldProduced)
        + PlannerPoints.Commerce(c.CommerceProduced) + PlannerPoints.Commerce(c.HeadquartersCommerce)
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
            if (FoundHere(unit, fits))
            {
                ctx.Done.Add(unit.Id);
                // Drop what is now founded; re-scanning and re-sorting every corporation for each great person is waste.
                open.RemoveAll(c => corporations.IsFounded(ctx.Sim, c.Id));
                continue;
            }
            // Walk to the nearest own city with a resource the best fitting corporation consumes.
            var site = ctx.Cities.Where(c => fits.Any(corp => Consumed(corporations, c.Id, corp.Id) > 0))
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
            if (ctx.Issue(new PerformMissionCommand { Player = ctx.PlayerId, Unit = unit.Id, Mission = CorporationMissions.Found, Subject = corporation.Id })) return true;
        return false;
    }

    private void Spread(ICorporationService corporations)
    {
        int spread = 0;
        long? netGold = null;
        foreach (var corporation in ctx.Sim.Content.Get<CorporationDef>().All.Where(c => corporations.IsFounded(ctx.Sim, c.Id))
            .OrderByDescending(Worth).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            if (Worth(corporation) <= 0) continue;
            foreach (var city in ctx.Cities.OrderByDescending(c => c.Population).ThenBy(c => c.Id).ToList())
            {
                // One branch per turn for the whole empire. A rejected attempt changes nothing, so an unaffordable
                // corporation must not end the pass: keep looking at the cheaper ones.
                if (spread >= SpreadsPerTurn) return;
                if (!Affordable(corporation, ref netGold)) continue;
                if (ctx.Issue(new SpreadCorporationCommand { Player = ctx.PlayerId, City = city.Id, Corporation = corporation.Id }))
                {
                    spread++;
                    netGold = null;
                }
            }
        }
    }

    private long Consumed(ICorporationService corporations, int city, string corporation)
    {
        if (_consumed.TryGetValue((city, corporation), out long count)) return count;
        count = corporations.ConsumedBonusCount(ctx.Sim, city, corporation);
        _consumed[(city, corporation)] = count;
        return count;
    }

    /// <summary>One economy calculation per pass: only our own spending moves the projection, and it is dropped
    /// whenever a branch is actually paid for.</summary>
    private bool Affordable(CorporationDef corporation, ref long? netGold)
    {
        netGold ??= ctx.Service<IPlayerEconomyService>().Calculate(ctx.Sim, ctx.PlayerId).NetGold;
        return ctx.Self.Treasury - corporation.SpreadCost >= 2 * EconomyPlanner.Reserve(ctx.Cities.Count) && netGold >= 0;
    }
}
