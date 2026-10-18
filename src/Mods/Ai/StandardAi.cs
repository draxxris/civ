using Civ.Content;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>The standard computer player (<c>ai:standard</c>).
/// Planning is sequential, so each decision sees the effect of the previous one: commands go through
/// <see cref="Simulation.TryApply"/> (the normal validate/apply path) inside <see cref="Decide"/>, and nothing
/// is returned for the engine to apply again. Retained goals and missions live in the saved player component,
/// not on this shared service. Every decision depends on authoritative state and <c>sim.Rng</c>.</summary>
public sealed class StandardAi : IPlayerAI
{
    public string Id => "ai:standard";

    public IEnumerable<GameCommand> Decide(Simulation sim, int playerId)
    {
        var ctx = new AiContext(sim, playerId);
        if (ctx.Self.Extensions.Get<AiPlayerState>(sim.Content) is not null)
            ctx.Issue(new PruneAiIntentCommand { Player = playerId });
        if (ctx.Cities.Count == 0 && ctx.Units.Count == 0) return [];
        Run(ctx);
        return [];
    }

    private static void Run(AiContext ctx)
    {
        var sites = new SitePlanner(ctx);
        var army = new ArmyPlan(ctx);
        new ResearchPlanner(ctx).Plan();
        new WarPlanner(ctx, army).Plan();
        new CivicPlanner(ctx).Plan();
        new SettlerPlanner(ctx, sites).Plan();
        ctx.Refresh();
        new CitizenPlanner(ctx, army).Plan();
        new ReligionPlanner(ctx).Plan();
        new GreatPersonPlanner(ctx).Plan();
        ctx.Refresh();
        var cities = new CityPlanner(ctx, sites, army);
        cities.Plan();
        new WorkerPlanner(ctx).Plan();
        // Resolve air preparation before naval support and ground assaults.
        new AirPlanner(ctx).Plan();
        var orders = new UnitOrders(ctx);
        new MilitaryPlanner(ctx, army, cities, new AttackPlanner(ctx, orders)).Plan();
        new EconomyPlanner(ctx).Plan();
        new HurryPlanner(ctx, cities).Plan();
    }
}
