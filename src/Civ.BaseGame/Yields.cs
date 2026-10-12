using Civ.Game;

namespace Civ.BaseGame;

public readonly record struct Yield(int Food, int Production, int Commerce)
{
    public static Yield operator +(Yield a, Yield b) => new(a.Food + b.Food, a.Production + b.Production, a.Commerce + b.Commerce);
}

public static class Yields
{
    /// <summary>City center + worked radius-1 tiles + buildings, plus a flat +1 production/commerce floor for the center.</summary>
    public static Yield OfCity(Simulation sim, City city)
    {
        var total = new Yield(0, 1, 1);
        foreach (var hex in Hex.Range(city.Position, 1))
        {
            if (!sim.State.Terrain.ContainsKey(hex)) continue;
            var t = sim.TerrainAt(hex);
            total += new Yield(t.Food, t.Production, t.Commerce);
        }
        foreach (var id in city.Buildings)
        {
            var b = sim.Content.Buildings.Get(id);
            total += new Yield(b.Food, b.Production, b.Commerce);
        }
        return total;
    }
}
