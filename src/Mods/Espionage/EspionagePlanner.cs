using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Espionage;

/// <summary>The computer player's espionage, run in the standard AI's turn at <see cref="AiPlannerOrder.Espionage"/>,
/// after the military planner has moved its army.</summary>
internal sealed class EspionageAiPlanner : IAiPlanner
{
    public string Id => "espionage:planner";
    public int Order => AiPlannerOrder.Espionage;
    public void Plan(AiContext ctx) => new EspionagePlanner(ctx).Plan();
}

/// <summary>What the city planner should build for espionage: spies, one per <see cref="EspionagePlanner.CitiesPerSpy"/>
/// cities once a rival is known, ranked a little above an ordinary naval explorer.</summary>
internal sealed class SpyWishes : IProductionWishes
{
    internal const long SpyScore = 150;
    public string Id => "espionage:spies";

    public IEnumerable<ProductionWish> Wishes(AiContext ctx, City city)
    {
        if (!new EspionagePlanner(ctx).WantsSpy()) yield break;
        foreach (var unit in ctx.Sim.Content.Get<UnitDef>().All.Where(u => u.Spy))
            yield return new("cities:unit", unit.Id, SpyScore + 1000L / Math.Max(1, unit.Cost));
    }
}

/// <summary>Points the espionage budget at one rival, walks spies to that rival's cities, and runs the best mission the
/// points allow: stealing a technology first, then sabotaging a wonder, and in war, stirring unrest or stealing gold.
/// The rival is chosen from what this player can see: enemies first, then the nearest known civilization.</summary>
internal sealed class EspionagePlanner(AiContext ctx)
{
    internal const int ReviewInterval = 5, FocusWeight = 4, OtherWeight = 1, CitiesPerSpy = 6;
    private IEspionageService? Espionage => ctx.Sim.Service<IEspionageService>();

    public void Plan()
    {
        if (Espionage is not { } espionage || ctx.Cities.Count == 0) return;
        int? focus = FocusTeam();
        if ((ctx.Turn + ctx.PlayerId) % ReviewInterval == 0) Weights(espionage, focus);
        foreach (var spy in ctx.Units.Where(u => ctx.Alive(u) && u.Health > 0 && u.MovesLeft > 0 && !ctx.HasActed(u) && ctx.Def(u).Spy)
            .OrderBy(u => u.Id).ToList())
            Act(spy, focus);
    }

    /// <summary>Spies wanted: one per <see cref="CitiesPerSpy"/> cities once a rival is known.</summary>
    public bool WantsSpy() => Espionage is not null && FocusTeam() is not null
        && ctx.Units.Count(u => ctx.Def(u).Spy) + ctx.Cities.Sum(c => c.ProductionQueue.Count(o => o.Kind == "cities:unit"
            && ctx.Sim.Content.Get<UnitDef>(o.ContentId).Spy)) < Math.Max(1, ctx.Cities.Count / CitiesPerSpy);

    private IEnumerable<City> KnownRivalCities() => ctx.Sim.State.Cities.Where(c => ctx.Sim.TeamOf(c.Owner).Id != ctx.Team.Id
        && !PlayerRules.IsBarbarian(ctx.Sim, c.Owner) && ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id)
        && ctx.Sim.Rules().Relations.HasMet(ctx.Sim, ctx.Team.Id, ctx.Sim.TeamOf(c.Owner).Id));

    /// <summary>An enemy team when at war, otherwise the known rival whose nearest city is closest to ours.</summary>
    public int? FocusTeam()
    {
        var rivals = KnownRivalCities().GroupBy(c => ctx.Sim.TeamOf(c.Owner).Id)
            .Select(g => (Team: g.Key, Enemy: ctx.Team.Wars.Contains(g.Key),
                Distance: g.Min(c => ctx.Cities.Min(own => ctx.Grid.Distance(own.Position, c.Position)))))
            .OrderByDescending(r => r.Enemy).ThenBy(r => r.Distance).ThenBy(r => r.Team).ToList();
        return rivals.Count == 0 ? null : rivals[0].Team;
    }

    private void Weights(IEspionageService espionage, int? focus)
    {
        foreach (var team in ctx.Sim.State.Teams.Where(t => t.Id != ctx.Team.Id).OrderBy(t => t.Id))
        {
            int weight = team.Id == focus ? FocusWeight : OtherWeight;
            if (espionage.ValidateWeight(ctx.Sim, ctx.PlayerId, team.Id, weight) is not null) continue;
            if (espionage.Weight(ctx.Sim, ctx.PlayerId, team.Id) != weight)
                ctx.Issue(new SetEspionageWeightCommand { Player = ctx.PlayerId, TargetTeam = team.Id, Weight = weight });
        }
    }

    private void Act(Unit spy, int? focus)
    {
        ctx.MarkActed(spy);
        if (ctx.Sim.State.CityAt(spy.Position) is { } here && ctx.Sim.TeamOf(here.Owner).Id != ctx.Team.Id)
        {
            if (TryMission(spy, here)) return;
            // Wait inside the city for points to build up.
            return;
        }
        if (focus is not { } team) return;
        var target = KnownRivalCities().Where(c => ctx.Sim.TeamOf(c.Owner).Id == team)
            .Where(c => ctx.Sim.State.PlotAt(c.Position).AreaId == ctx.Sim.State.PlotAt(spy.Position).AreaId)
            .OrderByDescending(c => c.Population).ThenBy(c => ctx.Grid.Distance(spy.Position, c.Position)).ThenBy(c => c.Id).FirstOrDefault();
        if (target is not null) ctx.MoveToward(spy, target.Position);
    }

    /// <summary>Missions in order of worth. Candidates are the target's own stealable technologies, cheapest first, so
    /// each spy prices tens of technologies instead of the whole catalog. Every mission is still checked by the
    /// normal command, and the AI issues every mission the rules define.</summary>
    public bool TryMission(Unit spy, City city)
    {
        foreach (var (mission, tech) in Missions(city))
            if (ctx.Issue(EspionageMissions.Order(ctx.PlayerId, spy.Id, mission, tech)))
                return true;
        return false;
    }

    private bool AtWarWith(int player) => player != ctx.PlayerId && ctx.Team.Wars.Contains(ctx.Sim.TeamOf(player).Id);

    private IEnumerable<(EspionageMissionKind, string?)> Missions(City city)
    {
        var service = Espionage!;
        var research = ctx.Sim.Rules().Research;
        // Only what this rival knows and this player can research; cheapest first, so the first candidate is the
        // most likely to be affordable.
        var stealable = service.StealableTechnologies(ctx.Sim, ctx.PlayerId, city.Id);
        if (research is not null)
            foreach (var tech in stealable
                .OrderBy(t => research.CalculateCost(ctx.Sim, ctx.Team.Id, t).Cost).ThenBy(t => t, StringComparer.Ordinal))
                yield return (EspionageMissionKind.StealTech, tech);
        if (city.ProductionQueue.FirstOrDefault() is { Kind: "cities:building" } head
            && CityBuildingRules.IsWonder(ctx.Sim.Content.Get<BuildingDef>(head.ContentId)))
            yield return (EspionageMissionKind.SabotageProduction, null);
        yield return (EspionageMissionKind.InciteRevolt, null);
        yield return (EspionageMissionKind.CounterEspionage, null);
        if (!AtWarWith(city.Owner)) yield break;
        yield return (EspionageMissionKind.FomentUnhappiness, null);
        yield return (EspionageMissionKind.StealTreasury, null);
    }
}
