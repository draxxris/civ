using Godot;

namespace Civ.Client;

/// <summary>
/// Godot entry point: camera, lighting, input, and the two views. All game logic is in <see cref="GameSession"/> and the
/// mods; this node only draws state and turns input into session calls.
/// </summary>
public partial class Main : Node3D
{
    private GameSession _session = null!;
    private WorldView _world = null!;
    private GameUi _ui = null!;
    private Camera3D _camera = null!;

    private Vector3 _focus;
    private float _distance = 22f;

    public override void _Ready()
    {
        var mods = new List<Civ.Modding.ModSource>();
        foreach (var root in ModRoots())
        {
            mods.AddRange(ModDiscovery.LoadMods(root));
            foreach (var pack in ModDiscovery.FindPacks(root))
                if (!ProjectSettings.LoadResourcePack(pack)) GD.PushWarning($"Could not load resource pack {pack}");
        }
        _session = GameSession.Start(mods);

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.08f, 0.1f, 0.14f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.45f,
            },
        });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, 20, 0), ShadowEnabled = true });

        _camera = new Camera3D { Far = 200f, Fov = 50f };
        AddChild(_camera);

        _world = new WorldView(_session.Sim, this);
        _ui = new GameUi(_session);
        AddChild(_ui);

        if (_session.Unit is { } start) _focus = WorldView.WorldOf(start.Position);
        UpdateCamera();
    }

    /// <summary>Folders searched for mods: next to the game, and in the per-user data folder.</summary>
    private static IEnumerable<string> ModRoots()
    {
        yield return System.IO.Path.Combine(System.AppContext.BaseDirectory, "mods");
        yield return System.IO.Path.Combine(OS.GetUserDataDir(), "mods");
    }

    public override void _Process(double delta)
    {
        float speed = _distance * 0.8f * (float)delta;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) _focus += new Vector3(0, 0, -speed);
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) _focus += new Vector3(0, 0, speed);
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) _focus += new Vector3(-speed, 0, 0);
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) _focus += new Vector3(speed, 0, 0);
        UpdateCamera();

        _world.Sync(_session);
        _ui.Refresh();
    }

    private void UpdateCamera()
    {
        const float pitch = 60f;
        float rad = Mathf.DegToRad(pitch);
        _camera.Position = _focus + new Vector3(0, _distance * Mathf.Sin(rad), _distance * Mathf.Cos(rad));
        _camera.LookAt(_focus, Vector3.Up);
    }

    // Only input the UI did not consume reaches here, so clicks on panels never hit the map.
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space) _session.EndTurn();
            else if (key.Keycode == Key.Escape) _session.ClearSelection();
        }
        else if (e is InputEventMouseButton { Pressed: true } mouse)
        {
            switch (mouse.ButtonIndex)
            {
                case MouseButton.WheelUp: _distance = Mathf.Clamp(_distance - 1.5f, 6f, 40f); break;
                case MouseButton.WheelDown: _distance = Mathf.Clamp(_distance + 1.5f, 6f, 40f); break;
                case MouseButton.Left or MouseButton.Right:
                    var origin = _camera.ProjectRayOrigin(mouse.Position);
                    var direction = _camera.ProjectRayNormal(mouse.Position);
                    if (_world.Pick(origin, direction) is not Hex hex) break;
                    if (mouse.ButtonIndex == MouseButton.Left) _session.LeftClick(hex);
                    else _session.RightClick(hex);
                    break;
            }
        }
    }
}
