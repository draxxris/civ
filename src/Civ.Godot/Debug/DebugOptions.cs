using System.Text.Json;
using Godot;

namespace Civ.Client;

/// <summary>Command-line switches for automated captures and tuning (after <c>--</c> on the Godot command line).
/// The reference with examples is in src/Civ.Godot/README.md; keep the two in step.
/// <code>
/// --screenshot [path|name]   save one capture (a bare name goes to --out-dir)
/// --screenshots              the built-in stage tour into docs/screenshots (or --out-dir)
/// --shots poses.json         several captures from one launch: [{name, focus, focusTile, pitch, yaw, distance, terrainDebug, wireframe}]
/// --out-dir dir              where captures and stats go (default docs/screenshots, which holds the committed images)
/// --stage name               setup|map|city|civics|research|path|world|terrain|rivers|peak|wrap
/// --focus kind               hills|forest|jungle|road|peak|river|city: centre on the best patch of that kind
/// --focus-tile col,row       centre on a tile
/// --pitch deg --yaw deg --distance n   camera pose (yaw 0 looks north; pitch 62 and distance 18 are the defaults)
/// --clean                    no HUD, tooltips, hover, path preview, units, cities or labels
/// --scenario file.json       overwrite the generated map with a hand-made scene (see DebugScenario)
/// --tuning Name=v,Name=v     override TerrainTuning values; --tuning-file file.json does the same from {"Name": v}
/// --scatter-density-scale f  multiply forest/jungle scatter density
/// --scatter-keep tree=f,other=f   fraction of tree and other props drawn after thinning
/// --terrain-debug mode       0/normal, 1/weights, 2/hidden, 4/relief, 5/bands (height contours), 6/slope, 7/lattice (= --wireframe)
/// --wireframe                draw the terrain mesh lattice (the compatibility renderer has no wireframe mode)
/// --stats                    print and save props, triangles and height statistics next to each capture
/// --world size --seed n --opponents n --reveal --zoom d --frames n --fps-probe
/// </code></summary>
internal sealed record DebugOptions
{
    public string? ScreenshotPath { get; private init; }
    public string Stage { get; private init; } = "setup";
    public int Frames { get; private init; } = 60;
    public int Turns { get; private init; }
    public string World { get; private init; } = "";
    public int Opponents { get; private init; } = 3;
    public ulong Seed { get; private init; } = 1;
    public bool Reveal { get; private init; }
    public float Zoom { get; private init; }
    public int TerrainDebug { get; private init; }
    public bool FpsProbe { get; private init; }
    public string? OutDir { get; private init; }
    public string? ShotsFile { get; private init; }
    public string? Focus { get; private init; }
    public (int Col, int Row)? FocusTile { get; private init; }
    public float? Pitch { get; private init; }
    public float? Yaw { get; private init; }
    public float? Distance { get; private init; }
    public bool Clean { get; private init; }
    public string? ScenarioFile { get; private init; }
    public bool Wireframe { get; private init; }
    public bool Stats { get; private init; }
    public IReadOnlyDictionary<string, double> Tuning { get; private init; } = new Dictionary<string, double>();
    public double ScatterDensityScale { get; private init; } = 1;
    public double? TreeKeep { get; private init; }
    public double? OtherKeep { get; private init; }

    /// <summary>A scene option that implies a custom capture: reveal the whole map and open on the map, not the setup screen.</summary>
    public bool CustomScene => Focus != null || FocusTile != null || ShotsFile != null || ScenarioFile != null;

    public static readonly string[] TerrainDebugNames = ["normal", "weights", "hidden", "", "relief", "bands", "slope", "lattice", "cavity"];

