using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Per-turn read-only snapshot of one city's economy, used to value what it should build.</summary>
internal sealed record CityView(City City, YieldVector Yields, int HappyMargin, int HealthMargin, int Threat, int Garrison)
{
    public int Production => (int)Math.Max(1, Yields.Production);
    public int Food => (int)Yields.Food;
    public int Commerce => (int)Yields.Commerce;
    public int Population => City.Population;

    public static CityView Build(AiContext ctx, City city)
    {
        var yield = ctx.Service<ICityYieldService>().Calculate(ctx.Sim, city.Id);
        var well = ctx.Service<ICityWellbeingService>().Calculate(ctx.Sim, city.Id);
        int garrison = ctx.Sim.State.UnitsAt(city.Position).Count(u => u.Owner == ctx.PlayerId && ctx.Role(u) == UnitRole.Melee);
        return new(city, yield.Yields, well.Happy - well.Unhappy - well.Angry, well.Healthy - well.Unhealthy,
            ctx.ThreatAround(city.Position, 3), garrison);
    }
}
