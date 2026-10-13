using Civ.Game;
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

    public GameSession Session => _session;
    public GameUi Ui => _ui;

    public void DebugZoom(float delta) => _camera.Zoom(delta);

    private Hex? _forcedHover;

    public void ForceHover(Hex? hex) => _forcedHover = hex;

    public GameView(GameSession session, IconCache icons) { _session = session; _icons = icons; }

    public override void _Ready()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.03f, 0.04f, 0.06f) },
        });
        _map = new MapRenderer(_session, _icons);
        AddChild(_map);
        _camera = new CameraRig(_map.Period, _map.MapDepth);
        AddChild(_camera);
        _ui = new GameUi(_session, _icons);
        AddChild(_ui);
        var start = _session.Unit?.Position ?? _session.Sim.State.Cities.FirstOrDefault(c => c.Owner == GameSession.Human)?.Position ?? new Hex(0, 0);
        _camera.FocusOn(MapRenderer.WorldOf(start));
    }

    public override void _Process(double delta)
    {
        if (!_ui.TextInputFocused) _camera.Move(PanDirection(), delta);
        var city = _session.City;
        _camera.FollowCity(city?.Id, city == null ? Vector3.Zero : MapRenderer.WorldOf(city.Position));
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
        if (Mathf.Abs(direction.Y) < 1e-5f) return null;
        float t = -origin.Y / direction.Y;
        if (t < 0) return null;
        var p = origin + direction * t;
        return _session.Pick(p.X, p.Z);
    }

    private void UpdateHover()
    {
        var mouse = GetViewport().GetMousePosition();
        Hex? hex = HoveredHex(mouse);
        if (hex == _hover && _hoverRevision == _session.Revision) { _ui.MoveHoverTip(mouse); return; }
        _hover = hex; _hoverRevision = _session.Revision;
        _map.ShowHover(hex, _camera.Focus.X);
        var preview = HoverPreview(hex);
        _map.ShowPath(_session.City == null ? _session.Unit : null, preview);
        _ui.SetHover(hex is Hex t ? _session.DescribeTile(t) : null, preview?.Summary);
        _ui.MoveHoverTip(mouse);
    }

    private Hex? HoveredHex(Vector2 mouse) => _forcedHover ?? (_ui.PointerOverUi ? null : PickAt(mouse));

    private MovePreview? HoverPreview(Hex? hex)
    {
        if (hex is not Hex h || _session.City != null || _session.Unit is not { } unit || unit.Position == h) return null;
        return _session.PreviewAt(h);
    }

    public override void _UnhandledInput(InputEvent e)
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
        switch (key)
        {
            case Key.Enter or Key.KpEnter or Key.Space: _session.EndTurn(); break;
            case Key.Escape: if (_ui.CloseDialogs()) break; _session.ClearSelection(); break;
            case Key.Tab: _session.SelectNextUnit(); FocusSelection(); break;
            case Key.F: _session.RunAction("fortify"); break;
            case Key.B: _session.RunAction("found"); break;
            case Key.R: _ui.ToggleResearch(); break;
            case Key.C: _ui.ToggleGovernment(); break;
        }
    }

    private void FocusSelection()
    {
        if (_session.Unit is { } unit) _camera.FocusOn(MapRenderer.WorldOf(unit.Position));
    }

    private void OnMouseButton(InputEventMouseButton mouse)
    {
        if (mouse.ButtonIndex == MouseButton.Middle) { _dragging = mouse.Pressed; return; }
        if (!mouse.Pressed) return;
        switch (mouse.ButtonIndex)
        {
            case MouseButton.WheelUp: _camera.Zoom(-1.5f); break;
            case MouseButton.WheelDown: _camera.Zoom(1.5f); break;
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
