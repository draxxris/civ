using Godot;

namespace Ariel.Client;

/// <summary>Tilted strategy camera over the map. The focus wraps east-west on a cylinder map and is clamped at the poles.</summary>
internal sealed partial class CameraRig : Camera3D
{
    private const float MinDistance = 6f, MaxDistance = 44f;

    private float _pitch = 62f, _yaw;

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

    /// <summary>Capture aid: set the camera pitch and yaw (degrees; yaw 0 looks north) and distance outside the player's zoom limits.</summary>
    public void SetView(float? pitch, float? yaw, float? distance)
    {
        if (pitch is { } p) _pitch = Mathf.Clamp(p, 5f, 89f);
        if (yaw is { } y) _yaw = y;
        if (distance is { } d) _distance = Mathf.Clamp(d, 1f, 120f);
        Apply();
    }

    /// <summary>Turn the camera by a yaw step and tilt it by a pitch step, in degrees. Pitch keeps to the same 5–89 range a
    /// capture pose does, so the camera never flattens to nothing or tips past straight down.</summary>
    public void Rotate(float yawDegrees, float pitchDegrees)
    {
        _yaw += yawDegrees;
        _pitch = Mathf.Clamp(_pitch + pitchDegrees, 5f, 89f);
        Apply();
    }

    public void Zoom(float delta) { _distance = Mathf.Clamp(_distance + delta, MinDistance, MaxDistance); Apply(); }

    /// <summary>Pan by a mouse drag measured in screen pixels.</summary>
    public void Drag(Vector2 pixels)
    {
        float perPixel = 2f * _distance * Mathf.Tan(Mathf.DegToRad(Fov / 2)) / GetViewport().GetVisibleRect().Size.Y;
        // The ground follows the cursor, so the focus moves the other way, in the camera's frame.
        Focus += GroundPan(-pixels.X * perPixel, pixels.Y * perPixel / Mathf.Sin(Mathf.DegToRad(_pitch)));
        Clamp(); Apply();
    }

    public void Move(Vector2 direction, double delta)
    {
        if (direction == Vector2.Zero) return;
        Focus += GroundPan(direction.X, -direction.Y) * _distance * 0.9f * (float)delta;
        Clamp(); Apply();
    }

    /// <summary>A ground offset of <paramref name="right"/> screen-right and <paramref name="away"/> away from the camera,
    /// at the current yaw: panning and dragging both follow the view, so the keys keep their meaning after a turn instead
    /// of sliding the map along axes the player can no longer see. With no turn this is the world's own axes.</summary>
    private Vector3 GroundPan(float right, float away)
    {
        float yaw = Mathf.DegToRad(_yaw);
        return new Vector3(Mathf.Cos(yaw) * right - Mathf.Sin(yaw) * away, 0, -Mathf.Sin(yaw) * right - Mathf.Cos(yaw) * away);
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
        float rad = Mathf.DegToRad(_pitch), yaw = Mathf.DegToRad(_yaw);
        float flat = _distance * Mathf.Cos(rad);
        Position = Focus + new Vector3(flat * Mathf.Sin(yaw), _distance * Mathf.Sin(rad), flat * Mathf.Cos(yaw));
        LookAt(Focus, Vector3.Up);
    }

    public override void _Ready() => Apply();
}
