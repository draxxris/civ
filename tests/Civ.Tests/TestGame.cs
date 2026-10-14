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

    public static Hex FindLand(Simulation sim, int skip = 0) =>
        sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf).OrderBy(h => h.Q).ThenBy(h => h.R)
            .Where(h => sim.TerrainAt(h) is { IsWater: false, Passable: true })
            .Skip(skip).First();
}
