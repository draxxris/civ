using System.Text.Json;
using Civ.Client.Terrain;
using Godot;

namespace Civ.Client;

/// <summary>Godot entry point: loads the mods once, shows the setup screen, then the game. All rules live in the mods and Client.Core.</summary>
public partial class Main : Node3D
{
    private DebugOptions _debug = DebugOptions.Parse(OS.GetCmdlineUserArgs());
    private readonly Queue<string> _screenshotStages = new();
    private readonly Label _status = new() { Text = "Loading mods…" };
    private Task<GameCatalog>? _loading;
    private Task<GameSession>? _starting;
    private GameCatalog? _catalog;
    private IconCache? _icons;
    private SetupScreen? _setup;
    private GameView? _game;
    private int _framesSinceReady = -1;
    private FpsProbe? _fpsProbe;
    private List<ShotSpec>? _shots;
    private int _shotIndex;
    private string? _shotPath;
    private string _shotName = "";
    private Hex? _scenarioFocus;

    public override void _Ready()
    {
        TerrainRenderer.Tuning = new TerrainTuning().WithOverrides(_debug.Tuning);
        TerrainRenderer.ScatterDensityScale = _debug.ScatterDensityScale;
        if (_debug.TreeKeep is { } tree) ScatterRenderer.TreeKeep = tree;
        if (_debug.OtherKeep is { } other) ScatterRenderer.OtherKeep = other;
        if (_debug.FpsProbe)
        {
            if (DisplayServer.GetName() == "headless")
            {
                GD.PushError("--fps-probe needs a rendered display, not --headless.");
                GetTree().Quit(2);
                return;
            }
            GetWindow().Size = new Vector2I(1600, 900);
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            GD.Print($"FPS probe: 1600x900, {RenderingServer.GetVideoAdapterName()}, VSync disabled.");
        }
        if (OS.GetCmdlineUserArgs().Contains("--screenshots"))
        {
            foreach (var stage in new[] { "setup", "map", "city", "civics", "research", "path", "world", "terrain", "rivers" }) _screenshotStages.Enqueue(stage);
            _debug = _debug.ForScreenshot(_screenshotStages.Dequeue());
            GetWindow().Size = new Vector2I(1600, 900);
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        }
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
        try
        {
            PollLoading();
            PollStarting();
            if (_fpsProbe is not null && _fpsProbe.Tick()) GetTree().Quit();
            if (_framesSinceReady >= 0 && ++_framesSinceReady >= _debug.Frames) TakeScreenshot();
        }
        catch (Exception error) when (_debug.ScreenshotPath != null || _debug.FpsProbe)
        {
            GD.PrintErr(error.ToString());
            GetTree().Quit(1);
        }
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
        else if (_debug.ScreenshotPath != null || _debug.FpsProbe) Start(DebugGame());
    }

    private NewGameOptions DebugGame()
    {
        var defaults = _catalog!.DefaultOptions();
        if (_debug.FpsProbe) return defaults with { WorldSize = _catalog.WorldSizes.First(w => w.Name.Equals("Huge", StringComparison.OrdinalIgnoreCase)).Id, Seed = _debug.Seed };
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
        MapRenderer.LogTimings = _debug.ScreenshotPath != null || _debug.FpsProbe;
        TerrainRenderer.DebugMode = _debug.ScreenshotPath != null ? _debug.TerrainDebug : 0;
        _game = new GameView(session, _icons!);
        AddChild(_game);
        if (_debug.Zoom != 0) _game.DebugZoom(_debug.Zoom);
        FocusTerrainScreenshot(session);
        ApplyCaptureOptions(session);
        if (_debug.Stage == "path") _game.ForceHover(PathTarget(session));
        OpenDebugPanel();
        if (_debug.ScreenshotPath != null) _framesSinceReady = 0;
        if (_debug.FpsProbe)
        {
            var focus = TerrainShowcase.Focus(session);
            _game.DebugFocus(MapRenderer.PositionOf(focus));
            GD.Print($"FPS probe focus: {focus}; revealed huge map, seed {_debug.Seed}.");
            _fpsProbe = new FpsProbe(() => _game.DebugZoom(100));
        }
    }

