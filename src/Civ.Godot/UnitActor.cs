using Godot;

namespace Civ.Client;

/// <summary>
/// One unit standing on the map: a 3D model (or a plain figurine when the unit has no .glb) under a floating faction banner.
/// Plays the model's idle/fortify/attack/hurt/die clips, and falls back to simple code-driven motion for any clip it lacks.
/// </summary>
internal sealed partial class UnitActor : Node3D
{
    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["idle"] = ["idle"], ["run"] = ["run", "walk"], ["fortify"] = ["fortify", "fortified", "sentry"],
        ["attack"] = ["attack", "strike"], ["hurt"] = ["hurt", "hit"], ["die"] = ["die", "death"],
    };

    private readonly Node3D _pivot = new() { Name = "Pivot" };
    private readonly AnimationPlayer? _player;
    private readonly Dictionary<string, string> _clips = new();
    private string _state = "";

    /// <summary>Where the unit stands before any wrap-around shift.</summary>
    public Vector3 Anchor { get; set; }
    /// <summary>Visible surface height, used to keep procedural lunges and shakes above slopes.</summary>
    public Func<float, float, float>? Ground { get; init; }
    /// <summary>Procedural pose: metres toward the facing, sideways shake, and 0..1 fall.</summary>
    public float Lunge { get; set; }
    public float Shake { get; set; }
    public float Fall { get; set; }

    public UnitActor(Node3D? model, Color owner)
    {
        AddChild(_pivot);
        _banner = Banner(owner);
        AddChild(_banner);
        AddChild(_health);
        model ??= Figurine(owner);
        _pivot.AddChild(model);
        _player = FindPlayer(model);
        if (_player != null) IndexClips(_player);
    }

    public bool HasClip(string state) => _clips.ContainsKey(state);

    /// <summary>Face a direction on the ground (x, z); zero length keeps the current facing.</summary>
    public void FaceToward(Vector3 direction)
    {
        if (direction.X * direction.X + direction.Z * direction.Z > 1e-6f) Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
    }

    /// <summary>Play a clip by state name (idle, run, fortify, attack, hurt, die); false when the model has none.</summary>
    public bool Play(string state, float duration = 0)
    {
        if (state == "die") _banner.Visible = false;
        if (!_clips.TryGetValue(state, out var clip) && !(state == "fortify" && _clips.TryGetValue("idle", out clip))) { _state = state; return false; }
        if (_player != null && (_state != state || !_player.IsPlaying() || state is "attack" or "hurt" or "die"))
        {
            // Each blow restarts its one-shot clip. Fit it to the battle phase instead of cutting it off.
            _player.Stop();
            var length = _player.GetAnimation(clip).Length;
            _player.Play(clip, customSpeed: duration > 0 ? (float)length / duration : 1);
        }
        _state = state;
        return true;
    }

    public override void _Process(double delta)
    {
        var motion = new Vector3(Shake, 0, Lunge);
        var worldMotion = Basis * motion;
        float rise = Ground == null ? 0 : Ground(Anchor.X + worldMotion.X, Anchor.Z + worldMotion.Z) - Ground(Anchor.X, Anchor.Z);
        _pivot.Position = motion + new Vector3(0, rise - Fall * 0.2f, 0);
        _pivot.Rotation = new Vector3(0, 0, HasClip("die") ? 0 : Fall * Mathf.Pi / 2);
    }

    private static AnimationPlayer? FindPlayer(Node node)
    {
        if (node is AnimationPlayer p) return p;
        foreach (var child in node.GetChildren())
            if (FindPlayer(child) is { } found) return found;
        return null;
    }

    private void IndexClips(AnimationPlayer player)
    {
        var names = player.GetAnimationList();
        foreach (var (state, aliases) in Aliases)
        {
            foreach (var alias in aliases)
            {
                var name = names.FirstOrDefault(n => string.Equals(n.Split('/')[^1], alias, StringComparison.OrdinalIgnoreCase));
                if (name == null) continue;
                _clips[state] = name;
                if (player.GetAnimation(name) is { } animation)
                    animation.LoopMode = state is "idle" or "run" or "fortify" ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
                break;
            }
        }
    }

    private static Node3D Figurine(Color owner)
    {
        var root = new Node3D { Name = "Figurine" };
        var body = new StandardMaterial3D { AlbedoColor = owner.Lightened(0.15f), Roughness = 0.6f };
        var skin = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.7f, 0.55f), Roughness = 0.8f };
        var metal = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.72f, 0.78f), Metallic = 0.6f, Roughness = 0.35f };
        root.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.14f, Height = 0.5f }, Position = new Vector3(0, 0.3f, 0), MaterialOverride = body });
        root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.1f, Height = 0.2f }, Position = new Vector3(0, 0.66f, 0), MaterialOverride = skin });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.4f) }, Position = new Vector3(0.18f, 0.4f, 0.18f), MaterialOverride = metal });
        return root;
    }
}