    public static DebugOptions Parse(string[] args)
    {
        string? Raw(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        string Value(string name, string fallback) => Raw(name) ?? fallback;
        float? Float(string name) => Raw(name) is { } v ? float.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : null;
        var tuning = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (Raw("--tuning-file") is { } file)
            foreach (var (k, v) in JsonSerializer.Deserialize<Dictionary<string, double>>(System.IO.File.ReadAllText(file)) ?? new()) tuning[k] = v;
        foreach (var (k, v) in Pairs(Raw("--tuning"))) tuning[k] = v;
        var keep = Pairs(Raw("--scatter-keep")).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var options = new DebugOptions
        {
            ScreenshotPath = Value("--screenshot", ""), Stage = Raw("--stage") ?? "setup", Frames = int.Parse(Value("--frames", "60")),
            Turns = int.Parse(Value("--turns", "0")), World = Value("--world", ""), Opponents = int.Parse(Value("--opponents", "3")),
            Seed = ulong.Parse(Value("--seed", "1")), Reveal = args.Contains("--reveal"), Zoom = float.Parse(Value("--zoom", "0")),
            TerrainDebug = ParseTerrainDebug(Value("--terrain-debug", "0")), FpsProbe = args.Contains("--fps-probe"),
            OutDir = Raw("--out-dir"), ShotsFile = Raw("--shots"), Focus = Raw("--focus"),
            FocusTile = Raw("--focus-tile") is { } t && t.Split(',') is [var c, var r] ? (int.Parse(c), int.Parse(r)) : null,
            Pitch = Float("--pitch"), Yaw = Float("--yaw"), Distance = Float("--distance"), Clean = args.Contains("--clean"),
            ScenarioFile = Raw("--scenario"), Wireframe = args.Contains("--wireframe"), Stats = args.Contains("--stats"), Tuning = tuning,
            ScatterDensityScale = Float("--scatter-density-scale") ?? 1,
            TreeKeep = keep.TryGetValue("tree", out var tk) ? tk : null, OtherKeep = keep.TryGetValue("other", out var ok) ? ok : null,
        };
        return options.Normalized(Raw("--stage") == null);
    }

    public static int ParseTerrainDebug(string value)
    {
        if (int.TryParse(value, out int n)) return n;
        int i = Array.FindIndex(TerrainDebugNames, name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
        return i >= 0 ? i : throw new ArgumentException($"Unknown terrain debug '{value}'.");
    }

    private static IEnumerable<KeyValuePair<string, double>> Pairs(string? list)
    {
        if (string.IsNullOrWhiteSpace(list)) yield break;
        foreach (var part in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) throw new ArgumentException($"Expected Name=value, got '{part}'.");
            yield return new(kv[0], double.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The PNG path for a capture name: in <see cref="OutDir"/> when given, else the committed screenshots folder.</summary>
    public string PathFor(string name) => OutDir != null ? System.IO.Path.Combine(OutDir, name + ".png")
        : ProjectSettings.GlobalizePath($"res://../../docs/screenshots/{name}.png");

    public DebugOptions ForScreenshot(string stage) => this with
    {
        ScreenshotPath = PathFor(stage),
        Stage = stage, Frames = 90, Turns = stage == "city" ? 3 : 0,
        Reveal = stage is "world" or "path" or "terrain" or "rivers",
        Zoom = stage == "map" ? -10 : stage == "world" ? 18 : stage == "terrain" ? -7 : stage == "rivers" ? -8 : 0,
    };

    private DebugOptions Normalized(bool defaultStage)
    {
        var o = this;
        // A bare name goes to --out-dir; a relative or absolute path is used as given.
        if (o.ScreenshotPath is { Length: > 0 } p && o.OutDir != null && !p.Contains('/') && !p.Contains('\\'))
            o = o with { ScreenshotPath = PathFor(System.IO.Path.GetFileNameWithoutExtension(p)) };
        if (o.ScreenshotPath is { Length: 0 }) o = o with { ScreenshotPath = null };
        // --shots alone is a capture run; the per-shot names decide the files.
        if (o.ShotsFile != null && o.ScreenshotPath == null) o = o with { ScreenshotPath = PathFor("shot") };
        // A custom scene opens on the revealed map rather than the setup screen.
        if (o.CustomScene && defaultStage && o.ScreenshotPath != null) o = o with { Stage = "map", Reveal = true };
        if (o.CustomScene && o.Frames == 60) o = o with { Frames = 30 };
        return o;
    }
}
