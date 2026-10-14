using Godot;

namespace Civ.Client;

/// <summary>Command-line switches for automated screenshots (after <c>--</c> on the Godot command line).</summary>
internal sealed class DebugOptions
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

    public static DebugOptions Parse(string[] args)
    {
        string Value(string name, string fallback) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        return new DebugOptions
        {
            ScreenshotPath = Value("--screenshot", ""), Stage = Value("--stage", "setup"), Frames = int.Parse(Value("--frames", "60")),
            Turns = int.Parse(Value("--turns", "0")), World = Value("--world", ""), Opponents = int.Parse(Value("--opponents", "3")),
            Seed = ulong.Parse(Value("--seed", "1")), Reveal = args.Contains("--reveal"), Zoom = float.Parse(Value("--zoom", "0")),
        }.Normalized();
    }

    private DebugOptions Normalized() => ScreenshotPath is { Length: 0 } ? new DebugOptions
    {
        ScreenshotPath = null, Stage = Stage, Frames = Frames, Turns = Turns, World = World, Opponents = Opponents, Seed = Seed, Reveal = Reveal, Zoom = Zoom,
    } : this;
}

/// <summary>Godot entry point: loads the mods once, shows the setup screen, then the game. All rules live in the mods and Client.Core.</summary>
public partial class Main : Node3D
{
    private readonly DebugOptions _debug = DebugOptions.Parse(OS.GetCmdlineUserArgs());
    private readonly Label _status = new() { Text = "Loading mods…" };
    private Task<GameCatalog>? _loading;
    private Task<GameSession>? _starting;
    private GameCatalog? _catalog;
    private IconCache? _icons;
    private SetupScreen? _setup;
    private GameView? _game;
    private int _framesSinceReady = -1;

    public override void _Ready()
    {
        var layer = new CanvasLayer();
        _status.AddThemeFontSizeOverride("font_size", 24);
        _status.Position = new Vector2(40, 40);
        layer.AddChild(_status);
        AddChild(layer);
        GetWindow().Theme = Ui.MakeTheme();
        RenderingServer.SetDefaultClearColor(new Color(0.05f, 0.07f, 0.1f));
        var root = ModsRoot();
        if (root == null) { _status.Text = "No mods found. Build with 'dotnet build src/Civ.Godot' or set CIV_MODS."; return; }
        _loading = Task.Run(() => GameCatalog.Load(root));
        // A headless frame-limited smoke must load the content before it can quit.
        if (DisplayServer.GetName() == "headless") _loading.Wait();
    }

