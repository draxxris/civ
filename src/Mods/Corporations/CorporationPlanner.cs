using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>The computer player's spreading of corporations, run inside the standard AI's turn after religion and
/// before the great people planner (<c>great_people:planner</c>, at <see cref="AiPlannerOrder.Religion"/> + 200).
/// Founding is production: the city planner builds a headquarters or Master Guild, valued by <see cref="HeadquartersValuer"/>, and
/// trains executives for foreign spread, wished for by <see cref="ExecutiveWishes"/>. With Realistic Guilds/Corporations
/// guilds and corporations found and spread by themselves, so none of these plan anything there.</summary>
internal sealed class CorporationAiPlanner(CorporationService service) : IAiPlanner
{
    public string Id => "corporations:planner";
    public int Order => AiPlannerOrder.Religion + 100;
    public void Plan(AiContext ctx) => new CorporationPlanner(ctx, service).Plan();
}

/// <summary>Spreads founded corporations: executives walk to foreign cities and open branches there, as the religion
/// planner's missionaries do once the empire's own cities have the faith, and the empire's own cities are paid for
/// directly, which needs no unit. Both ways cost the spread cost and share one budget of
/// <see cref="SpreadsPerTurn"/> branches a turn. Spreading stops while income is negative, because every branch
/// adds maintenance.</summary>
internal sealed class CorporationPlanner(AiContext ctx, CorporationService service)
{
    internal const int SpreadsPerTurn = 1;
    private int _spread;
    private long? _netGold;

