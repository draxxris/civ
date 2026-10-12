using Civ.BaseGame;
using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>Turns simulation state into Godot nodes: hex prisms, cities and units. Read-only over the simulation.</summary>
public sealed class WorldView
{
    public const float HexSize = 1f;
    private const float Thickness = 0.4f;

    private readonly Simulation _sim;
    private readonly Node3D _root;
    private readonly Mesh _hexMesh = new CylinderMesh { TopRadius = HexSize * 0.96f, BottomRadius = HexSize * 0.96f, Height = Thickness, RadialSegments = 6, Rings = 1 };
    private readonly Mesh _cityMesh = new BoxMesh { Size = new Vector3(0.9f, 0.6f, 0.9f) };
    private readonly Mesh _unitMesh = new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.32f, Height = 0.8f, RadialSegments = 12 };

    private sealed class TileView
    {
        public required StandardMaterial3D Material;
        public Color Base;
        public TileMark Mark;
    }

    private readonly Dictionary<Hex, TileView> _tiles = new();
    private readonly Dictionary<int, MeshInstance3D> _cities = new();
    private readonly Dictionary<int, MeshInstance3D> _units = new();

    public WorldView(Simulation sim, Node3D root)
    {
        _sim = sim;
        _root = root;
        foreach (var (hex, terrainId) in sim.State.Terrain)
        {
            var terrain = sim.Content.Terrains.Get(terrainId);
            var color = Color.FromHtml(terrain.Color);
            var material = new StandardMaterial3D { AlbedoColor = color };
            var node = new MeshInstance3D { Mesh = _hexMesh, MaterialOverride = material, Name = $"tile {hex.Q},{hex.R}" };
            var (x, z) = hex.ToPixel(HexSize);
            node.Position = new Vector3((float)x, terrain.IsWater ? -0.35f : -Thickness / 2, (float)z);
            root.AddChild(node);
            _tiles[hex] = new TileView { Material = material, Base = color };
        }
    }

    public static Vector3 WorldOf(Hex hex, float y = 0)
    {
        var (x, z) = hex.ToPixel(HexSize);
        return new Vector3((float)x, y, (float)z);
    }

    /// <summary>Hex under a ray (origin + direction) where it crosses the tile surface, or null off the map.</summary>
    public Hex? Pick(Vector3 origin, Vector3 direction)
    {
        if (Mathf.Abs(direction.Y) < 1e-5f) return null;
        float t = -origin.Y / direction.Y;
        if (t < 0) return null;
        var p = origin + direction * t;
        var hex = Hex.FromPixel(p.X, p.Z, HexSize);
        return _tiles.ContainsKey(hex) ? hex : null;
    }

    public void Sync(GameSession session)
    {
        foreach (var (hex, view) in _tiles)
        {
            var mark = session.MarkOf(hex);
            if (mark == view.Mark) continue;
            view.Mark = mark;
            view.Material.AlbedoColor = mark switch
            {
                TileMark.Worked => view.Base.Lerp(new Color(1f, 0.55f, 0.1f), 0.65f),
                TileMark.Workable => view.Base.Lerp(Colors.White, 0.35f),
                TileMark.Selected => view.Base.Lerp(Colors.White, 0.7f),
                _ => view.Base,
            };
        }

        Reconcile(_cities, _sim.State.Cities.Select(c => c.Id), id => MakeNode($"city {id}", _cityMesh, OwnerColor(_sim.State.Cities.First(c => c.Id == id).Owner)));
        foreach (var city in _sim.State.Cities)
        {
            var node = _cities[city.Id];
            node.Position = WorldOf(city.Position, 0.3f);
            node.Scale = new Vector3(1f, 1f + 0.25f * (city.Population - 1), 1f);
        }

        Reconcile(_units, _sim.State.Units.Select(u => u.Id), id => MakeNode($"unit {id}", _unitMesh, OwnerColor(_sim.State.Units.First(u => u.Id == id).Owner)));
        foreach (var group in _sim.State.Units.GroupBy(u => u.Position))
        {
            int i = 0, count = group.Count();
            foreach (var unit in group.OrderBy(u => u.Id))
            {
                var node = _units[unit.Id];
                node.Position = WorldOf(unit.Position, 0.4f) + new Vector3(0.3f * i - 0.15f * (count - 1), 0, -0.35f);
                float s = unit.Id == session.SelectedUnitId ? 1.4f : 1f;
                node.Scale = new Vector3(s, s, s);
                i++;
            }
        }
    }

    private MeshInstance3D MakeNode(string name, Mesh mesh, Color color)
    {
        var node = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = new StandardMaterial3D { AlbedoColor = color } };
        _root.AddChild(node);
        return node;
    }

    private static void Reconcile(Dictionary<int, MeshInstance3D> views, IEnumerable<int> ids, Func<int, MeshInstance3D> create)
    {
        var live = ids.ToHashSet();
        foreach (var id in views.Keys.Where(k => !live.Contains(k)).ToList())
        {
            views[id].QueueFree();
            views.Remove(id);
        }
        foreach (var id in live)
            if (!views.ContainsKey(id)) views[id] = create(id);
    }

    private Color OwnerColor(int player) => Color.FromHtml(_sim.CivilizationOf(player)?.Color ?? "#cccccc");
}
