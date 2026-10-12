using Civ.BaseGame;
using Civ.Game;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Civ.Client;

public enum TileMark { None, Worked, Workable, Selected }

/// <summary>Turns simulation state into Prowl game objects: hex prisms, cities and units. Read-only over the simulation.</summary>
public sealed class WorldView
{
    public const float HexSize = 1f;
    private const float Thickness = 0.4f;

    private readonly Simulation _sim;
    private readonly Scene _scene;
    private readonly Mesh _hexMesh = Mesh.CreateCylinder(HexSize * 0.96f, Thickness, 6);
    private readonly Mesh _cityMesh = Mesh.CreateCube(new Float3(0.9f, 0.6f, 0.9f));
    private readonly Mesh _unitMesh = Mesh.CreateCylinder(0.3f, 0.8f, 10);

    private sealed class TileView
    {
        public required Material Material;
        public Color Base;
        public TileMark Mark = TileMark.None;
    }

    private readonly Dictionary<Hex, TileView> _tiles = new();
    private readonly Dictionary<int, GameObject> _cities = new();
    private readonly Dictionary<int, GameObject> _units = new();

    public WorldView(Simulation sim, Scene scene)
    {
        _sim = sim;
        _scene = scene;
        foreach (var (hex, terrainId) in sim.State.Terrain)
        {
            var terrain = sim.Content.Terrains.Get(terrainId);
            var (x, z) = hex.ToPixel(HexSize);
            var go = new GameObject($"tile {hex.Q},{hex.R}");
            go.Transform.Position = new Float3((float)x, terrain.IsWater ? -0.35f : -Thickness / 2, (float)z);
            go.Transform.LocalEulerAngles = new Float3(0, 30, 0); // cylinder vertices start on +X; pointy-top needs +Z
            var mr = go.AddComponent<MeshRenderer>();
            mr.Mesh = _hexMesh;
            var mat = new Material(Shader.LoadDefault(DefaultShader.Standard));
            var color = ParseColor(terrain.Color);
            mat.SetColor("_MainColor", color);
            mr.Material = mat;
            scene.Add(go);
            _tiles[hex] = new TileView { Material = mat, Base = color };
        }
    }

    public static Float3 WorldOf(Hex hex, float y = 0)
    {
        var (x, z) = hex.ToPixel(HexSize);
        return new Float3((float)x, y, (float)z);
    }

    /// <summary>Ray-plane test against the tile surface; returns the hex under the pointer, or null off the map.</summary>
    public Hex? Pick(Float3 origin, Float3 direction)
    {
        if (MathF.Abs(direction.Y) < 1e-5f) return null;
        float t = (0f - origin.Y) / direction.Y;
        if (t < 0) return null;
        var p = origin + direction * t;
        var hex = Hex.FromPixel(p.X, p.Z, HexSize);
        return _tiles.ContainsKey(hex) ? hex : null;
    }

    public void Sync(Func<Hex, TileMark> mark, int? selectedUnit, int? selectedCity)
    {
        foreach (var (hex, view) in _tiles)
        {
            var m = mark(hex);
            if (m == view.Mark) continue;
            view.Mark = m;
            view.Material.SetColor("_MainColor", m switch
            {
                TileMark.Worked => Lerp(view.Base, new Color(1f, 0.35f, 0.05f, 1f), 0.65f),
                TileMark.Workable => Lerp(view.Base, new Color(1f, 1f, 1f, 1f), 0.35f),
                TileMark.Selected => Lerp(view.Base, new Color(1f, 1f, 1f, 1f), 0.7f),
                _ => view.Base,
            });
        }

        Reconcile(_cities, _sim.State.Cities.Select(c => c.Id), id =>
        {
            var c = _sim.State.Cities.First(x => x.Id == id);
            return MakeObject($"city {id}", _cityMesh, OwnerColor(c.Owner));
        });
        foreach (var city in _sim.State.Cities)
        {
            var go = _cities[city.Id];
            go.Transform.Position = WorldOf(city.Position, 0.25f);
            go.Transform.LocalScale = new Float3(1f, 1f + 0.25f * (city.Population - 1), 1f);
        }

        Reconcile(_units, _sim.State.Units.Select(u => u.Id), id =>
        {
            var u = _sim.State.Units.First(x => x.Id == id);
            return MakeObject($"unit {id}", _unitMesh, OwnerColor(u.Owner));
        });
        foreach (var group in _sim.State.Units.GroupBy(u => u.Position))
        {
            int i = 0;
            foreach (var unit in group.OrderBy(u => u.Id))
            {
                var go = _units[unit.Id];
                var p = WorldOf(unit.Position, 0.35f);
                go.Transform.Position = p + new Float3(0.3f * i - 0.15f * (group.Count() - 1), 0, -0.35f);
                float s = unit.Id == selectedUnit ? 1.4f : 1f;
                go.Transform.LocalScale = new Float3(s, s, s);
                i++;
            }
        }
    }

    private GameObject MakeObject(string name, Mesh mesh, Color color)
    {
        var go = new GameObject(name);
        var mr = go.AddComponent<MeshRenderer>();
        mr.Mesh = mesh;
        var mat = new Material(Shader.LoadDefault(DefaultShader.Standard));
        mat.SetColor("_MainColor", color);
        mr.Material = mat;
        _scene.Add(go);
        return go;
    }

    private static void Reconcile(Dictionary<int, GameObject> views, IEnumerable<int> ids, Func<int, GameObject> create)
    {
        var live = ids.ToHashSet();
        foreach (var id in views.Keys.Where(k => !live.Contains(k)).ToList())
        {
            views[id].Destroy();
            views.Remove(id);
        }
        foreach (var id in live)
            if (!views.ContainsKey(id)) views[id] = create(id);
    }

    public Color OwnerColor(int player)
    {
        var civ = _sim.CivilizationOf(player);
        return ParseColor(civ?.Color ?? "#cccccc");
    }

    public static Color ParseColor(string hex)
    {
        int v = Convert.ToInt32(hex.TrimStart('#'), 16);
        // The UI colors are sRGB; the renderer works in linear space and tonemaps, so convert or everything looks washed out.
        static float Lin(int c) => MathF.Pow(c / 255f, 2.2f);
        return new Color(Lin((v >> 16) & 255), Lin((v >> 8) & 255), Lin(v & 255), 1f);
    }

    private static Color Lerp(Color a, Color b, float t) =>
        new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, 1f);
}