    public void Plan()
    {
        if (ctx.Sim.Service<ICorporationService>() is null || ctx.Cities.Count == 0 || CorporationMode.Realistic(ctx.Sim)) return;
        var content = ctx.Sim.Content;
        // An obsolete corporation can be spread nowhere by this team, so it is not planned.
        var spreading = content.Get<CorporationDef>().All.Where(c => service.IsFounded(ctx.Sim, c.Id)
                && !CorporationService.Obsolete(ctx.Sim, ctx.PlayerId, c) && Worth(content, c) > 0)
            .OrderByDescending(c => Worth(content, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        if (spreading.Count == 0) return;
        Executives(spreading);
        PayForBranches(spreading);
    }

    /// <summary>Everything one branch and its share of headquarters income produce, in the same points the city
    /// planner uses, minus the maintenance of one resource copy: a branch with one copy of a resource it consumes, or
    /// with none for a corporation that consumes none. The per-copy output stays in hundredths until the end, so a
    /// fraction of a point still counts. Its happiness, health, military production and produced resource count as
    /// <see cref="Extras"/> weighs them.</summary>
    public static long Worth(Ariel.Content.ContentDb content, CorporationDef c)
    {
        long copies = c.PrereqBonuses.Length > 0 ? 1 : 0;
        long perCopy = (PlannerPoints.Yields(c.YieldProducedHundredths) + PlannerPoints.Commerce(c.CommerceProducedHundredths)
            - c.MaintenanceHundredths * PlannerPoints.Gold) * copies;
        return perCopy / 100 + PlannerPoints.Yields(c.YieldChanges) + PlannerPoints.Commerce(c.CommerceChanges)
            + PlannerPoints.Commerce(c.HeadquartersCommerce) + Extras(content, c, copies);
    }

    /// <summary>The weights the civic planner gives a point of happiness and of health in a city.</summary>
    internal const int HappinessPoints = 8, HealthPoints = 6;
    /// <summary>The production a military production percent is weighed on: a modest city's ten.</summary>
    internal const int MilitaryProductionBase = 10;

    /// <summary>What a branch with this many consumed copies gives beyond its yields and commerce, in planner points:
    /// its happiness and health at the civic planner's weights, its military production percent as production of a city
    /// making <see cref="MilitaryProductionBase"/>, and the happiness and health of the resource it produces once it
    /// produces a copy (more copies of one resource add nothing).</summary>
    internal static long Extras(Ariel.Content.ContentDb content, CorporationDef c, long copies)
    {
        long points = (long)c.Happiness * HappinessPoints + (long)c.Health * HealthPoints
            + (long)c.MilitaryProductionPercent * MilitaryProductionBase * PlannerPoints.Production / 100;
        if (c.BonusProduced is { } produced && (c.PrereqBonuses.Length == 0 || copies > 0) && content.TryGet<BonusDef>(produced, out var bonus))
            points += (long)bonus.Happiness * HappinessPoints + (long)bonus.Health * HealthPoints;
        return points;
    }

    /// <summary>Each awake executive goes to the nearest city it can open a branch in: a foreign one, or failing that
    /// one of the empire's own, and spreads there when the turn's budget and the treasury allow.</summary>
    private void Executives(IReadOnlyList<CorporationDef> spreading)
    {
        foreach (var unit in ctx.Units.Where(u => ctx.Alive(u) && u.Health > 0 && !ctx.HasActed(u)
                && CorporationService.IsExecutive(ctx.Sim, u.Type)).OrderBy(u => u.Id).ToList())
        {
            if ((unit.Extensions.Get<MilitaryUnitState>(ctx.Sim.Content)?.Activity ?? UnitActivity.Awake) != UnitActivity.Awake) continue;
            Send(unit, spreading.Where(c => CorporationService.Spreads(ctx.Sim, unit, c)));
        }
    }

    private void Send(Unit unit, IEnumerable<CorporationDef> corporations)
    {
        foreach (var corporation in corporations)
        {
            var targets = ExecutiveTargets.Foreign(ctx, service, corporation, unit.Position)
                .Concat(ExecutiveTargets.Own(ctx, service, corporation, unit.Position));
            foreach (var city in targets)
                if (Visit(unit, city, corporation)) return;
        }
    }

    private bool Visit(Unit unit, City city, CorporationDef corporation)
    {
        bool moved = ctx.MoveToward(unit, city.Position);
        bool here = ctx.Alive(unit) && unit.Position == city.Position;
        if (!moved && !here) return false;
        // An executive that arrived waits there for gold or the next turn's budget rather than wandering off.
        if (here && unit.MovesLeft > 0 && CanSpend(corporation, city)
            && ctx.Issue(new PerformMissionCommand { Player = ctx.PlayerId, Unit = unit.Id, Mission = CorporationMissions.Spread, Subject = corporation.Id }))
            Spent();
        // A unit that moved has spent its turn even when it did not arrive, so it is not planned again this pass.
        ctx.MarkActed(unit);
        return true;
    }

    private void PayForBranches(IReadOnlyList<CorporationDef> spreading)
    {
        foreach (var corporation in spreading)
        {
            foreach (var city in ctx.Cities.OrderByDescending(c => c.Population).ThenBy(c => c.Id).ToList())
            {
                // One branch per turn for the whole empire. A rejected attempt changes nothing, so an unaffordable
                // corporation must not end the pass: keep looking at the cheaper ones.
                if (_spread >= SpreadsPerTurn) return;
                if (!CanSpend(corporation, city)) continue;
                if (ctx.Issue(new SpreadCorporationCommand { Player = ctx.PlayerId, City = city.Id, Corporation = corporation.Id }))
                    Spent();
            }
        }
    }

    private bool CanSpend(CorporationDef corporation, City city)
    {
        if (_spread >= SpreadsPerTurn) return false;
        _netGold ??= NetGold(ctx);
        return Affordable(ctx, service.SpreadCost(ctx.Sim, ctx.PlayerId, city, corporation), _netGold.Value);
    }

    /// <summary>One economy calculation per pass: only our own spending moves the projection, and it is dropped
    /// whenever a branch is actually paid for.</summary>
    private void Spent()
    {
        _spread++;
        _netGold = null;
    }

    internal static long NetGold(AiContext ctx) => ctx.Service<IPlayerEconomyService>().Calculate(ctx.Sim, ctx.PlayerId).NetGold;

    /// <summary>Whether the player should pay a spread cost now (<see cref="CorporationService.SpreadCost"/>, the
    /// same price the spread charges): it leaves twice the gold reserve, and income is not negative.</summary>
    internal static bool Affordable(AiContext ctx, long cost, long netGold) =>
        ctx.Self.Treasury - cost >= 2 * ctx.GoldReserve && netGold >= 0;
}

/// <summary>The cities the computer player's executives aim at, shared by the planner that sends them and the wishes
/// that train them, so an executive is only trained for a city it would be sent to.</summary>
internal static class ExecutiveTargets
{
    /// <summary>How far a foreign city may be from the executive, or from the headquarters that would train one, as
    /// for missionaries.</summary>
    internal const int ForeignSpreadRange = 10;

    /// <summary>Cities of civilizations at peace whose borders the player's units may cross, on the executive's
    /// landmass, revealed to the team, within <see cref="ForeignSpreadRange"/>, and able to take a branch now (see
    /// <see cref="CorporationService.RefuseBranch"/>); nearest first, then the larger, then by id. Sharing a corporation
    /// abroad pays the headquarters for every branch while the foreign owner pays the maintenance.</summary>
    public static IEnumerable<City> Foreign(AiContext ctx, CorporationService service, CorporationDef corporation, Hex from)
    {
        var sim = ctx.Sim;
        int area = sim.State.PlotAt(from).AreaId;
        var relations = sim.Rules().Relations;
        // The branch check runs the connectivity query, so it comes after every cheap filter.
        return sim.State.Cities.Where(c => c.Owner != ctx.PlayerId && PlayerRules.IsNation(sim, c.Owner)
                && ctx.Grid.Distance(from, c.Position) <= ForeignSpreadRange
                && sim.State.PlotAt(c.Position).AreaId == area
                && sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id)
                && relations.MayEnter(sim, ctx.Team.Id, sim.TeamOf(c.Owner).Id))
            .OrderBy(c => ctx.Grid.Distance(from, c.Position)).ThenByDescending(c => c.Population).ThenBy(c => c.Id)
            .Where(c => service.RefuseBranch(sim, ctx.PlayerId, c, corporation) is null);
    }

    /// <summary>The player's own cities on the executive's landmass that could take a branch, nearest first. The
    /// planner pays for these directly, so they are only where an executive with no foreign city to go to is spent.</summary>
    public static IEnumerable<City> Own(AiContext ctx, CorporationService service, CorporationDef corporation, Hex from)
    {
        int area = ctx.Sim.State.PlotAt(from).AreaId;
        return ctx.Cities.Where(c => ctx.Sim.State.PlotAt(c.Position).AreaId == area)
            .OrderBy(c => ctx.Grid.Distance(from, c.Position)).ThenByDescending(c => c.Population).ThenBy(c => c.Id).ToList()
            .Where(c => service.RefuseBranch(ctx.Sim, ctx.PlayerId, c, corporation) is null);
    }
}

/// <summary>Asks a corporation's headquarters, when the player holds it, for one executive: while none of that
/// corporation is alive or queued, the player can afford a branch, and a foreign city the executive would be sent to
/// can take one. The empire's own cities need no executive, since the planner pays for their branches directly. As
/// with the religion mod's inquisitors this is one executive at a time (per corporation), and only the headquarters
/// can train it.</summary>
internal sealed class ExecutiveWishes(CorporationService service) : IProductionWishes
{
    public string Id => "corporations:executives";

