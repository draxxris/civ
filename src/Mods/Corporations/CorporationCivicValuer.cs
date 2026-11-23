using Ariel.Ai;
using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>What a civic's corporation rules are worth to the computer player, added to the civic planner's score
/// (<see cref="CivicSubject"/>). The base AI knows neither the <see cref="CivicCorporationsDef"/> attachment nor the
/// <c>corporations:maintenance_percent</c> key; this mod, which reads both, values them, in <see cref="PlannerPoints"/>.
/// <list type="bullet">
/// <item>A civic that bans corporations costs what the branches in the player's cities produce (their yields and
/// commerce, and their happiness, health, military production and produced resource as
/// <see cref="CorporationPlanner.Extras"/> weighs them) and saves their maintenance; one that bans foreign corporations
/// costs what the branches of other teams' corporations produce.</item>
/// <item>Either ban also costs the corporations the player could still get (<see cref="Prospects"/>), so a ban is not
/// free before anything is founded: a share of what a branch is worth (<see cref="CorporationPlanner.Worth"/>) in each
/// of its cities that could hold one. A foreign ban costs only the prospects headquartered in another team.</item>
/// <item>A change to maintenance gains or costs that share of the branches' maintenance, as gold.</item>
/// </list>
/// Everything is gathered once per planning segment (<see cref="AiContext.Derived{T}"/>): the same for every civic
/// weighed.</summary>
internal sealed class CorporationCivicValuer(ICorporationService corporations, NaturalCorporations natural) : IAiValuer<CivicSubject>
{
    /// <summary>Percent of a prospective corporation's branches a ban is charged for: none is open yet, and some never
    /// will be.</summary>
    internal const int ProspectPercent = 50;
    /// <summary>Percent of that again for a corporation whose founding technology the player can research now but does
    /// not know yet.</summary>
    internal const int ResearchablePercent = 50;

    public string Id => "corporations:civic_valuer";

    public int Adjust(AiContext ctx, in CivicSubject subject)
    {
        var sim = ctx.Sim;
        var civic = subject.Civic;
        var rules = sim.Content.Attachment<CivicCorporationsDef>(civic.Id);
        bool realistic = CorporationMode.Realistic(sim);
        long maintenancePercent = civic.Effects.Total(ModifierScope.Player, CorporationService.MaintenanceKey)
            + (rules is null ? 0 : realistic ? rules.RealisticMaintenancePercent : rules.MaintenancePercent);
        bool banned = rules?.NoCorporations == true, foreignBanned = rules?.NoForeignCorporations == true;
        if (!banned && !foreignBanned && maintenancePercent == 0) return 0;

        long points = 0;
        foreach (var city in ctx.Derived("corporations:civic_valuer:branches", () => Branches(ctx)))
        {
            foreach (var (corporation, worth) in city.Outputs)
            {
                if (!banned && !(foreignBanned && Foreign(ctx, corporation))) continue;
                points -= worth;
            }
            // Maintenance is in hundredths of gold. A ban ends it with the branches; a cut is a gain.
            if (banned) points += city.MaintenanceHundredths * PlannerPoints.Gold / 100;
            else if (maintenancePercent != 0) points -= city.MaintenanceHundredths * maintenancePercent / 100 * PlannerPoints.Gold / 100;
        }
        if (banned || foreignBanned)
            foreach (var prospect in ctx.Derived("corporations:civic_valuer:prospects", () => Prospects(ctx)))
                if (banned || prospect.Foreign) points -= prospect.Points;
        return (int)Math.Clamp(points, -AiValuation.Limit, AiValuation.Limit);
    }

    /// <summary>What each branch in a city produces, in points, and what the city pays for its branches: the same for
    /// every civic weighed, so gathered once per state rather than once per civic.</summary>
    private sealed record CityBranches((string Corporation, long Worth)[] Outputs, long MaintenanceHundredths);

