using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>Top bar, unit and city lists, end-turn button, research dialog and the city screen. The top bar and the
/// left panel are built once and updated in place; the rest is rebuilt only when the session changes.</summary>
internal sealed partial class GameUi : CanvasLayer
{
    internal const float TopBarHeight = 82f;
    private const float LeftWidth = 320f;

    private readonly GameSession _session;
    private readonly IconCache _icons;
    private readonly PanelContainer _top = Ui.Panel(margin: 6);
    private readonly PanelContainer _left = Ui.Panel();
    private readonly PanelContainer _hoverTip = Ui.Panel(new Color(0.05f, 0.06f, 0.08f, 0.92f), 6);
    private readonly Label _hoverText = Ui.Text("", 13, wrap: false);
    private readonly Label _message = Ui.Text("", 16, Ui.Accent);
    private readonly Button _endTurn;
    private readonly HFlowContainer _topRow = new();
    private Label _turnText = null!;
    private Label _goldenAgeText = null!;
    private Label _goldText = null!;
    private Button _researchButton = null!;
    private (Label Label, Button Minus, Button Plus) _scienceRate;
    private (Label Label, Button Minus, Button Plus) _cultureRate;
    private (Label Label, Button Minus, Button Plus) _espionageRate;
    private Button _civicsButton = null!;
    private Button _conquestButton = null!;
    private Button _religionButton = null!;
    private Button _corporationsButton = null!;
    private Button _espionageButton = null!;
    private Button _eventsButton = null!;
    private Button _diplomacyButton = null!;
    private Button _pediaButton = null!;
    private Button _techTreeButton = null!;
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Label _citiesText = null!;
    private readonly PanelContainer _researchDialog = Ui.Panel(margin: 12);
    private readonly CityUi _city;
    private int _shownRevision = -1;
    private bool _shownCityOpen;
    private bool _researchOpen;
    private bool _shownResearchOpen;
    private int _shownResearchRevision = -1;
    private int _shownCityState;
    private MapOverlays? _shownOverlays;
    private (Vector2 Mouse, Vector2 Size, Vector2 View)? _hoverTipInputs;
    private readonly List<(CheckBox Button, Func<MapOverlays, bool> Read)> _overlayChecks = new();

    public bool PointerOverUi => GetViewport().GuiGetHoveredControl() != null;
    public bool TextInputFocused => GetViewport().GuiGetFocusOwner() is LineEdit;

    public GameUi(GameSession session, IconCache icons)
    {
        _session = session; _icons = icons;
        _top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _top.OffsetBottom = TopBarHeight;
        _left.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        _left.OffsetTop = TopBarHeight + 4; _left.OffsetRight = LeftWidth; _left.OffsetBottom = -4;
        _left.GrowVertical = Control.GrowDirection.Both;
        _hoverTip.MouseFilter = Control.MouseFilterEnum.Ignore;
        _hoverTip.Visible = false;
        _hoverTip.AddChild(_hoverText);
        _message.HorizontalAlignment = HorizontalAlignment.Center;
        _message.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        _message.OffsetTop = -96; _message.OffsetBottom = -66;
        _message.MouseFilter = Control.MouseFilterEnum.Ignore;
        _endTurn = Ui.Button("End turn", session.EndTurn, size: 18);
        _endTurn.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        _endTurn.OffsetLeft = -220; _endTurn.OffsetTop = -64; _endTurn.OffsetRight = -12; _endTurn.OffsetBottom = -12;
        _researchDialog.SetAnchorsPreset(Control.LayoutPreset.Center);
        _researchDialog.Visible = false;
        _governmentDialog.SetAnchorsPreset(Control.LayoutPreset.Center);
        _governmentDialog.Visible = false;
        _city = new CityUi(session, icons);
        InitPanels();
        InitLeft();
        InitTop();
        foreach (var node in new Node[] { _top, _left, _feed, _message, _endTurn, _researchDialog, _city, _governmentDialog, _modal, _hoverTip }) AddChild(node);
        AddChild(MapOverlayControls());
    }

