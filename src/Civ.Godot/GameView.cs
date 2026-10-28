using Civ.Game;
using Civ.Client.Terrain;
using Godot;

namespace Civ.Client;

/// <summary>The in-game screen: map renderer, camera, UI, and the translation of mouse and keys into session calls.</summary>
internal sealed partial class GameView : Node3D
{
    private readonly GameSession _session;
    private readonly IconCache _icons;
    private MapRenderer _map = null!;
    private CameraRig _camera = null!;
    private GameUi _ui = null!;
    private Hex? _hover;
    private int _hoverRevision = -1;
    private bool _dragging;
    private (Vector2 Mouse, Transform3D Camera, float Fov, Vector2 ViewSize, int WorldRevision, bool OverUi, Hex? Forced)? _pickInputs;
    private Hex? _pickedHex;

    public GameSession Session => _session;
    public GameUi Ui => _ui;

    public void DebugZoom(float delta) => _camera.Zoom(delta);

    public void DebugFocus(Vector3 world) => _camera.FocusOn(world);

    public void DebugView(float? pitch, float? yaw, float? distance) => _camera.SetView(pitch, yaw, distance);

    public TerrainRenderer Terrain => _map.Terrain;
    public MapRenderer Map => _map;

    /// <summary>Capture aid: hide the HUD, tooltips, hover and path previews, units, cities and labels.</summary>
    public void SetClean()
    {
        _clean = true;
        _ui.Visible = false;
        _map.Clean = true;
        _map.ShowHover(null);
        _map.ShowPath(null, null);
    }

    private bool _clean;

    private Hex? _forcedHover;

    public void ForceHover(Hex? hex) => _forcedHover = hex;

    public GameView(GameSession session, IconCache icons) { _session = session; _icons = icons; }

