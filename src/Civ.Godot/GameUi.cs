using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>Top bar, unit and city lists, end-turn button, research dialog and the city screen. Rebuilt only when the session changes.</summary>
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
    private readonly PanelContainer _researchDialog = Ui.Panel(margin: 12);
    private readonly CityUi _city;
    private int _shownRevision = -1;
    private bool _researchOpen;
    private bool _shownResearchOpen;
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
        foreach (var node in new Node[] { _top, _left, _feed, _message, _endTurn, _researchDialog, _city, _governmentDialog, _modal, _hoverTip }) AddChild(node);
        AddChild(MapOverlayControls());
    }

    public void Refresh()
    {
        RefreshOverlayChecks();
        if (!NeedsRefresh()) return;
        bool cityOpen = _session.City != null;
        _left.Visible = !cityOpen;
        _endTurn.Visible = !cityOpen;
        _city.Visible = cityOpen;
        _shownRevision = _session.Revision; _shownResearchOpen = _researchOpen; _shownCityState = _city.State;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        BuildTop();
        if (cityOpen) _city.Build(_session.City!);
        else BuildLeft();
        BuildResearchDialog();
        BuildGovernmentDialog();
        BuildModal();
        BuildFeed();
        _shownModalRevision = _modalRevision;
        _shownGovernmentRevision = _governmentRevision;
        _message.Text = _session.Message;
        int idle = _session.UnitsNeedingOrders;
        _endTurn.Text = idle > 0 ? $"End turn ({idle} waiting)" : "End turn";
        if (MapRenderer.LogTimings) GD.Print($"ui rebuild: {clock.ElapsedMilliseconds} ms (city open: {cityOpen})");
    }

    private bool NeedsRefresh() => _shownRevision != _session.Revision || _shownResearchOpen != _researchOpen
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

    private void BuildTop()
    {
        Ui.Clear(_top);
        var s = _session.Summary();
        var row = new HFlowContainer();
        row.AddThemeConstantOverride("h_separation", 10);
        row.AddThemeConstantOverride("v_separation", 4);
        string date = s.CalendarDate is { } calendar ? $"  ·  {calendar.FormattedDate}" : "";
        row.AddChild(Ui.Text($"Turn {s.Turn}{date}  ·  {s.Era}", 15, Colors.White, wrap: false));
        int goldenAge = _session.GoldenAgeTurnsRemaining;
        if (goldenAge > 0) row.AddChild(Ui.Text($"Golden Age · {goldenAge} turns", 14, Ui.Accent, wrap: false));
        row.AddChild(Ui.Text($"Gold {_session.Me.Treasury} ({Labels.Signed(s.Net)})", 15, s.Net < 0 ? Ui.Bad : Ui.Accent, wrap: false));
        row.AddChild(ResearchButton(s));
        row.AddChild(RateBox("Science", RateKind.Research, s.Rates.Research));
        row.AddChild(RateBox("Culture", RateKind.Culture, s.Rates.Culture));
        row.AddChild(RateBox("Espionage", RateKind.Espionage, s.Rates.Espionage));
        var anarchy = _session.Me.Extensions.Get<GovernmentPlayerState>(_session.Sim.Content)?.AnarchyTurns ?? 0;
        row.AddChild(Ui.Button(anarchy > 0 ? $"Civics · {anarchy}t anarchy" : "Civics", ToggleGovernment));
        AddPanelButtons(row);
        row.AddChild(Ui.Text($"{s.Cities} cities  ·  {s.Units} units", 14, Ui.Dim, wrap: false));
        _top.AddChild(row);
    }

    private Control ResearchButton(PlayerSummary s)
    {
        var r = s.Research;
        string text = r == null ? "Choose research!" : $"{r.Name} {r.Progress}/{r.Cost}" + (r.Turns is int t ? $" ({t}t)" : "");
        var button = Ui.Button(text, ToggleResearch, "Open the research list (R)");
        button.ClipText = true;
        button.CustomMinimumSize = new Vector2(170, 0);
        if (r == null) button.AddThemeColorOverride("font_color", Ui.Bad);
        return button;
    }

    private Control RateBox(string name, RateKind kind, long percent)
    {
        var box = Ui.HBox(2);
        box.AddChild(Ui.Text($"{name} {percent}%", 14, Colors.White, wrap: false));
        box.AddChild(Ui.Button("−", () => _session.ShiftRate(kind, -10), $"10% less {name}"));
        box.AddChild(Ui.Button("+", () => _session.ShiftRate(kind, 10), $"10% more {name}"));
        return box;
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

    private void BuildResearchDialog()
    {
        _researchDialog.Visible = _researchOpen && _session.City == null;
        Ui.Clear(_researchDialog);
        if (!_researchDialog.Visible) return;
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
        float width = cards ? 940 : 620;
        _researchDialog.CustomMinimumSize = new Vector2(width, 0);
        _researchDialog.AddChild(box);
        _researchDialog.Position = (GetViewport().GetVisibleRect().Size - new Vector2(width, 520)) / 2;
    }

    internal static string StripMarkup(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\{[^}]*\}|\[[^\]]*\]", "");
}