    public void Refresh()
    {
        RefreshOverlayChecks();
        if (!NeedsRefresh()) return;
        bool cityOpen = _session.City != null;
        bool revision = _shownRevision != _session.Revision;
        bool cityOpenChanged = _shownCityOpen != cityOpen;
        bool cityStateChanged = _shownCityState != _city.State;
        _shownRevision = _session.Revision;
        _shownCityOpen = cityOpen;
        _shownCityState = _city.State;
        _left.Visible = !cityOpen;
        _endTurn.Visible = !cityOpen;
        _city.Visible = cityOpen;

        // One gate per panel: a panel is rebuilt only when the key it depends on moved. The dialog gates are read
        // after the top bar, because RefreshTop can open or close the conquest modal as a side effect.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (revision) RefreshTop();
        double topMs = clock.Elapsed.TotalMilliseconds;
        double leftMs = 0, cityMs = 0;
        if (cityOpen)
        {
            if (cityOpenChanged || revision || cityStateChanged) _city.Build(_session.City!);
            cityMs = clock.Elapsed.TotalMilliseconds - topMs;
        }
        else if (revision)
        {
            RefreshLeft();
            leftMs = clock.Elapsed.TotalMilliseconds - topMs;
        }
        double dialogStart = clock.Elapsed.TotalMilliseconds;
        if (_shownResearchOpen != _researchOpen || (revision && _researchOpen)) BuildResearchDialog();
        if (_shownGovernmentRevision != _governmentRevision || (revision && _governmentOpen)) BuildGovernmentDialog();
        if (_shownModalRevision != _modalRevision || ((revision || cityOpenChanged) && _modalKind != Modal.None)) BuildModal();
        _shownResearchOpen = _researchOpen;
        _shownGovernmentRevision = _governmentRevision;
        _shownModalRevision = _modalRevision;
        double dialogsMs = clock.Elapsed.TotalMilliseconds - dialogStart;
        if (revision) BuildFeed();
        double feedMs = clock.Elapsed.TotalMilliseconds - dialogStart - dialogsMs;
        _message.Text = _session.Message;
        int idle = _session.UnitsNeedingOrders;
        _endTurn.Text = idle > 0 ? $"End turn ({idle} waiting)" : "End turn";
        if (MapRenderer.LogTimings)
            GD.Print($"ui update: top {topMs:F2} ms, left {leftMs:F2} ms, city {cityMs:F2} ms, "
                + $"dialogs {dialogsMs:F2} ms, feed {feedMs:F2} ms (city open: {cityOpen})");
    }

    private bool NeedsRefresh() => _shownRevision != _session.Revision || _shownResearchOpen != _researchOpen
        || _shownCityOpen != (_session.City != null)
        || _shownCityState != _city.State || _shownGovernmentRevision != _governmentRevision
        || _shownModalRevision != _modalRevision;

    private void RefreshOverlayChecks()
    {
        if (_shownOverlays == _session.Overlays) return;
        foreach (var (button, read) in _overlayChecks) button.SetPressedNoSignal(read(_session.Overlays));
        _shownOverlays = _session.Overlays;
    }

    private Control MapOverlayControls()
    {
        var panel = Ui.Panel(margin: 4);
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        panel.OffsetLeft = -215; panel.OffsetRight = 215; panel.OffsetTop = -42; panel.OffsetBottom = -6;
        var row = Ui.HBox(4);
        AddOverlayCheck(row, "Yields", o => o.Yields, (o, on) => o with { Yields = on },
            "Visible tile yields: green F = food, orange P = production, gold C = commerce. City work labels remain available.");
        AddOverlayCheck(row, "Resources", o => o.Resources, (o, on) => o with { Resources = on }, "Known resource badges");
        AddOverlayCheck(row, "Grid", o => o.Grid, (o, on) => o with { Grid = on }, "Hex lines on explored tiles");
        AddOverlayCheck(row, "Borders", o => o.CulturalBorders, (o, on) => o with { CulturalBorders = on }, "Known cultural borders");
        panel.AddChild(row);
        return panel;
    }

    private void AddOverlayCheck(Control row, string text, Func<MapOverlays, bool> read,
        Func<MapOverlays, bool, MapOverlays> update, string tooltip)
    {
        var check = new CheckBox { Text = text, ButtonPressed = read(_session.Overlays), TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None };
        check.AddThemeFontSizeOverride("font_size", 13);
        check.Toggled += on => _session.Overlays = update(_session.Overlays, on);
        _overlayChecks.Add((check, read));
        row.AddChild(check);
    }

    // --- hover -------------------------------------------------------------------------------------

    public void SetHover(TileInfo? tile, string? preview)
    {
        var lines = new List<string>();
        if (preview != null) lines.Add(preview);
        if (tile != null) { lines.Add(tile.Title); lines.AddRange(tile.Lines); }
        _hoverTip.Visible = lines.Count > 0;
        _hoverText.Text = string.Join("\n", lines);
        _hoverTip.Size = Vector2.Zero;
        _hoverTipInputs = null;
    }

    public void MoveHoverTip(Vector2 mouse)
    {
        if (!_hoverTip.Visible) return;
        var size = _hoverTip.GetCombinedMinimumSize();
        var view = GetViewport().GetVisibleRect().Size;
        var inputs = (mouse, size, view);
        if (_hoverTipInputs == inputs) return;
        _hoverTipInputs = inputs;
        _hoverTip.Position = new Vector2(Mathf.Min(mouse.X + 18, view.X - size.X - 4), Mathf.Min(mouse.Y + 18, view.Y - size.Y - 4));
    }