    public override void _Ready()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.03f, 0.04f, 0.06f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 0.45f },
        });
        _map = new MapRenderer(_session, _icons);
        AddChild(_map);
        _camera = new CameraRig(_map.Period, _map.MapDepth);
        AddChild(_camera);
        _ui = new GameUi(_session, _icons);
        AddChild(_ui);
        var start = _session.Unit?.Position ?? _session.Sim.State.Cities.FirstOrDefault(c => c.Owner == GameSession.Human)?.Position ?? new Hex(0, 0);
        _camera.FocusOn(MapRenderer.PositionOf(start));
    }

    public override void _Process(double delta)
    {
        if (!_ui.TextInputFocused) _camera.Move(PanDirection(), delta);
        var city = _session.City;
        _camera.FollowCity(city?.Id, city == null ? Vector3.Zero : MapRenderer.PositionOf(city.Position));
        _map.Refresh();
        _map.WrapLabels(_camera.Focus.X);
        _ui.Refresh();
        UpdateHover();
    }

    private static Vector2 PanDirection()
    {
        var d = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) d.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) d.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) d.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) d.X += 1;
        return d;
    }

    /// <summary>The tile under a screen point, or null off the map.</summary>
    private Hex? PickAt(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        if (_map.TerrainField is { } field)
        {
            var hit = TerrainRay.Pick(new System.Numerics.Vector3(origin.X, origin.Y, origin.Z),
                new System.Numerics.Vector3(direction.X, direction.Y, direction.Z), field, includeWaterSurface: true);
            if (hit is { } point) return _session.Pick(point.X, point.Z);
        }
        if (Mathf.Abs(direction.Y) < 1e-5f) return null;
        float t = -origin.Y / direction.Y;
        if (t < 0) return null;
        var p = origin + direction * t;
        return _session.Pick(p.X, p.Z);
    }

    private void UpdateHover()
    {
        if (_clean) return;
        var mouse = GetViewport().GetMousePosition();
        Hex? hex = HoveredHex(mouse);
        if (hex == _hover && _hoverRevision == _session.Revision) { _ui.MoveHoverTip(mouse); return; }
        _hover = hex; _hoverRevision = _session.Revision;
        _map.ShowHover(hex);
        var preview = HoverPreview(hex);
        _map.ShowPath(_session.City == null ? _session.Unit : null, preview);
        _ui.SetHover(hex is Hex t ? _session.DescribeTile(t) : null, preview?.Summary);
        _ui.MoveHoverTip(mouse);
    }

    private Hex? HoveredHex(Vector2 mouse)
    {
        // A stationary pointer still needs a new ray after camera, viewport or terrain changes.
        var inputs = (Mouse: mouse, Camera: _camera.GlobalTransform, Fov: _camera.Fov,
            ViewSize: GetViewport().GetVisibleRect().Size, WorldRevision: _session.WorldRevision,
            OverUi: _ui.PointerOverUi, Forced: _forcedHover);
        if (_pickInputs == inputs) return _pickedHex;
        _pickInputs = inputs;
        return _pickedHex = _forcedHover ?? (inputs.OverUi ? null : PickAt(mouse));
    }

    private MovePreview? HoverPreview(Hex? hex)
    {
        if (hex is not Hex h || _session.City != null || _session.Unit is not { } unit || unit.Position == h) return null;
        return _session.PreviewAt(h);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        HandleInput(e);
        if (MapRenderer.LogTimings && e is not InputEventMouseMotion)
            GD.Print($"input {e.GetType().Name}: {System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds:F2} ms");
    }

    /// <summary>
    /// Runs before the GUI, so it can give the keyboard back when a click leaves a text field without the field noticing.
    /// Nothing else here consumes input: every map action still goes through <see cref="HandleInput"/>.
    /// </summary>
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true }) _ui.ReleaseTextFocus();
    }

    private void HandleInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false } key: OnKey(key.Keycode); break;
            case InputEventMouseButton mouse: OnMouseButton(mouse); break;
            case InputEventMouseMotion motion when _dragging: _camera.Drag(motion.Relative); break;
        }
    }

    private void OnKey(Key key)
    {
        // A dialog owns the keyboard while it is open: Enter would end the turn behind the player's back, and Tab would
        // swap the unit whose details the dialog is showing. Esc closes the dialog itself, the save and load keys stay
        // useful there, and the hotkey that opened a dialog still closes it.
        if (_ui.DialogsOpen && key is not (Key.Escape or Key.F5 or Key.F9) && !_ui.ClosesOpenDialog(key)) return;
        switch (key)
        {
            case Key.Enter or Key.KpEnter or Key.Space: _session.EndTurn(); break;
            case Key.Escape: if (_ui.CloseDialogs()) break; _session.ClearSelection(); break;
            case Key.Tab: _session.SelectNextUnit(); FocusSelection(); break;
            case Key.F: _session.RunAction("fortify"); break;
            case Key.B: _session.RunAction("found"); break;
            case Key.R: _ui.ToggleResearch(); break;
            case Key.C: _ui.ToggleGovernment(); break;
            case Key.F5: _ui.QuickSaveGame(); break;
            case Key.F9: _ui.QuickLoadGame(); break;
        }
    }

    private void FocusSelection()
    {
        if (_session.Unit is { } unit) _camera.FocusOn(MapRenderer.PositionOf(unit.Position));
    }

    private void OnMouseButton(InputEventMouseButton mouse)
    {
        if (mouse.ButtonIndex == MouseButton.Middle) { _dragging = mouse.Pressed; return; }
        if (!mouse.Pressed) return;
        switch (mouse.ButtonIndex)
        {
            case MouseButton.WheelUp when !_ui.PointerOverUi: _camera.Zoom(-1.5f); break;
            case MouseButton.WheelDown when !_ui.PointerOverUi: _camera.Zoom(1.5f); break;
            case MouseButton.Left or MouseButton.Right: Click(mouse); break;
        }
    }

    private void Click(InputEventMouseButton mouse)
    {
        if (PickAt(mouse.Position) is not Hex hex) return;
        if (mouse.ButtonIndex == MouseButton.Left) _session.LeftClick(hex);
        else _session.RightClick(hex);
    }
}
