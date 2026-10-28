using Godot;

namespace Ariel.Client;

/// <summary>New-game screen: world size, layout, speed, handicap, opponents, options and seed in a label-column table
/// beside two tabs. The Leader tab picks the leader and describes what that leader brings; the Map tab shows the world
/// the current choice generates, rendered on a worker thread, where a newer choice discards an older render.</summary>
internal sealed partial class SetupScreen : Control
{
    /// <summary>Room for the map picture, which the frame surrounds by ten pixels on each side.</summary>
    private static readonly Vector2 PreviewBox = new(600, 300);
    /// <summary>Wide enough for a label column and its widest control, and no wider.</summary>
    private const int SettingsColumnWidth = 420;
    private static readonly Rgb PreviewWater = new(0.35f, 0.68f, 0.87f);
    private static readonly Rgb PreviewLand = new(0.11f, 0.26f, 0.14f);

    private readonly GameCatalog _catalog;
    private readonly OptionButton _world = new(), _mapGenerator = new(), _speed = new(), _handicap = new();
    private readonly SpinBox _opponents = new() { MinValue = 0, MaxValue = 15, Value = 3, Step = 1 };
    private readonly LineEdit _seed = new() { Text = "1" };
    private readonly LineEdit _search = new() { PlaceholderText = "Search leaders, civilizations or traits" };
    private readonly ItemList _leaders = new() { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 150) };
    private readonly Label _message = Ui.Text("", 13, Ui.Bad);
    private readonly VBoxContainer _briefing = Ui.VBox(1);
    private readonly VBoxContainer _mapSettings = Ui.VBox(4);
    private readonly TabContainer _tabs = new();
    private readonly Dictionary<string, CheckBox> _gameOptions = new(StringComparer.Ordinal);
    private readonly MapPreviewPanel _preview = new(PreviewBox);
    private readonly Button _start;
    private List<LeaderChoice> _shown = new();
    private LeaderChoice? _selected;
    private int _previewToken;
    private bool _previewPending;
    private ulong _previewDue;
    private Task<(int Token, MapImage? Image, string? Note)>? _previewTask;

    public event Action<NewGameOptions>? StartRequested;
    public event Action? ModsRequested;

    public SetupScreen(GameCatalog catalog)
    {
        _catalog = catalog;
        var defaults = catalog.DefaultOptions();
        _selected = catalog.LeaderById(defaults.Leader);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new ColorRect { Color = new Color(0.05f, 0.07f, 0.1f) };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
        Fill(_world, catalog.WorldSizes, defaults.WorldSize);
        Fill(_mapGenerator, catalog.MapGenerators, defaults.MapGenerator);
        Fill(_speed, catalog.GameSpeeds, defaults.GameSpeed);
        Fill(_handicap, catalog.Handicaps, defaults.Handicap);
        _start = Ui.Button("Start game", OnStart, size: 18);
        _start.CustomMinimumSize = new Vector2(230, 48);
        _mapSettings.AddChild(Ui.Text("This map generator has no settings yet.", 13, Ui.Dim));
        BuildLayout();
        _search.TextChanged += _ => FillLeaders();
        _leaders.ItemSelected += OnLeaderSelected;
        _seed.TextChanged += _ => PreviewChanged(immediate: false);
        _world.ItemSelected += _ => PreviewChanged(immediate: true);
        _mapGenerator.ItemSelected += _ => PreviewChanged(immediate: true);
        FillLeaders();
    }

    /// <summary>The first picture is drawn before the screen is shown, so a capture of the setup stage already has it.</summary>
    public override void _Ready() => RenderNow();

    private static void Fill(OptionButton button, IReadOnlyList<SetupChoice> choices, string selected)
    {
        foreach (var choice in choices) button.AddItem(choice.Name);
        button.Selected = Math.Max(0, choices.ToList().FindIndex(c => c.Id == selected));
        button.AddThemeFontSizeOverride("font_size", 15);
    }

    private void BuildLayout()
    {
        var panel = Ui.Panel(new Color(0.1f, 0.12f, 0.16f, 0.98f), 20);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        panel.CustomMinimumSize = new Vector2(1100, 600);
        panel.GrowHorizontal = GrowDirection.Both; panel.GrowVertical = GrowDirection.Both;
        var root = Ui.VBox(8);
        root.AddChild(Ui.Text("New game", 28, Ui.Accent, wrap: false));
        var columns = Ui.HBox(20);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        columns.AddChild(SettingsColumn());
        columns.AddChild(Tabs());
        root.AddChild(columns);
        root.AddChild(ButtonRow());
        panel.AddChild(root);
        AddChild(panel);
    }

    /// <summary>The settings as one label column and one control column, then the game options.</summary>
    private Control SettingsColumn()
    {
        var col = Ui.VBox(8);
        col.CustomMinimumSize = new Vector2(SettingsColumnWidth, 0);
        col.SizeFlagsVertical = SizeFlags.ExpandFill;
        var table = new GridContainer { Columns = 2 };
        table.AddThemeConstantOverride("h_separation", 10);
        table.AddThemeConstantOverride("v_separation", 2);
        Row(table, "World size", _world);
        Row(table, "World layout", _mapGenerator);
        Row(table, "Game speed", _speed);
        Row(table, "Handicap", _handicap);
        Row(table, "AI opponents", _opponents);
        Row(table, "Seed", SeedRow());
        col.AddChild(table);
        col.AddChild(Ui.Text("Game options", 14, Ui.Dim, wrap: false));
        // Two columns halve the height of the option list, which a mod may make long.
        var options = new GridContainer { Columns = 2 };
        options.AddThemeConstantOverride("h_separation", 8);
        options.AddThemeConstantOverride("v_separation", 2);
        foreach (var option in _catalog.GameOptions)
        {
            var check = new CheckBox { Text = option.Name, ButtonPressed = option.Default, TooltipText = option.Help };
            _gameOptions.Add(option.Id, check);
            options.AddChild(check);
        }
        col.AddChild(Ui.Scroll(options, 100));
        return col;
    }

    private static void Row(GridContainer table, string label, Control control)
    {
        var text = Ui.Text(label, 14, Ui.Dim, wrap: false);
        text.VerticalAlignment = VerticalAlignment.Center;
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        table.AddChild(text);
        table.AddChild(control);
    }

    private Control SeedRow()
    {
        var row = Ui.HBox(6);
        _seed.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_seed);
        var random = Ui.Button("Random", () => _seed.Text = ((ulong)Random.Shared.NextInt64(1, 999_999_999)).ToString(), size: 13);
        random.CustomMinimumSize = new Vector2(88, 0);
        row.AddChild(random);
        return row;
    }

    /// <summary>The right column as tabs: the leader to play, and the map the choice generates.</summary>
    private Control Tabs()
    {
        _tabs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tabs.SizeFlagsVertical = SizeFlags.ExpandFill;
        _tabs.AddChild(LeaderTab());
        _tabs.AddChild(MapTab());
        return _tabs;
    }

    private Control LeaderTab()
    {
        var tab = Ui.VBox(8);
        tab.Name = "Leader";
        tab.AddChild(_search);
        _leaders.AddThemeFontSizeOverride("font_size", 14);
        tab.AddChild(_leaders);
        var briefing = Ui.Scroll(_briefing, 210);
        briefing.SizeFlagsVertical = SizeFlags.Fill;
        tab.AddChild(briefing);
        return tab;
    }

    /// <summary>Generator settings sit above the picture once a ruleset offers them; the picture keeps the bottom.</summary>
    private Control MapTab()
    {
        var tab = Ui.VBox(8);
        tab.Name = "Map";
        tab.AddChild(_mapSettings);
        tab.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        tab.AddChild(_preview);
        return tab;
    }

    /// <summary>Mods and Start game sit right, at their own width, and the message line fills the space left of them.</summary>
    private Control ButtonRow()
    {
        var row = Ui.HBox(10);
        _message.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_message);
        var mods = Ui.Button("Mods (restart required)", () => ModsRequested?.Invoke(), size: 15);
        mods.CustomMinimumSize = new Vector2(210, 44);
        row.AddChild(mods);
        row.AddChild(_start);
        return row;
    }

    private void FillLeaders()
    {
        string query = _search.Text.Trim();
        _shown = _catalog.Leaders.Where(l => query.Length == 0 || l.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _leaders.Clear();
        foreach (var leader in _shown) _leaders.AddItem(leader.Label);
        int index = _selected is null ? -1 : _shown.IndexOf(_selected);
        if (index < 0 && _shown.Count > 0) { index = 0; _selected = _shown[0]; }
        if (index >= 0) { _leaders.Select(index); _leaders.EnsureCurrentIsVisible(); }
        ShowBriefing();
    }

    private void OnLeaderSelected(long index)
    {
        _selected = _shown[(int)index];
        ShowBriefing();
    }

    private void ShowBriefing()
    {
        Ui.Clear(_briefing);
        if (_selected is null) return;
        foreach (var section in LeaderBriefing.Build(_catalog, _selected))
        {
            _briefing.AddChild(Ui.Text(section.Heading, 12, Ui.Dim, wrap: false));
            foreach (var group in section.Groups)
            {
                if (group.Title is not null) _briefing.AddChild(Ui.Text(group.Title, 13, Ui.Accent, wrap: false));
                foreach (var line in group.Lines) _briefing.AddChild(Ui.Text(line, 12));
            }
        }
    }

    public NewGameOptions Options() => TryOptions(out var options)
        ? options : throw new ArgumentException("Seed must be an unsigned whole number.");

    /// <summary>The choices as they stand, or false while the seed field holds no whole number.</summary>
    private bool TryOptions(out NewGameOptions options)
    {
        options = null!;
        if (!ulong.TryParse(_seed.Text, out var seed)) return false;
        var selected = _gameOptions.ToDictionary(option => option.Key, option => option.Value.ButtonPressed, StringComparer.Ordinal);
        options = new NewGameOptions(_catalog.WorldSizes[_world.Selected].Id, _catalog.GameSpeeds[_speed.Selected].Id,
            _catalog.Handicaps[_handicap.Selected].Id, _selected!.Id, (int)_opponents.Value, seed,
            MapGenerator: _catalog.MapGenerators[_mapGenerator.Selected].Id, GameOptions: selected, Civilization: _selected!.CivilizationId);
        return true;
    }

    private void OnStart()
    {
        if (!TryOptions(out var options)) { _message.Text = "Seed must be an unsigned whole number."; return; }
        _message.Text = "";
        _start.Disabled = true;
        StartRequested?.Invoke(options);
    }

    public void Reset() => _start.Disabled = false;

    // --- the world picture ---------------------------------------------------------------------

    /// <summary>A choice changed. The seed field is debounced so typing a number does not start a render per key; a
    /// world size or layout change takes effect at once.</summary>
    private void PreviewChanged(bool immediate)
    {
        _previewToken++;
        _previewPending = true;
        _previewDue = Time.GetTicksMsec() + (immediate ? 0UL : 250UL);
    }

    public override void _Process(double delta)
    {
        if (_previewTask is null && _previewPending && Time.GetTicksMsec() >= _previewDue) StartPreview();
        if (_previewTask is { IsCompleted: true } task) FinishPreview(task);
    }

    /// <summary>Draw the picture for the current choice on this thread. The setup screen is small enough for first use.</summary>
    private void RenderNow()
    {
        if (!TryOptions(out var options)) { _preview.ShowNote("The seed must be an unsigned whole number."); return; }
        var (image, note) = Render(_catalog, options);
        if (image is null) _preview.ShowNote(note!);
        else _preview.ShowImage(image);
    }

    private void StartPreview()
    {
        _previewPending = false;
        if (!TryOptions(out var options))
        {
            _previewToken++;
            _preview.ShowNote("The seed must be an unsigned whole number.");
            return;
        }
        int token = _previewToken;
        var catalog = _catalog;
        _previewTask = Task.Run(() =>
        {
            var (image, note) = Render(catalog, options);
            return (token, image, note);
        });
    }

    private void FinishPreview(Task<(int Token, MapImage? Image, string? Note)> task)
    {
        _previewTask = null;
        // Render reports its own failures as a note, so a task that did not finish has nothing newer to say: keeping
        // the picture avoids an older, empty result replacing a newer one.
        if (!task.IsCompletedSuccessfully) return;
        var (token, image, note) = task.Result;
        if (token != _previewToken) return; // A newer choice owns the picture now.
        if (image is null) _preview.ShowNote(note ?? "The world preview could not be drawn.");
        else _preview.ShowImage(image);
    }

    /// <summary>Generate the silhouette for a choice and rasterise it, reporting a failure as one line of text.</summary>
    private static (MapImage? Image, string? Note) Render(GameCatalog catalog, NewGameOptions options)
    {
        try
        {
            var silhouette = MapPreview.Land(catalog, options);
            return (MapSilhouetteImage.Render(silhouette, (int)PreviewBox.X, (int)PreviewBox.Y, PreviewWater, PreviewLand), null);
        }
        catch (Exception error)
        {
            return (null, error.GetBaseException().Message);
        }
    }
}