    // --- top bar -----------------------------------------------------------------------------------

    /// <summary>
    /// Builds the top bar's one row of widgets once. <see cref="RefreshTop"/> only changes their text, colour, tooltip
    /// and visibility, so the controls keep their canvas items and the flow layout keeps a stable child order for the
    /// conditional buttons.
    /// </summary>
    private void InitTop()
    {
        _topRow.AddThemeConstantOverride("h_separation", 10);
        _topRow.AddThemeConstantOverride("v_separation", 4);
        _turnText = Ui.Text("", 15, Colors.White, wrap: false);
        _goldenAgeText = Ui.Text("", 14, Ui.Accent, wrap: false);
        _goldenAgeText.Visible = false;
        _goldText = Ui.Text("", 15, Ui.Accent, wrap: false);
        _researchButton = Ui.Button("", ToggleResearch, "Open the research list (R)");
        _researchButton.ClipText = true;
        _researchButton.CustomMinimumSize = new Vector2(170, 0);
        _civicsButton = Ui.Button("Civics", ToggleGovernment);
        _conquestButton = Ui.Button("Conquest!", () => OpenModal(Modal.Conquest));
        _conquestButton.AddThemeColorOverride("font_color", Ui.Bad);
        _religionButton = Ui.Button("Religion", () => OpenModal(Modal.Religion), "State religion and the religions of the world");
        _corporationsButton = Ui.Button("Corporations", () => OpenModal(Modal.Corporations), "Corporations and their branches");
        _espionageButton = Ui.Button("Espionage", ToggleEspionage, "Rival intelligence, spending and counter-espionage");
        _eventsButton = Ui.Button("Events!", () => OpenModal(Modal.Events), "An event needs your choice");
        _diplomacyButton = Ui.Button("Diplomacy", () => OpenModal(Modal.Diplomacy), "Rival leaders, treaties, scores and victory");
        _pediaButton = Ui.Button("Pedia", () => OpenModal(Modal.Pedia), "Browse every tech, unit, building and more");
        _techTreeButton = Ui.Button("Tech Tree", () => OpenModal(Modal.TechTree), "Browse technology prerequisites");
        _saveButton = Ui.Button("Save", QuickSaveGame, "Quick save (F5)");
        _loadButton = Ui.Button("Load", () => OpenModal(Modal.Load), "Load a saved game (F9)");
        _citiesText = Ui.Text("", 14, Ui.Dim, wrap: false);
        _topRow.AddChild(_turnText);
        _topRow.AddChild(_goldenAgeText);
        _topRow.AddChild(_goldText);
        _topRow.AddChild(_researchButton);
        _scienceRate = RateBox("Science", RateKind.Research);
        _cultureRate = RateBox("Culture", RateKind.Culture);
        _espionageRate = RateBox("Espionage", RateKind.Espionage);
        _topRow.AddChild(_civicsButton);
        // Every optional button exists from the start and RefreshTop toggles Visible, so the flow layout never sees a
        // child appear or disappear in a different position.
        _conquestButton.Visible = false;
        _corporationsButton.Visible = false;
        _espionageButton.Visible = false;
        _eventsButton.Visible = false;
        _techTreeButton.Visible = false;
        foreach (var button in new[] { _conquestButton, _religionButton, _corporationsButton, _espionageButton, _eventsButton,
            _diplomacyButton, _pediaButton, _techTreeButton, _saveButton, _loadButton }) _topRow.AddChild(button);
        _topRow.AddChild(_citiesText);
        _top.AddChild(_topRow);
    }

