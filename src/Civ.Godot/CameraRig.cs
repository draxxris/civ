using Godot;

namespace Civ.Client;

/// <summary>Tilted strategy camera over the map. The focus wraps east-west on a cylinder map and is clamped at the poles.</summary>
internal sealed partial class CameraRig : Camera3D
{
    private const float Pitch = 62f, MinDistance = 6f, MaxDistance = 44f;

    private readonly float _period;
    private readonly float _depth;
    private float _distance = 18f;
    private float _savedDistance = 18f;
    private int? _focusedCity;

    public Vector3 Focus { get; private set; }

    public CameraRig(float period, float depth)
    {
        _period = period; _depth = depth;
        Far = 300f; Fov = 50f; Current = true;
    }

    public void FocusOn(Vector3 world) { Focus = world; Clamp(); Apply(); }

    public void Zoom(float delta) { _distance = Mathf.Clamp(_distance + delta, MinDistance, MaxDistance); Apply(); }

    /// <summary>Pan by a mouse drag measured in screen pixels.</summary>
    public void Drag(Vector2 pixels)
    {
        float perPixel = 2f * _distance * Mathf.Tan(Mathf.DegToRad(Fov / 2)) / GetViewport().GetVisibleRect().Size.Y;
        Focus += new Vector3(-pixels.X * perPixel, 0, -pixels.Y * perPixel / Mathf.Sin(Mathf.DegToRad(Pitch)));
        Clamp(); Apply();
    }

    public void Move(Vector2 direction, double delta)
    {
        if (direction == Vector2.Zero) return;
        Focus += new Vector3(direction.X, 0, direction.Y) * _distance * 0.9f * (float)delta;
        Clamp(); Apply();
    }

    /// <summary>Opening a city centres and zooms on it; closing restores the previous zoom.</summary>
    public void FollowCity(int? cityId, Vector3 cityPosition)
    {
        if (cityId == _focusedCity) return;
        if (cityId != null && _focusedCity == null) _savedDistance = _distance;
        if (cityId != null) { Focus = cityPosition; _distance = 12f; }
        else _distance = _savedDistance;
        _focusedCity = cityId;
        Clamp(); Apply();
    }

    private void Clamp()
    {
        float x = Focus.X;
        if (_period > 0) x = ((x % _period) + _period) % _period;
        Focus = new Vector3(x, 0, Mathf.Clamp(Focus.Z, 0, _depth));
    }

    private void Apply()
    {
        float rad = Mathf.DegToRad(Pitch);
        Position = Focus + new Vector3(0, _distance * Mathf.Sin(rad), _distance * Mathf.Cos(rad));
        LookAt(Focus, Vector3.Up);
    }

    public override void _Ready() => Apply();
}
