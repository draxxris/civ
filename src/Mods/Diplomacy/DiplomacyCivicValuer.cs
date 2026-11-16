using Ariel.Ai;
using Ariel.Game;

namespace Ariel.Mods.Diplomacy;

/// <summary>What a civic does to how other leaders regard the player, added to the civic planner's score
/// (<see cref="CivicSubject"/>). A leader whose civic carries an <see cref="CivicDiplomacyDef.AttitudeChanges"/> entry
/// for this civic likes or dislikes a player running it; that attitude decides the deals the leader accepts, the
/// agreements it signs and whether it picks the player as a war target. Each point of it, from each computer leader
/// the player's team has met that still holds a city, is worth <see cref="PointsPerAttitude"/>, on the planner's
/// scale. Only the leaders that judge count: a human's attitude decides nothing.
/// <para>A civic's own entries and its <see cref="CivicDiplomacyDef.GlobalRelationsChange"/> are not priced. They change
/// how the player's own leader regards others, which steers its own diplomacy but gains or costs its empire
/// nothing.</para></summary>
internal sealed class DiplomacyCivicValuer : IAiValuer<CivicSubject>
{
    /// <summary>Points per point of attitude from one leader. Attitude tiers are 3 and 10 points wide, so −4 from one
    /// leader (about a tier) costs 20, more than the 15 a civic needs over another to be preferred.</summary>
    internal const int PointsPerAttitude = 5;

    public string Id => "diplomacy:civic_valuer";

    public int Adjust(AiContext ctx, in CivicSubject subject)
    {
        var sim = ctx.Sim;
        string civic = subject.Civic.Id;
        var relations = sim.Rules().Relations;
        long attitude = 0;
        foreach (var leader in sim.State.Players)
        {
            if (leader.Ai is null || leader.TeamId == ctx.Self.TeamId || !relations.HasMet(sim, ctx.Self.TeamId, leader.TeamId)
                || !sim.State.Cities.Any(c => c.Owner == leader.Id)) continue;
            foreach (var held in leader.Civics.Values)
                if (sim.Content.Attachment<CivicDiplomacyDef>(held) is { } rules && rules.AttitudeChanges.TryGetValue(civic, out var effect))
                    attitude += effect.Change;
        }
        return (int)Math.Clamp(attitude * PointsPerAttitude, -AiValuation.Limit, AiValuation.Limit);
    }
}
