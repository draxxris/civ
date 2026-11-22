using Ariel.Ai;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>The computer player's spreading of corporations, run inside the standard AI's turn after religion and
/// before the great people planner (<c>great_people:planner</c>, at <see cref="AiPlannerOrder.Religion"/> + 200).
/// Founding is production: the city planner builds a headquarters, valued by <see cref="HeadquartersValuer"/>.</summary>
internal sealed class CorporationAiPlanner : IAiPlanner
{
    public string Id => "corporations:planner";
    public int Order => AiPlannerOrder.Religion + 100;
    public void Plan(AiContext ctx) => new CorporationPlanner(ctx).Plan();
}

/// <summary>Spreads founded corporations to the empire's own cities for gold. Spreading stops while income is negative,
/// because every branch adds maintenance.</summary>
internal sealed class CorporationPlanner(AiContext ctx)
{
    internal const int SpreadsPerTurn = 1;
    private ICorporationService? Corporations => ctx.Sim.Service<ICorporationService>();

    public void Plan()
    {
        if (Corporations is not { } corporations || ctx.Cities.Count == 0) return;
        Spread(corporations);
    }

    /// <summary>Everything one branch and its share of headquarters income produce, in the same points the city
    /// planner uses, minus the maintenance of one resource copy.</summary>
    public static long Worth(CorporationDef c) => PlannerPoints.Yields(c.YieldProduced)
        + PlannerPoints.Commerce(c.CommerceProduced) + PlannerPoints.Commerce(c.HeadquartersCommerce)
        - c.MaintenanceHundredths / 20;

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

    /// <summary>One economy calculation per pass: only our own spending moves the projection, and it is dropped
    /// whenever a branch is actually paid for.</summary>
    private bool Affordable(CorporationDef corporation, ref long? netGold)
    {
        netGold ??= ctx.Service<IPlayerEconomyService>().Calculate(ctx.Sim, ctx.PlayerId).NetGold;
        return ctx.Self.Treasury - corporation.SpreadCost >= 2 * ctx.GoldReserve && netGold >= 0;
    }
}

/// <summary>A founding headquarters has no effects of its own; this value stands for the corporation it founds: the
/// headquarters income, the first branch and the right to spread it. As with a religion's founding wonder, production
/// only offers it where founding can happen (its technology known, a consumed resource connected, nothing founded).
/// A corporation worth more adds up to as much again.</summary>
internal sealed class HeadquartersValuer(CorporationFounder founder) : IAiValuer<BuildingSubject>
{
    internal const int FoundingValue = 80;
    public string Id => "corporations:headquarters_valuer";

    public int Adjust(AiContext ctx, in BuildingSubject subject) =>
        founder.Corporation(subject.Building) is { } id
            ? FoundingValue + (int)Math.Clamp(CorporationPlanner.Worth(ctx.Sim.Content.Get<CorporationDef>(id)), 0, FoundingValue)
            : 0;
}