    /// <summary>What an executive is worth to the city planner: the headquarters income of one more branch, in the
    /// planner's points, over its cost plus 60, which is how the planner rates a building's yield (see
    /// <see cref="ProductionWish"/>). The four gold of most corporations makes about 125 for a 100-hammer executive.</summary>
    internal static long Score(CorporationDef corporation, UnitDef executive) =>
        PlannerPoints.Commerce(corporation.HeadquartersCommerce) * 1000L / (executive.Cost + 60);

    public IEnumerable<ProductionWish> Wishes(AiContext ctx, City city)
    {
        // Executives are refused with Realistic Guilds/Corporations.
        if (CorporationMode.Realistic(ctx.Sim)) yield break;
        // Cheapest first: this runs for every city each time it plans, and only a headquarters trains executives.
        var corporations = ctx.Sim.Content.Get<CorporationDef>().All
            .Where(c => service.Headquarters(ctx.Sim, c.Id) == city.Id && CorporationPlanner.Worth(ctx.Sim.Content, c) > 0)
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        if (corporations.Count == 0) yield break;
        long? netGold = null;
        foreach (var corporation in corporations)
        {
            if (Busy(ctx, corporation)) continue;
            // The base cost is a lower bound of every price, so it rules out a poor treasury before the dear target search.
            netGold ??= CorporationPlanner.NetGold(ctx);
            if (!CorporationPlanner.Affordable(ctx, corporation.SpreadCost, netGold.Value)) continue;
            if (ExecutiveTargets.Foreign(ctx, service, corporation, city.Position).FirstOrDefault() is not { } target) continue;
            if (!CorporationPlanner.Affordable(ctx, service.SpreadCost(ctx.Sim, ctx.PlayerId, target, corporation), netGold.Value)) continue;
            foreach (var unit in ctx.Sim.Content.Get<UnitDef>().All.Where(u => CorporationService.Spreads(ctx.Sim, u.Id, corporation)))
                yield return new("cities:unit", unit.Id, Score(corporation, unit));
        }
    }

    /// <summary>Whether an executive of the corporation is already alive or queued anywhere in the empire.</summary>
    private static bool Busy(AiContext ctx, CorporationDef corporation) =>
        ctx.Units.Any(u => ctx.Alive(u) && u.Health > 0 && CorporationService.Spreads(ctx.Sim, u, corporation))
        || ctx.Cities.SelectMany(c => c.ProductionQueue)
            .Any(o => o.Kind == "cities:unit" && CorporationService.Spreads(ctx.Sim, o.ContentId, corporation));
}

/// <summary>A founding building (a headquarters, or a guild's Master Guild) stands for the corporation it founds: the
/// headquarters income, the first branch and the right to spread it, on top of what the building itself gives. As
/// with a religion's founding wonder, production only offers it where founding can happen (its requirements met, a
/// consumed resource connected when the corporation consumes any, nothing founded). A corporation worth more adds up
/// to as much again.</summary>
internal sealed class HeadquartersValuer : IAiValuer<BuildingSubject>
{
    internal const int FoundingValue = 80;
    public string Id => "corporations:headquarters_valuer";

    public int Adjust(AiContext ctx, in BuildingSubject subject)
    {
        // With Realistic Guilds/Corporations founding buildings are refused.
        if (CorporationMode.Realistic(ctx.Sim)) return 0;
        var content = ctx.Sim.Content;
        return CorporationFounder.Corporation(content, subject.Building) is { } corporation
            ? FoundingValue + (int)Math.Clamp(CorporationPlanner.Worth(content, corporation), 0, FoundingValue)
            : 0;
    }
}
