using Godot;

namespace Civ.Client;

/// <summary>New-game screen: world size, speed, handicap, opponents, seed and the leader to play.</summary>
internal sealed partial class SetupScreen : Control
{
    private readonly GameCatalog _catalog;
    private readonly OptionButton _world = new(), _speed = new(), _handicap = new();
    private readonly SpinBox _opponents = new() { MinValue = 0, MaxValue = 15, Value = 3, Step = 1 };
    private readonly LineEdit _seed = new() { Text = "1" };
    private readonly LineEdit _search = new() { PlaceholderText = "Search leaders, civilizations or traits" };
    private readonly ItemList _leaders = new() { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 360) };
    private readonly Label _details = Ui.Text("", 14, Ui.Dim);
    private readonly Button _start;
    private List<LeaderChoice> _shown = new();
    private string _leaderId;

    public event Action<NewGameOptions>? StartRequested;

    public SetupScreen(GameCatalog catalog)
    {
        _catalog = catalog;
        var defaults = catalog.DefaultOptions();
        _leaderId = defaults.Leader;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new ColorRect { Color = new Color(0.05f, 0.07f, 0.1f) };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
        Fill(_world, catalog.WorldSizes, defaults.WorldSize);
        Fill(_speed, catalog.GameSpeeds, defaults.GameSpeed);
        Fill(_handicap, catalog.Handicaps, defaults.Handicap);
        _start = Ui.Button("Start game", OnStart, size: 20);
        _start.CustomMinimumSize = new Vector2(0, 52);
        BuildLayout();
        _search.TextChanged += _ => FillLeaders();
        _leaders.ItemSelected += OnLeaderSelected;
        FillLeaders();
    }

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
        panel.CustomMinimumSize = new Vector2(980, 600);
        panel.GrowHorizontal = GrowDirection.Both; panel.GrowVertical = GrowDirection.Both;
        var root = Ui.VBox(12);
        root.AddChild(Ui.Text("New game", 30, Ui.Accent, wrap: false));
        var columns = Ui.HBox(24);
        columns.AddChild(SettingsColumn());
        columns.AddChild(LeaderColumn());
        root.AddChild(columns);
        root.AddChild(_start);
        panel.AddChild(root);
        AddChild(panel);
    }

    private Control SettingsColumn()
    {
        var col = Ui.VBox(8);
        col.CustomMinimumSize = new Vector2(320, 0);
        Add(col, "World size", _world);
        Add(col, "Game speed", _speed);
        Add(col, "Handicap", _handicap);
        Add(col, "AI opponents", _opponents);
        var seedRow = Ui.HBox(6);
        _seed.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        seedRow.AddChild(_seed);
        seedRow.AddChild(Ui.Button("Random", () => _seed.Text = ((ulong)Random.Shared.NextInt64(1, 999_999_999)).ToString()));
        Add(col, "Seed", seedRow);
        return col;
    }

    private static void Add(VBoxContainer col, string label, Control control)
    {
        col.AddChild(Ui.Text(label, 14, Ui.Dim));
        col.AddChild(control);
    }

    private Control LeaderColumn()
    {
        var col = Ui.VBox(8);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.AddChild(Ui.Text("Your leader", 14, Ui.Dim));
        col.AddChild(_search);
        _leaders.AddThemeFontSizeOverride("font_size", 14);
        col.AddChild(_leaders);
        col.AddChild(_details);
        return col;
    }

    private void FillLeaders()
    {
        string query = _search.Text.Trim();
        _shown = _catalog.Leaders.Where(l => query.Length == 0 || l.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _leaders.Clear();
        foreach (var leader in _shown) _leaders.AddItem(leader.Label);
        int index = _shown.FindIndex(l => l.Id == _leaderId);
        if (index < 0 && _shown.Count > 0) { index = 0; _leaderId = _shown[0].Id; }
        if (index >= 0) { _leaders.Select(index); _leaders.EnsureCurrentIsVisible(); }
        ShowDetails();
    }

    private void OnLeaderSelected(long index)
    {
        _leaderId = _shown[(int)index].Id;
        ShowDetails();
    }

    private void ShowDetails()
    {
        var leader = _catalog.Leaders.FirstOrDefault(l => l.Id == _leaderId);
        _details.Text = leader == null ? "" : $"{leader.Name} leads {leader.CivilizationName}.\nTraits: {(leader.Traits.Count == 0 ? "none" : string.Join(", ", leader.Traits))}";
    }

    public NewGameOptions Options()
    {
        if (!ulong.TryParse(_seed.Text, out var seed)) throw new ArgumentException("Seed must be an unsigned whole number.");
        return new NewGameOptions(_catalog.WorldSizes[_world.Selected].Id, _catalog.GameSpeeds[_speed.Selected].Id,
            _catalog.Handicaps[_handicap.Selected].Id, _leaderId, (int)_opponents.Value, seed);
    }

    private void OnStart()
    {
        if (!ulong.TryParse(_seed.Text, out _)) { _details.Text = "Seed must be an unsigned whole number."; return; }
        _start.Disabled = true;
        StartRequested?.Invoke(Options());
    }

    public void Reset() => _start.Disabled = false;
}
