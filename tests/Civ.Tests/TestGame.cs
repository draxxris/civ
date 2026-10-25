using Civ.BaseGame;
using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Tests;

internal static class TestGame
{
    /// <summary>Base game loaded the same way any mod is: manifest + assembly + sidecar data.</summary>
    public static ContentDb Content()
    {
        var src = ModLoader.FromAssembly(typeof(TestRulesetMod).Assembly, AppContext.BaseDirectory);
        return ModLoader.Build([src]);
    }

    public static Simulation Sim(ulong seed = 7, int players = 2) =>
        new(Content(), new SimulationSettings
        {
            Seed = seed, PlayerCount = players, Map = new(17, 17),
            Civilizations = Enumerable.Repeat("base:builders", players).ToArray(),
        });

    /// <summary>Land a unit can stand on: on the map, not water, not impassable.</summary>
    public static bool IsLand(Simulation sim, Hex hex) =>
        sim.State.Grid.Contains(hex) && sim.TerrainAt(hex) is { IsWater: false, Passable: true };

    /// <summary>First land tile in scan order, optionally one that also satisfies <paramref name="where"/>.</summary>
    public static Hex FindLand(Simulation sim, Func<Hex, bool>? where = null) =>
        sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf).OrderBy(h => h.Q).ThenBy(h => h.R)
            .Where(h => IsLand(sim, h) && (where is null || where(h)))
            .First();
}