    /// <summary>Mods sit next to the built client; CIV_MODS overrides.</summary>
    private static string? ModsRoot() => ModDiscovery.FindModsRoot(new[]
    {
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Main).Assembly.Location) ?? "", "mods"),
        System.IO.Path.Combine(AppContext.BaseDirectory, "mods"),
        System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "mods"),
        System.IO.Path.Combine(OS.GetUserDataDir(), "mods"),
    });

    public override void _Process(double delta)
    {
        PollLoading();
        PollStarting();
        if (_framesSinceReady >= 0 && ++_framesSinceReady >= _debug.Frames) TakeScreenshot();
    }

    private void PollLoading()
    {
        if (_loading is not { IsCompleted: true } task) return;
        _loading = null;
        if (task.IsFaulted) { _status.Text = "Could not load mods:\n" + task.Exception?.GetBaseException().Message; GD.PushError(task.Exception?.ToString() ?? ""); return; }
        _catalog = task.Result;
        GD.Print($"Loaded {_catalog.Mods.Count} mods; content frozen.");
        _icons = new IconCache(_catalog);
        _status.Visible = false;
        ShowSetup();
    }

    private void ShowSetup()
    {
        _setup = new SetupScreen(_catalog!);
        _setup.StartRequested += Start;
        AddChild(_setup);
        if (_debug.ScreenshotPath != null && _debug.Stage == "setup") _framesSinceReady = 0;
        else if (_debug.ScreenshotPath != null) Start(DebugGame());
    }

    private NewGameOptions DebugGame()
    {
        var defaults = _catalog!.DefaultOptions();
        var size = _debug.World.Length == 0 ? defaults.WorldSize : _catalog.WorldSizes.First(w =>
            w.Id == _debug.World || w.Name.Equals(_debug.World, StringComparison.OrdinalIgnoreCase)).Id;
        return defaults with { WorldSize = size, Opponents = _debug.Opponents, Seed = _debug.Seed };
    }

    private void Start(NewGameOptions options)
    {
        _status.Text = "Generating world…";
        _status.Visible = true;
        var catalog = _catalog!;
        _starting = Task.Run(() => GameSession.Start(catalog, options));
    }

    private void PollStarting()
    {
        if (_starting is not { IsCompleted: true } task) return;
        _starting = null;
        if (task.IsFaulted)
        {
            _status.Text = "Could not start the game:\n" + task.Exception?.GetBaseException().Message;
            _setup?.Reset();
            return;
        }
        _status.Visible = false;
        _setup?.QueueFree();
        _setup = null;
        var session = task.Result;
        PrepareDebugStage(session);
        MapRenderer.LogTimings = _debug.ScreenshotPath != null;
        _game = new GameView(session, _icons!);
        AddChild(_game);
        if (_debug.Zoom != 0) _game.DebugZoom(_debug.Zoom);
        if (_debug.Stage == "path") _game.ForceHover(PathTarget(session));
        OpenDebugPanel();
        if (_debug.ScreenshotPath != null) _framesSinceReady = 0;
    }

    private void OpenDebugPanel()
    {
        if (_debug.Stage == "civics") _game!.Ui.ToggleGovernment();
        if (_debug.Stage == "research") _game!.Ui.ToggleResearch();
    }

    /// <summary>Screenshot runs can advance turns and open the city screen before the first frame.</summary>
    private void PrepareDebugStage(GameSession session)
    {
        if (_debug.ScreenshotPath == null) return;
        if (_debug.Reveal) RevealEverything(session);
        Input.WarpMouse(new Vector2(700, 10));
        var settler = session.Sim.State.Units.FirstOrDefault(u => u.Owner == GameSession.Human && session.DefOf(u).CanFoundCity);
        if (settler != null && _debug.Stage is "city" or "map-city") { session.SelectUnit(settler.Id); session.RunAction("found"); }
        for (int i = 0; i < _debug.Turns; i++) session.EndTurn();
        if (_debug.Stage == "city" && session.Sim.State.Cities.FirstOrDefault(c => c.Owner == GameSession.Human) is { } city)
        {
            session.SelectCity(city.Id);
            foreach (var choice in session.ProductionChoices(city).Where(p => p.Available).Take(3)) session.Enqueue(city, choice.Kind, choice.Id, append: true);
        }
    }

    /// <summary>A tile a few steps from the selected unit, to show the route preview.</summary>
    private static Hex? PathTarget(GameSession session)
    {
        if (session.Unit is not { } unit) return null;
        var grid = session.Sim.State.Grid;
        foreach (var hex in grid.Range(unit.Position, 6).OrderByDescending(h => grid.Distance(h, unit.Position)))
            if (session.PreviewAt(hex) is { } p && p.Path.Count >= 4) return hex;
        return null;
    }

    /// <summary>Screenshot aid: explore the whole map for the human team so terrain rendering can be inspected.</summary>
    private static void RevealEverything(GameSession session)
    {
        foreach (var plot in session.Sim.State.Plots) plot.Revealed.Add(session.Me.TeamId);
    }

    private void TakeScreenshot()
    {
        _framesSinceReady = -1;
        var image = GetViewport().GetTexture().GetImage();
        var path = System.IO.Path.GetFullPath(_debug.ScreenshotPath!);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var error = image.SavePng(path);
        GD.Print($"screenshot {path}: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}
