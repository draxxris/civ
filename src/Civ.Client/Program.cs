using Civ.BaseGame;
using Civ.Game;
using Civ.Modding;
using Prowl.Runtime;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Civ.Client;

internal static class Program
{
    // NOTE: written against Prowl's SimpleCube sample; not yet compiled in CI (needs the engine checkout).
    private static void Main() => new CivGame().Run("Civ", 1280, 720);
}

/// <summary>
/// Thin view over the headless simulation: reads state and draws it, never mutates it except by sending commands.
/// Today it renders the terrain as a field of hex prisms colored by the terrain def.
/// </summary>
public sealed class CivGame : Prowl.Runtime.Game
{
    private Simulation _sim = null!;

    public override void Initialize()
    {
        // The base game is loaded exactly like any other mod. Extra mods would be added here from a mods/ folder.
        var baseMod = ModLoader.FromAssembly(
            new ModManifest { Id = "base", Assembly = "Civ.BaseGame.dll" },
            typeof(BaseGameMod).Assembly, AppContext.BaseDirectory);
        var content = ModLoader.Build([baseMod]);
        _sim = new Simulation(content, new SimulationSettings { Seed = 1, MapRadius = 12, PlayerCount = 2 });

        var scene = new Scene();

        var light = new GameObject("Sun");
        light.AddComponent<DirectionalLight>();
        light.Transform.LocalEulerAngles = new Float3(-60, 20, 0);
        scene.Add(light);

        var cam = new GameObject("Camera");
        cam.Tag = "Main Camera";
        cam.Transform.Position = new Float3(0, 30, -18);
        cam.Transform.LocalEulerAngles = new Float3(60, 0, 0);
        cam.AddComponent<Camera>();
        scene.Add(cam);

        const float size = 1f;
        var mesh = Mesh.CreateCylinder(size * 0.95f, 0.4f, 6);
        foreach (var (hex, terrainId) in _sim.State.Terrain)
        {
            var terrain = _sim.Content.Terrains.Get(terrainId);
            var (x, z) = hex.ToPixel(size);

            var go = new GameObject($"{hex.Q},{hex.R}");
            go.Transform.Position = new Float3((float)x, terrain.IsWater ? -0.2f : 0f, (float)z);
            var mr = go.AddComponent<MeshRenderer>();
            mr.Mesh = mesh;
            var mat = new Material(Shader.LoadDefault(DefaultShader.Standard));
            mat.SetColor("_MainColor", ParseColor(terrain.Color));
            mr.Material = mat;
            scene.Add(go);
        }

        Scene.Load(scene);
    }

    private static Color ParseColor(string hex)
    {
        int v = Convert.ToInt32(hex.TrimStart('#'), 16);
        return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
    }
}
