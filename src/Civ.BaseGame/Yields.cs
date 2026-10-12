using Civ.Game;

namespace Civ.BaseGame;

public readonly record struct Yield(int Food, int Production, int Commerce, int Culture = 0)
{
    public static Yield operator +(Yield a, Yield b) =>
        new(a.Food + b.Food, a.Production + b.Production, a.Commerce + b.Commerce, a.Culture + b.Culture);

    public static Yield operator *(Yield a, int k) => new(a.Food * k, a.Production * k, a.Commerce * k, a.Culture * k);
}

public static class Yields
{
    public static Yield OfTile(Simulation sim, Hex hex)
    {
        if (!sim.State.Terrain.ContainsKey(hex)) return default;
        var t = sim.TerrainAt(hex);
        return new Yield(t.Food, t.Production, t.Commerce);
    }

    /// <summary>Flat +1 production/commerce floor, the free city centre, worked tiles, specialists and buildings.</summary>
    public static Yield OfCity(Simulation sim, City city)
    {
        var total = new Yield(0, 1, 1) + OfTile(sim, city.Position);
        foreach (var hex in city.Worked) total += OfTile(sim, hex);
        foreach (var (id, count) in city.Specialists)
        {
            var s = sim.Content.Specialists.Get(id);
            total += new Yield(s.Food, s.Production, s.Commerce, s.Culture) * count;
        }
        foreach (var id in city.Buildings)
        {
            var b = sim.Content.Buildings.Get(id);
            total += new Yield(b.Food, b.Production, b.Commerce, b.Culture);
        }
        return total;
    }
}

/// <summary>Percent bonuses a player gets from its civilization and held civics.</summary>
public static class Bonuses
{
    private static IEnumerable<Content.CivicDef> Civics(Simulation sim, int playerId) =>
        sim.State.PlayerById(playerId).Civics.Values.Select(id => sim.Content.Civics.Get(id));

    public static int Research(Simulation sim, int playerId) =>
        (sim.CivilizationOf(playerId)?.ResearchBonus ?? 0) + Civics(sim, playerId).Sum(c => c.ResearchBonus);
    public static int Production(Simulation sim, int playerId) =>
        (sim.CivilizationOf(playerId)?.ProductionBonus ?? 0) + Civics(sim, playerId).Sum(c => c.ProductionBonus);
    public static int Culture(Simulation sim, int playerId) =>
        (sim.CivilizationOf(playerId)?.CultureBonus ?? 0) + Civics(sim, playerId).Sum(c => c.CultureBonus);
}
