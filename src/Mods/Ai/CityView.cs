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

/// <summary>What a city is best at, from its yields compared with the empire's other cities.</summary>
internal enum CityFocus { General, Production, Military, Science, Commerce }

internal static class CityFocusRules
{
    /// <summary>A city leads with production when it has well above the empire's average production, and becomes the
    /// military city when it also has the empire's top production. High-commerce cities follow the leader's gold or science preference.
    /// Small empires (fewer than three cities) keep a general focus.</summary>
    public static CityFocus Focus(CityView v, IReadOnlyList<CityView> all, bool favorGold)
    {
        if (all.Count < 3) return CityFocus.General;
        long production = all.Sum(c => (long)c.Production), commerce = all.Sum(c => (long)Math.Max(0, c.Commerce));
        bool productive = v.Production * 100L * all.Count >= production * 130;
        bool rich = v.Commerce * 100L * all.Count >= commerce * 130;
        if (productive)
        {
            var top = all.OrderByDescending(c => c.Production).ThenBy(c => c.City.Id).First();
            return top.City.Id == v.City.Id ? CityFocus.Military : CityFocus.Production;
        }
        if (rich) return favorGold ? CityFocus.Commerce : CityFocus.Science;
        return CityFocus.General;
    }
}
