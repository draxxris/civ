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
        var manifest = new ModManifest { Id = "base", Name = "Base Game", Assembly = "Civ.BaseGame.dll" };
        var src = ModLoader.FromAssembly(manifest, typeof(BaseGameMod).Assembly, AppContext.BaseDirectory);
        return ModLoader.Build([src]);
    }

    public static Simulation Sim(ulong seed = 7, int players = 2) =>
        new(Content(), new SimulationSettings { Seed = seed, PlayerCount = players, MapRadius = 8 });

    public static Hex FindLand(Simulation sim, int skip = 0) =>
        sim.State.Terrain.OrderBy(k => k.Key.Q).ThenBy(k => k.Key.R)
            .Where(k => !sim.Content.Terrains.Get(k.Value).IsWater && sim.Content.Terrains.Get(k.Value).Passable)
            .Skip(skip).First().Key;
}
