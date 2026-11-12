using Ariel.Game;

namespace Ariel.Ai;

/// <summary>Suppresses rebellion in the computer player's own cities: a free garrison when one qualifies, otherwise
/// gold, city id order first. Runs every few turns because describing a city's stability costs a wellbeing ledger.</summary>
internal sealed class StabilityPlanner(AiContext ctx)
{
    internal const int ReviewInterval = 3;

    public void Plan()
    {
        var revolution = ctx.Sim.Rules().Revolution;
        if (revolution is null || ctx.Sim.State.PlayerById(ctx.PlayerId).EndedTurn || ctx.Cities.Count == 0) return;
        if ((ctx.Turn + ctx.PlayerId) % ReviewInterval != 0) return;
        foreach (var city in ctx.Cities.OrderBy(c => c.Id))
        {
            // Cheap pre-filter: a city with no recorded instability cannot be worth suppressing.
            if ((city.Extensions.Get<RevolutionCityState>(ctx.Sim.Content)?.InstabilityIndex ?? 0) <= 0) continue;
            var description = revolution.DescribeCityStability(ctx.Sim, city.Id);
            if (description.Status < CityStabilityStatus.Warning) continue;
            if (Suppress(revolution, city)) continue;
            // Gold may run out mid-loop; stop rather than retrying every city.
            if (ctx.Sim.State.PlayerById(ctx.PlayerId).Treasury < description.SuppressionGold) return;
        }
    }

    private bool Suppress(IRevolutionService revolution, City city)
    {
        int? garrison = null;
        foreach (var unit in ctx.Sim.State.UnitsAt(city.Position).Where(u => u.Owner == ctx.PlayerId).OrderBy(u => u.Id))
            if (revolution.ValidateSuppression(ctx.Sim, ctx.PlayerId, city.Id, unit.Id) is null) { garrison = unit.Id; break; }
        var command = new SuppressRebellionCommand { Player = ctx.PlayerId, CityId = city.Id, Garrison = garrison };
        return command.Validate(ctx.Sim) is null && ctx.Issue(command);
    }
}
