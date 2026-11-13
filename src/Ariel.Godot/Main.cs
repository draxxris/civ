using System.Text.Json;
using Ariel.Client.Terrain;
using Ariel.Game;
using Godot;

namespace Ariel.Client;

/// <summary>Godot entry point: selects mods, loads them once, then shows setup and the game.</summary>
public partial class Main : Node3D
{
    private DebugOptions _debug = DebugOptions.Parse(OS.GetCmdlineUserArgs());
    private readonly Queue<string> _screenshotStages = new();
    private readonly Label _status = new() { Text = "Loading mods…" };
    private Task<GameCatalog>? _loading;
    private Task<GameSession>? _starting;
    private Task<MultiplayerGame>? _startingMultiplayer;
    private MultiplayerScreen? _multiplayerScreen;
    private MultiplayerGame? _multiplayer;
    private DebugMultiplayer? _debugMultiplayer;
    private GameCatalog? _catalog;
    private IconCache? _icons;
    private SetupScreen? _setup;
    private GameView? _game;
    private int _framesSinceReady = -1;
    // Frames the current capture waits: --frames for the first, --pose-frames for each later --shots pose.
    private int _framesToWait = -1;
    private FpsProbe? _fpsProbe;
    private List<ShotSpec>? _shots;
    private int _shotIndex;
    private string? _shotPath;
    private string _shotName = "";
    private Hex? _scenarioFocus;
    /// <summary>Whether the map was sized to the scenario file, so the scene is the whole world and starts blank.</summary>
    private bool _sceneWorld;
    /// <summary>How far a scene's own coordinates moved to fit the map it was sized to, so <c>--focus-tile</c> can keep
    /// using the numbers the file was written with.</summary>
    private (int Col, int Row) _sceneShift;

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
        // The automatic paths (--screenshot, --fps-probe, --skip-mod-manager) start the catalog load first and paint
        // the theme while it runs on its worker threads. Headless takes the same branch but waits for the load
        // inside BeginModStartup, so it stays serial. The interactive path must install the theme first:
        // BeginModStartup opens the mod manager, which builds Control nodes immediately.
        if (AutomaticStartup)
        {
            BeginModStartup();
            // A startup that already failed has queued its own quit; painting the theme would only delay the exit.
            if (_loading is not null) Ui.InstallTheme(GetWindow());
        }
        else
        {
            Ui.InstallTheme(GetWindow());
            BeginModStartup();
        }
        RenderingServer.SetDefaultClearColor(new Color(0.05f, 0.07f, 0.1f));
    }

    /// <summary>Stable bundled candidate order. User installations and ARIEL_MODS take priority per ID.</summary>
    private static IEnumerable<string> BundledRoots() => new[]
    {
        System.IO.Path.GetDirectoryName(typeof(Main).Assembly.Location) ?? "",
        AppContext.BaseDirectory,
        ProjectSettings.GlobalizePath("res://"),
    };

    private static IEnumerable<string> BundledModsRoots() =>
        BundledRoots().Select(root => System.IO.Path.Combine(root, Ariel.Composition.GameContent.ModsFolder));

    /// <summary>The first bundled location that holds the base modules. They ship with the game and are never
    /// replaced from user or environment mod folders.</summary>
    private static string RulesRoot()
    {
        var candidates = BundledRoots().Select(root => System.IO.Path.Combine(root, Ariel.Composition.GameContent.RulesFolder)).ToArray();
        return candidates.FirstOrDefault(System.IO.Directory.Exists) ?? candidates[0];
    }

    public override void _Process(double delta)
    {
        try
        {
            PollLoading();
            if (_debugMultiplayer?.Tick() is { } lobby) StartMultiplayer(lobby);
            PollStarting();
            if (_game is { } shown && !shown.CanShow(shown.Session.Sim)) RebuildGameView(shown);
            if (_debugMultiplayer != null && _multiplayer != null && _framesSinceReady < 0 && _debugMultiplayer.Drive(_multiplayer.Session))
            {
                GD.Print($"multiplayer smoke: turn {_multiplayer.Session.Sim.State.Turn}, hash {_multiplayer.Session.Sim.ComputeHash():x16}");
                if (_debug.ScreenshotPath != null) _framesSinceReady = 0;
                else GetTree().Quit();
            }
            if (_fpsProbe is not null && _fpsProbe.Tick()) GetTree().Quit();
            if (_framesSinceReady >= 0)
            {
                if (++_framesSinceReady == 2 && _debug.Stage == "research")
                {
                    _game!.Ui.DebugShowResearchTooltip();
                }
                if (_framesSinceReady >= (_framesToWait < 0 ? _debug.Frames : _framesToWait)) TakeScreenshot();
            }
        }
        catch (Exception error) when (_debug.Unattended)
        {
            GD.PrintErr(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void PollLoading()
    {
        if (_loading is not { IsCompleted: true } task) return;
        _loading = null;
        if (task.IsFaulted) { ModLoadFailed("Could not load mods:\n" + task.Exception?.GetBaseException().Message); return; }
        _catalog = task.Result;
        if (SetupError(_catalog) is { } error) { ModLoadFailed(error); return; }
        GD.Print($"Loaded {_catalog.Mods.Count} mods; content frozen.");
        _icons = new IconCache(_catalog);
        _status.Visible = false;
        ShowSetup();
    }

    private void ShowSetup()
    {
        _setup = new SetupScreen(_catalog!);
        _setup.StartRequested += Start;
        _setup.ModsRequested += () => ShowModManager();
        _setup.MultiplayerRequested += ShowMultiplayer;
        AddChild(_setup);
        if (_debug.Multiplayer != null) _debugMultiplayer = new DebugMultiplayer(_catalog!, _debug);
        else if (_debug.ScreenshotPath != null && _debug.Stage == "lobby")
        {
            ShowMultiplayer();
            _multiplayerScreen!.DebugHost();
            _framesSinceReady = 0;
        }
        else if (_debug.ScreenshotPath != null && _debug.Stage == "setup") _framesSinceReady = 0;
        else if (_debug.Unattended) Start(DebugGame());
    }

    private NewGameOptions DebugGame()
    {
        var defaults = _catalog!.DefaultOptions();
        var mapGenerator = _debug.MapGenerator.Length == 0 ? defaults.MapGenerator : ResolveMapGenerator(_debug.MapGenerator);
        if (_debug.FpsProbe) return defaults with
        {
            WorldSize = _catalog.WorldSizes.First(w => w.Name.Equals("Huge", StringComparison.OrdinalIgnoreCase)).Id,
            MapGenerator = mapGenerator, Seed = _debug.Seed,
        };
        var size = _debug.World.Length == 0 ? defaults.WorldSize : _catalog.WorldSizes.First(w =>
            w.Id == _debug.World || w.Name.Equals(_debug.World, StringComparison.OrdinalIgnoreCase)).Id;
        var options = defaults with { WorldSize = size, MapGenerator = mapGenerator, Opponents = _debug.Opponents, Seed = _debug.Seed };
        if (_debug.ScenarioFile is { } file && _debug.World.Length == 0 && DebugScenario.Frame(file) is { } scene)
        {
            // A hand-made scene gets a world of its own: the map is the scene plus a margin and does not wrap, and there is
            // nobody to play against, because the tiles outside the scene are cleared before it lands.
            _sceneWorld = true;
            _sceneShift = (scene.ShiftCol, scene.ShiftRow);
            GD.Print($"scene world {scene.Width} x {scene.Height} tiles, scene shifted by ({scene.ShiftCol}, {scene.ShiftRow})");
            options = options with { MapOverride = new MapSpec(scene.Width, scene.Height), Opponents = 0 };
        }
        return options;
    }

    private string ResolveMapGenerator(string value)
    {
        var generators = _catalog!.MapGenerators;
        var choice = generators.FirstOrDefault(g => string.Equals(g.Id, value, StringComparison.Ordinal))
            ?? generators.FirstOrDefault(g => string.Equals(ShortId(g.Id), value, StringComparison.Ordinal))
            ?? generators.FirstOrDefault(g => string.Equals(g.Name, value, StringComparison.OrdinalIgnoreCase));
        return choice?.Id ?? throw new ArgumentException($"Unknown map generator '{value}'.");
    }

    private static string ShortId(string id)
    {
        int separator = id.IndexOf(':');
        return separator < 0 ? id : id[(separator + 1)..];
    }

    private void Start(NewGameOptions options)
    {
        _status.Text = "Generating world…";
        _status.Visible = true;
        var catalog = _catalog!;
        _starting = Task.Run(() => GameSession.Start(catalog, options, backgroundTurns: true));
    }

    public override void _ExitTree() => _multiplayer?.Dispose();

    /// <summary>Leave setup for the multiplayer screen; Back returns to setup.</summary>
    private void ShowMultiplayer()
    {
        if (_setup != null) _setup.Visible = false;
        _multiplayerScreen = new MultiplayerScreen(_catalog!);
        _multiplayerScreen.Back += () =>
        {
            _multiplayerScreen?.QueueFree();
            _multiplayerScreen = null;
            if (_setup != null) _setup.Visible = true;
        };
        _multiplayerScreen.Starting += StartMultiplayer;
        AddChild(_multiplayerScreen);
    }

    /// <summary>The lobby agreed on a game: build the same world as every other peer, off the game thread.</summary>
    private void StartMultiplayer(Lobby lobby)
    {
        _status.Text = "Generating world…";
        _status.Visible = true;
        _multiplayerScreen?.QueueFree();
        _multiplayerScreen = null;
        var catalog = _catalog!;
        var start = lobby.Starting!;
        var transport = lobby.Release();
        _startingMultiplayer = Task.Run(() => MultiplayerGame.Create(catalog, transport, start, lobby.Chat));
    }

    private void PollStarting()
    {
        if (_startingMultiplayer is { IsCompleted: true } multiplayer)
        {
            _startingMultiplayer = null;
            if (multiplayer.IsFaulted)
            {
                _status.Text = "Could not start the game:\n" + multiplayer.Exception?.GetBaseException().Message;
                if (_setup != null) _setup.Visible = true;
                return;
            }
            _multiplayer = multiplayer.Result;
            _multiplayer.DesyncDirectory = ProjectSettings.GlobalizePath("user://desync");
            _status.Visible = false;
            Launch(_multiplayer.Session, _multiplayer);
            return;
        }
        if (_starting is not { IsCompleted: true } task) return;
        _starting = null;
        if (task.IsFaulted)
        {
            _status.Text = "Could not start the game:\n" + task.Exception?.GetBaseException().Message;
            _setup?.Reset();
            return;
        }
        _status.Visible = false;
        Launch(task.Result, null);
    }

    /// <summary>A loaded save of another map or seed: build the in-game screen again for it, before anything draws the
    /// new world with renderers sized and seeded for the old one.</summary>
    private void RebuildGameView(GameView shown)
    {
        RemoveChild(shown);
        shown.QueueFree();
        _game = new GameView(shown.Session, _icons!, _multiplayer);
        AddChild(_game);
    }

    private void Launch(GameSession session, MultiplayerGame? multiplayer)
    {
        _setup?.QueueFree();
        _setup = null;
        if (multiplayer == null) PrepareDebugStage(session); // debug stages edit the state directly, which would desync
        MapRenderer.LogTimings = _debug.Unattended || OS.GetCmdlineUserArgs().Contains("--trace-performance");
        TerrainRenderer.DebugMode = _debug.Unattended ? _debug.TerrainDebug : 0;
        _game = new GameView(session, _icons!, multiplayer);
        AddChild(_game);
        if (_debug.SaveToFile is { } saveTo && !session.SaveGame(saveTo)) GD.PrintErr(session.Message);
        if (_debug.LoadSaveFile is { } load && !session.LoadGame(load)) GD.PrintErr(session.Message);
        if (_debug.Zoom != 0) _game.DebugZoom(_debug.Zoom);
        FocusTerrainScreenshot(session);
        ApplyCaptureOptions(session);
        if (_debug.Stage == "path") _game.ForceHover(PathTarget(session));
        OpenDebugPanel();
        if (_debug.ScreenshotPath != null && multiplayer == null) _framesSinceReady = 0;
        if (_debug.Stats) RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
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
        if (!_debug.Unattended) return;
        if (_debug.Clean) _game!.SetClean();
        if (_scenarioFocus is { } scene) _game!.DebugFocus(MapRenderer.PositionOf(scene));
        if (FocusHex(session, _debug.Focus, _debug.FocusTile is { } t ? new[] { t.Col, t.Row } : null) is { } focus) _game!.DebugFocus(MapRenderer.PositionOf(focus));
        _game!.DebugView(_debug.Pitch, _debug.Yaw, _debug.Distance);
        if (_debug.Wireframe) _game.Terrain.SetWireframe(true);
        if (_debug.ScreenshotPath == null) return;
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
        if (tile is { Length: 2 })
        {
            // A scene-sized world moved the scene, so the flag keeps the coordinates the file was written with.
            int col = tile[0] + _sceneShift.Col, row = tile[1] + _sceneShift.Row;
            if (grid.TryTile(col, row, out var id)) return grid.HexOf(id);
            GD.PushWarning($"--focus-tile {tile[0]},{tile[1]} is off this {grid.Spec.Width} x {grid.Spec.Height} map; keeping the scene's own focus.");
            return null;
        }
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
            // GPU time of the last measured frame; steadier than the FPS of a capture run.
            ["gpu_ms"] = Math.Round(RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid()), 2),
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
        if (!_debug.Unattended) return;
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
            int peak = Array.FindIndex(state.Plots, plot => plot.Kind == Ariel.Game.PlotKind.Peak);
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
        bool dialog = _debug.ScreenshotPath != null && _game!.Ui.OpenDebugPanel(_debug.Stage);
        if (_debug.OpenScreen is { } name && !OpenScreen(name)) GetTree().Quit(1);
        // A stage that names no base dialog may name a mod's screen (`--stage diplomacy`), the way the screens'
        // committed captures are taken.
        else if (_debug.OpenScreen is null && !dialog && _debug.ScreenshotPath != null && ScreenNamed(_debug.Stage) is { } screen)
            _game!.Ui.ToggleScreen(screen.Id);
        foreach (var id in _debug.Press) PressInScreen(id);
    }

    /// <summary>Screenshot runs can advance turns and open the city screen before the first frame.</summary>
    private void PrepareDebugStage(GameSession session)
    {
        if (_debug.FpsProbe) { RevealEverything(session); return; }
        if (!_debug.Unattended) return;
        PrepareCaptureScene(session);
        if (_debug.ScreenshotPath == null) return;
        Input.WarpMouse(new Vector2(700, 10));
        // A screen about other players needs someone to have met.
        if (_debug.OpenScreen != null || ScreenNamed(_debug.Stage, session) != null)
            foreach (var rival in session.Sim.State.Players.Where(p => p.Id != session.LocalPlayer))
                session.Sim.Rules().Relations.EstablishContact(session.Sim, session.Me.TeamId, rival.TeamId);
        if (_debug.Stage is "city" or "map-city" or "conquest") FoundScreenshotCity(session);
        if (_debug.Stage == "conquest" && session.Sim.State.Cities.FirstOrDefault() is { } captured)
            captured.Extensions.Set(session.Sim.Content, new CityRuleState { PendingConquestChoice = true });
        for (int i = 0; i < _debug.Turns; i++)
        {
            session.EndTurn();
            session.WaitForOtherPlayers();
        }
        if (_debug.Stage == "city") OpenScreenshotCity(session);
    }

    private void PrepareCaptureScene(GameSession session)
    {
        if (_debug.Reveal) RevealEverything(session);
        if (_debug.ScenarioFile != null) _scenarioFocus = DebugScenario.Apply(session, _debug.ScenarioFile, _sceneWorld);
        else if (_debug.CustomScene) foreach (var plot in session.Sim.State.Plots) plot.Visible.Add(session.Me.TeamId);
        if (_debug.Stage == "terrain") TerrainShowcase.Prepare(session);
        if (_debug.Stage == "rivers")
        {
            RevealEverything(session);
            foreach (var plot in session.Sim.State.Plots) plot.Visible.Add(session.Me.TeamId);
        }
        if (_debug.DumpScenarioFile is { } dump) DebugScenario.Write(session, dump);
    }

    private static void FoundScreenshotCity(GameSession session)
    {
        var settler = session.Sim.State.Units.FirstOrDefault(u => u.Owner == session.LocalPlayer && session.DefOf(u).CanFoundCity);
        if (settler == null) return;
        session.SelectUnit(settler.Id);
        session.RunAction("found");
    }

    private static void OpenScreenshotCity(GameSession session)
    {
        var city = session.Sim.State.Cities.FirstOrDefault(c => c.Owner == session.LocalPlayer);
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
        var visibility = session.Sim.Rules().Visibility;
        foreach (var tile in session.Sim.State.Grid.Tiles) visibility.RevealPlot(session.Sim, tile, session.Me.TeamId);
    }

    /// <summary>Opens a registered screen for a capture: by its id, or by its mod's id when that mod has one screen.
    /// An unknown name lists the screens this content has and fails the run.</summary>
    private bool OpenScreen(string name)
    {
        if (ScreenNamed(name) is { } match) { _game!.Ui.ToggleScreen(match.Id); return true; }
        GD.PrintErr($"--open-screen {name}: no such screen; registered: {string.Join(", ", _game!.Session.Screens.Screens.Select(s => s.Id))}");
        return false;
    }

    private IGameScreen? ScreenNamed(string name) => ScreenNamed(name, _game!.Session);

    /// <summary>The screen with this id, or the only screen of the mod with this id.</summary>
    private static IGameScreen? ScreenNamed(string name, GameSession session)
    {
        var screens = session.Screens.Screens;
        var byMod = screens.Where(s => s.Id.StartsWith(name + ":", StringComparison.Ordinal)).ToList();
        return screens.FirstOrDefault(s => s.Id == name) ?? (byMod.Count == 1 ? byMod[0] : null);
    }

    /// <summary>Presses the control with this id in the open screen, as a click would, for a capture of a state the
    /// screen only reaches through its own controls (a selected row, a chosen tab).</summary>
    private void PressInScreen(string id)
    {
        var screens = _game!.Session.Screens;
        var action = screens.View is { } view ? ViewWalk.ActionOf(view.Body, id) : null;
        if (action is null || !screens.Press(action)) GD.PrintErr($"--press {id}: nothing to press with that id in {screens.OpenId ?? "no open screen"}");
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
        if (error != Error.Ok) { GetTree().Quit(1); return; }
        if (AdvanceShotPose() || AdvanceStageTour()) return;
        GetTree().Quit();
    }

    private bool AdvanceShotPose()
    {
        if (_shots is null || ++_shotIndex >= _shots.Count) return false;
        ApplyShot(_game!.Session, _shots[_shotIndex]);
        _framesSinceReady = 0;
        _framesToWait = _debug.PoseFrames;
        return true;
    }

    private bool AdvanceStageTour()
    {
        if (_screenshotStages.Count == 0) return false;
        _game?.QueueFree();
        _setup?.QueueFree();
        _game = null;
        _setup = null;
        _debug = _debug.ForScreenshot(_screenshotStages.Dequeue());
        ShowSetup();
        return true;
    }
}