    /// <summary>Command-line scene options: clean view, focus, camera pose, wireframe and the shot list.</summary>
    private void ApplyCaptureOptions(GameSession session)
    {
        if (_debug.ScreenshotPath == null) return;
        if (_debug.Clean) _game!.SetClean();
        if (_scenarioFocus is { } scene) _game!.DebugFocus(MapRenderer.PositionOf(scene));
        if (FocusHex(session, _debug.Focus, _debug.FocusTile is { } t ? new[] { t.Col, t.Row } : null) is { } focus) _game!.DebugFocus(MapRenderer.PositionOf(focus));
        _game!.DebugView(_debug.Pitch, _debug.Yaw, _debug.Distance);
        if (_debug.Wireframe) _game.Terrain.SetWireframe(true);
        _shotPath = _debug.ScreenshotPath;
        _shotName = System.IO.Path.GetFileNameWithoutExtension(_shotPath);
        if (_debug.ShotsFile == null) return;
        _shots = ShotSpec.Load(_debug.ShotsFile);
        _shotIndex = 0;
        ApplyShot(session, _shots[0]);
    }

    private Hex? FocusHex(GameSession session, string? kind, int[]? tile)
    {
        var grid = session.Sim.State.Grid;
        if (tile is { Length: 2 }) return grid.HexOf(grid.TileAt(tile[0], tile[1]));
        if (kind == null) return null;
        var hex = DebugFocusFinder.Find(session, kind);
        if (hex == null) GD.PushWarning($"--focus {kind}: nothing of that kind on this map; try another --seed or a --scenario.");
        else GD.Print($"focus {kind}: {hex}");
        return hex;
    }

    private void ApplyShot(GameSession session, ShotSpec shot)
    {
        _shotName = shot.Name;
        _shotPath = _debug.PathFor(shot.Name);
        if (FocusHex(session, shot.Focus, shot.FocusTile) is { } focus) _game!.DebugFocus(MapRenderer.PositionOf(focus));
        _game!.DebugView(shot.Pitch, shot.Yaw, shot.Distance);
        if (shot.TerrainDebug != null) _game.Terrain.SetDebugMode(DebugOptions.ParseTerrainDebug(shot.TerrainDebug));
        if (shot.Wireframe is { } wire) _game.Terrain.SetWireframe(wire);
    }