    private CityBranches[] Branches(AiContext ctx)
    {
        var sim = ctx.Sim;
        var result = new List<CityBranches>();
        foreach (var city in ctx.Cities)
        {
            var present = corporations.Present(sim, city.Id);
            if (present.Count == 0) continue;
            var outputs = new (string, long)[present.Count];
            for (int i = 0; i < present.Count; i++)
            {
                var def = sim.Content.Get<CorporationDef>(present[i]);
                long copies = corporations.ConsumedBonusCount(sim, city.Id, present[i]);
                var (yields, commerce) = CorporationService.BranchOutput(def, copies);
                outputs[i] = (present[i], PlannerPoints.Yields(yields) + PlannerPoints.Commerce(commerce) + CorporationPlanner.Extras(sim.Content, def, copies));
            }
            result.Add(new(outputs, corporations.MaintenanceHundredths(sim, city.Id)));
        }
        return result.ToArray();
    }

    /// <summary>A corporation the player does not have in any city yet but could get, and what a ban would cost of it.</summary>
    internal sealed record Prospect(string Corporation, long Points, bool Foreign);

    /// <summary>The corporations the player could still get, with their civics left out (the ban is what is weighed),
    /// each charged <see cref="ProspectPercent"/> of <see cref="CorporationPlanner.Worth"/> for every one of its cities
    /// that could hold a branch (a city that can use a copy of a resource the corporation consumes; any city for one that
    /// consumes none):
    /// <list type="bullet">
    /// <item>Realistic Guilds/Corporations: an unfounded one the player's nation could found on its own (its technology
    /// known, its resource or buildings held), and a founded one whose branches could spread into those cities.</item>
    /// <item>Manual: an unfounded one whose founding technology the player knows (at <see cref="ResearchablePercent"/>
    /// of that when it can only research it now), and a founded one that could be spread to those cities: by the player
    /// from its own headquarters, or by another team's executives.</item>
    /// </list>
    /// A corporation obsolete for the player, or already in one of its cities (its branches are counted above), is not
    /// a prospect.</summary>
    internal Prospect[] Prospects(AiContext ctx)
    {
        var sim = ctx.Sim;
        bool realistic = CorporationMode.Realistic(sim);
        var cities = ctx.Cities.OrderBy(c => c.Id).ToArray();
        if (cities.Length == 0) return [];
        var all = sim.Content.Get<CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        var held = cities.SelectMany(c => corporations.Present(sim, c.Id)).ToHashSet(StringComparer.Ordinal);
        var candidates = all.Where(d => !held.Contains(d.Id) && !CorporationService.Obsolete(sim, ctx.PlayerId, d)).ToArray();
        if (candidates.Length == 0) return [];
        // One resource query per city, shared with the founding check.
        var pass = natural.BeginPass(sim);
        var counts = cities.Select(pass.Counts).ToArray();
        var result = new List<Prospect>();
        foreach (var def in candidates)
        {
            int sites = def.PrereqBonuses.Length == 0 ? cities.Length : counts.Count(c => CorporationService.Consumed(def, c) > 0);
            if (sites == 0) continue;
            long percent = ProspectPercent;
            bool foreign = false;
            if (corporations.Headquarters(sim, def.Id) is int hq)
                foreign = sim.TeamOf(sim.State.CityById(hq).Owner).Id != ctx.Team.Id;
            else if (realistic)
            {
                if (natural.FoundingSite(sim, ctx.PlayerId, def, ignoreCivics: true, pass) is null) continue;
            }
            else if (!NaturalCorporations.KnowsFoundingTechs(sim, ctx.PlayerId, def))
            {
                var techs = CorporationIndex.Of(sim.Content).FoundingTechs.GetValueOrDefault(def.Id, []);
                if (!techs.All(t => sim.KnowsTech(ctx.PlayerId, t) || sim.Rules().Research.Validate(sim, ctx.PlayerId, t).Success)) continue;
                percent = percent * ResearchablePercent / 100;
            }
            long worth = Math.Max(0, CorporationPlanner.Worth(sim.Content, def));
            if (worth > 0) result.Add(new(def.Id, worth * sites * percent / 100, foreign));
        }
        return result.ToArray();
    }

    /// <summary>Whether the corporation's headquarters lies in another team; an unfounded one counts as nobody's.</summary>
    private bool Foreign(AiContext ctx, string corporation) =>
        corporations.Headquarters(ctx.Sim, corporation) is { } city && ctx.Sim.State.FindCity(city) is { } hq
        && ctx.Sim.TeamOf(hq.Owner).Id != ctx.Team.Id;
}
