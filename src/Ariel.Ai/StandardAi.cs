using Ariel.Content;
using Ariel.Game;

namespace Ariel.Ai;

/// <summary>The standard computer player (<c>ai:standard</c>).
/// Planning is sequential, so each decision sees the effect of the previous one: commands go through
/// <see cref="Simulation.TryApply"/> (the normal validate/apply path) inside <see cref="Decide"/>, and nothing
/// is returned for the engine to apply again. A turn runs every registered <see cref="IAiPlanner"/>, the AI's own
/// and those mods contribute, in <see cref="IAiPlanner.Order"/>. Retained goals and missions live in the saved player
/// component, not on this shared service. Every decision depends on authoritative state and <c>sim.Rng</c>.</summary>
public sealed class StandardAi : IPlayerAI
{
    public string Id => "ai:standard";

    public IEnumerable<GameCommand> Decide(Simulation sim, int playerId)
    {
        var ctx = new AiContext(sim, playerId);
        if (ctx.Self.Extensions.Get<AiPlayerState>(sim.Content) is not null)
            ctx.Issue(new PruneAiIntentCommand { Player = playerId });
        if (ctx.Cities.Count == 0 && ctx.Units.Count == 0) return [];
        foreach (var planner in ctx.Catalog.Planners) planner.Plan(ctx);
        return [];
    }
}