    /// <summary>Updates the top bar in place; nothing is freed, and a value that did not change is not written again.</summary>
    private void RefreshTop()
    {
        var s = _session.Summary();
        string date = s.CalendarDate is { } calendar ? $"  ·  {calendar.FormattedDate}" : "";
        Ui.Set(_turnText, $"Turn {s.Turn}{date}  ·  {s.Era}");
        int goldenAge = _session.GoldenAgeTurnsRemaining;
        _goldenAgeText.Visible = goldenAge > 0;
        if (goldenAge > 0) Ui.Set(_goldenAgeText, $"Golden Age · {goldenAge} turns");
        Ui.Set(_goldText, $"Gold {_session.Me.Treasury} ({Labels.Signed(s.Net)})");
        Ui.Tint(_goldText, s.Net < 0 ? Ui.Bad : Ui.Accent);
        var r = s.Research;
        Ui.Set(_researchButton, r == null ? "Choose research!" : $"{r.Name} {r.Progress}/{r.Cost}" + (r.Turns is int t ? $" ({t}t)" : ""));
        if (r == null) Ui.Tint(_researchButton, Ui.Bad);
        else if (_researchButton.HasThemeColorOverride("font_color")) _researchButton.RemoveThemeColorOverride("font_color");
        Ui.Set(_scienceRate.Label, $"Science {s.Rates.Research}%");
        Ui.Set(_cultureRate.Label, $"Culture {s.Rates.Culture}%");
        Ui.Set(_espionageRate.Label, $"Espionage {s.Rates.Espionage}%");
        var anarchy = _session.Me.Extensions.Get<GovernmentPlayerState>(_session.Sim.Content)?.AnarchyTurns ?? 0;
        Ui.Set(_civicsButton, anarchy > 0 ? $"Civics · {anarchy}t anarchy" : "Civics");

        // Not pure rendering: UpdateConquestModal records the pending conquests and can open or close the modal, so it
        // runs whenever the top bar refreshes, even when the buttons themselves look unchanged. Each availability call
        // runs exactly once per refresh and its result drives the button's visibility.
        var conquests = _session.DescribePendingConquests();
        UpdateConquestModal(conquests);
        _conquestButton.Visible = conquests.Count > 0;
        if (conquests.Count > 0) _conquestButton.TooltipText = $"{conquests.Count} city conquest choice(s) need your decision";
        _corporationsButton.Visible = _session.DescribeCorporations().Available;
        _espionageButton.Visible = _session.DescribeEspionage().Available;
        _eventsButton.Visible = _session.DescribePendingEvents().Count > 0;
        _techTreeButton.Visible = _session.DescribeTechTree() is not null;
        Ui.Set(_citiesText, $"{s.Cities} cities  ·  {s.Units} units");
    }

    private (Label Label, Button Minus, Button Plus) RateBox(string name, RateKind kind)
    {
        var box = Ui.HBox(2);
        var label = Ui.Text("", 14, Colors.White, wrap: false);
        var minus = Ui.Button("−", () => _session.ShiftRate(kind, -10), $"10% less {name}");
        var plus = Ui.Button("+", () => _session.ShiftRate(kind, 10), $"10% more {name}");
        box.AddChild(label);
        box.AddChild(minus);
        box.AddChild(plus);
        _topRow.AddChild(box);
        return (label, minus, plus);
    }

    // --- research dialog ------------------------------------------------------------------------

    public void ToggleResearch()
    {
        if (_governmentOpen) ToggleGovernment();
        if (_modalKind != Modal.None) CloseModal();
        _researchOpen = !_researchOpen;
    }

    public bool CloseDialogs()
    {
        bool wasOpen = _researchOpen || _governmentOpen || _modalKind != Modal.None;
        if (_modalKind != Modal.None) CloseModal();
        if (_governmentOpen) ToggleGovernment();
        _researchOpen = false;
        return wasOpen;
    }

    /// <summary>Hides the dialog without clearing it while it is closed, so only a new revision rebuilds it.</summary>
    private void BuildResearchDialog()
    {
        if (!_researchOpen || _session.City != null) { _researchDialog.Visible = false; return; }
        _researchDialog.Visible = true;
        if (_shownResearchRevision == _session.Revision) { CentreResearchDialog(); return; }
        _shownResearchRevision = _session.Revision;
        Ui.Clear(_researchDialog);
        var box = Ui.VBox(6);
        var head = Ui.HBox();
        var selection = _session.ResearchSelection();
        head.AddChild(Ui.Heading(selection?.Title ?? "Choose research"));
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        head.AddChild(Ui.Button("Close", () => _researchOpen = false));
        box.AddChild(head);
        if (_session.DescribeTechTree() is not null)
            box.AddChild(Ui.Button("Browse tech tree", () => OpenModal(Modal.TechTree)));
        var choices = _session.ResearchChoices();
        bool cards = selection?.Layout == Civ.Game.ResearchChoiceLayout.Cards;
        if (cards) BuildResearchCards(box, choices);
        else BuildResearchList(box, choices);
        if (choices.Count == 0) box.AddChild(Ui.Text("No eligible technologies are available.", 14, Ui.Dim));
        if (_session.FreeResearchCredits > 0) box.AddChild(Ui.Text($"Free technologies: {_session.FreeResearchCredits}", 14, Ui.Accent));
        _researchDialog.CustomMinimumSize = new Vector2(cards ? 940 : 620, 0);
        _researchDialog.AddChild(box);
        CentreResearchDialog();
    }

    /// <summary>Keeps a reused dialog in the middle of the current window without rebuilding it.</summary>
    private void CentreResearchDialog() => _researchDialog.Position =
        (GetViewport().GetVisibleRect().Size - new Vector2(_researchDialog.CustomMinimumSize.X, 520)) / 2;

    internal static string StripMarkup(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\{[^}]*\}|\[[^\]]*\]", "");
}
