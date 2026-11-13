using Ariel.Ai;
using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>What a civic's corporation rules are worth to the computer player, added to the civic planner's score
/// (<see cref="CivicSubject"/>). The base AI knows neither the <see cref="CivicCorporationsDef"/> attachment nor the
/// <c>corporations:maintenance_percent</c> key; this mod, which reads both, values them. Everything is weighed by the
/// branches already open in the player's own cities, so a civic means nothing to corporations before any is founded.
/// <list type="bullet">
/// <item>A civic that bans corporations costs what the branches produce (their yields and commerce, in
/// <see cref="PlannerPoints"/>) and saves their maintenance; one that bans foreign corporations costs what the branches
/// of other teams' corporations produce.</item>
/// <item>A change to maintenance gains or costs that share of the branches' maintenance, as gold.</item>
/// </list></summary>
internal sealed class CorporationCivicValuer(ICorporationService corporations) : IAiValuer<CivicSubject>
{
    public string Id => "corporations:civic_valuer";

    public int Adjust(AiContext ctx, in CivicSubject subject)
    {
        var sim = ctx.Sim;
        var civic = subject.Civic;
        var rules = sim.Content.Attachment<CivicCorporationsDef>(civic.Id);
        bool realistic = sim.IsOptionEnabled("foundation:gameoption_realistic_corporations");
        long maintenancePercent = civic.Effects.Total(ModifierScope.Player, CorporationService.MaintenanceKey)
            + (rules is null ? 0 : realistic ? rules.RealisticMaintenancePercent : rules.MaintenancePercent);
        bool banned = rules?.NoCorporations == true, foreignBanned = rules?.NoForeignCorporations == true;
        if (!banned && !foreignBanned && maintenancePercent == 0) return 0;

        long points = 0;
        foreach (var city in ctx.Cities)
        {
            var present = corporations.Present(sim, city.Id);
            if (present.Count == 0) continue;
            foreach (var corporation in present)
            {
                if (!banned && !(foreignBanned && Foreign(ctx, corporation))) continue;
                var def = sim.Content.Get<CorporationDef>(corporation);
                long copies = corporations.ConsumedBonusCount(sim, city.Id, corporation);
                points -= (PlannerPoints.Yields(def.YieldProduced) + PlannerPoints.Commerce(def.CommerceProduced)) * copies;
            }
            // Maintenance is in hundredths of gold. A ban ends it with the branches; a cut is a gain.
            long maintenance = corporations.MaintenanceHundredths(sim, city.Id);
            if (banned) points += maintenance * PlannerPoints.Gold / 100;
            else if (maintenancePercent != 0) points -= maintenance * maintenancePercent / 100 * PlannerPoints.Gold / 100;
        }
        return (int)Math.Clamp(points, -AiValuation.Limit, AiValuation.Limit);
    }

    /// <summary>Whether the corporation's headquarters lies in another team; an unfounded one counts as nobody's.</summary>
    private bool Foreign(AiContext ctx, string corporation) =>
        corporations.Headquarters(ctx.Sim, corporation) is { } city && ctx.Sim.State.FindCity(city) is { } hq
        && ctx.Sim.TeamOf(hq.Owner).Id != ctx.Team.Id;
}