    private void WriteStats(string path)
    {
        var terrain = _game!.Terrain;
        var stats = new Dictionary<string, object?>
        {
            ["name"] = _shotName, ["seed"] = _debug.Seed, ["fps"] = Engine.GetFramesPerSecond(),
            ["tuning_overrides"] = _debug.Tuning, ["scatter"] = RenderStats.Scatter(),
            ["triangles"] = _game.Map.TriangleCounts(), ["heights"] = RenderStats.Heights(terrain.Snapshot, terrain.Field),
        };
        var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(path, ".json"), json);
        GD.Print($"stats {_shotName}: {JsonSerializer.Serialize(stats)}");
    }

    /// <summary>Terrain inspection views: a mountain close-up or the actual east-west wrap line at x = 0.</summary>
    private void FocusTerrainScreenshot(GameSession session)
    {
        if (_debug.ScreenshotPath is null) return;
        var state = session.Sim.State;
        if (_debug.Stage == "terrain")
        {
            // Offset the ridge from the left-hand unit panel and leave the coast in the close-up.
            _game!.DebugFocus(MapRenderer.PositionOf(TerrainShowcase.Focus(session)) + new Vector3(-1.5f, 0, 0.75f));
        }
        else if (_debug.Stage == "rivers")
        {
            _game!.DebugFocus(MapRenderer.PositionOf(TerrainShowcase.RiverFocus(session)));
        }
        else if (_debug.Stage == "peak")
        {
            int peak = Array.FindIndex(state.Plots, plot => plot.Kind == Civ.Game.PlotKind.Peak);
            if (peak >= 0) _game!.DebugFocus(MapRenderer.PositionOf(state.Grid.HexOf(new TileId(peak))));
        }
        else if (_debug.Stage == "wrap")
        {
            _game!.DebugFocus(new Vector3(0, 0, (state.Map.Height - 1) * 0.75f));
        }
    }

    private void OpenDebugPanel()
    {
        if (_debug.Stage == "civics") _game!.Ui.ToggleGovernment();
        if (_debug.Stage == "research") _game!.Ui.ToggleResearch();
    }

    /// <summary>Screenshot runs can advance turns and open the city screen before the first frame.</summary>
    private void PrepareDebugStage(GameSession session)
    {
        if (_debug.FpsProbe) { RevealEverything(session); return; }
        if (_debug.ScreenshotPath == null) return;
        if (_debug.Reveal) RevealEverything(session);
        if (_debug.ScenarioFile != null) _scenarioFocus = DebugScenario.Apply(session, _debug.ScenarioFile);
        else if (_debug.CustomScene) foreach (var plot in session.Sim.State.Plots) plot.Visible.Add(session.Me.TeamId);
        if (_debug.Stage == "terrain") TerrainShowcase.Prepare(session);
        if (_debug.Stage == "rivers")
        {
            RevealEverything(session);
            foreach (var plot in session.Sim.State.Plots) plot.Visible.Add(session.Me.TeamId);
        }
        Input.WarpMouse(new Vector2(700, 10));
        if (_debug.Stage is "city" or "map-city") FoundScreenshotCity(session);
        for (int i = 0; i < _debug.Turns; i++) session.EndTurn();
        if (_debug.Stage == "city") OpenScreenshotCity(session);
    }

    private static void FoundScreenshotCity(GameSession session)
    {
        var settler = session.Sim.State.Units.FirstOrDefault(u => u.Owner == GameSession.Human && session.DefOf(u).CanFoundCity);
        if (settler == null) return;
        session.SelectUnit(settler.Id);
        session.RunAction("found");
    }

    private static void OpenScreenshotCity(GameSession session)
    {
        var city = session.Sim.State.Cities.FirstOrDefault(c => c.Owner == GameSession.Human);
        if (city == null) return;
        session.SelectCity(city.Id);
        foreach (var choice in session.ProductionChoices(city).Where(p => p.Available).Take(3))
            session.Enqueue(city, choice.Kind, choice.Id, append: true);
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
        var visibility = session.Sim.Content.Services.Get<Civ.Game.IVisibilityService>().Single();
        foreach (var tile in session.Sim.State.Grid.Tiles) visibility.RevealPlot(session.Sim, tile, session.Me.TeamId);
    }

    private void TakeScreenshot()
    {
        _framesSinceReady = -1;
        var image = GetViewport().GetTexture().GetImage();
        var path = System.IO.Path.GetFullPath(_shotPath ?? _debug.ScreenshotPath!);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var error = image.SavePng(path);
        GD.Print($"screenshot {path}: {error}");
        if (error == Error.Ok && _debug.Stats && _game != null) WriteStats(path);
        if (error == Error.Ok && _shots != null && ++_shotIndex < _shots.Count)
        {
            ApplyShot(_game!.Session, _shots[_shotIndex]);
            _framesSinceReady = 0;
            return;
        }
        if (error == Error.Ok && _screenshotStages.Count > 0)
        {
            _game?.QueueFree();
            _setup?.QueueFree();
            _game = null;
            _setup = null;
            _debug = _debug.ForScreenshot(_screenshotStages.Dequeue());
            ShowSetup();
            return;
        }
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}
