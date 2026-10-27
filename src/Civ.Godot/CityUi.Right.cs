using System.Text;
using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class CityUi
{
    // The right panel skeleton, created once by InitRightPanel. RefreshRight writes values in place and rebuilds a
    // section only when the text it renders changed, so reordering the queue keeps every other plate's pattern.
    private readonly VBoxContainer _queueBox = Ui.VBox(6);
    private readonly Label _queueEmpty = Ui.Text("Nothing queued. Pick something below.", 13, Ui.Bad);
    private readonly List<QueueLine> _queueLines = new();
    private readonly VBoxContainer _hurryBox = Ui.VBox(6);
    private readonly VBoxContainer _conscriptBox = Ui.VBox(6);
    private readonly HFlowContainer _choiceTabs = new();
    private readonly ButtonGroup _tabGroup = new();
    private readonly List<Button> _choiceTabButtons = new();
    private readonly LineEdit _filterBox = new()
    {
        PlaceholderText = "Filter by name", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClearButtonEnabled = true,
    };
    private readonly Label _noMatches = Ui.Text("", 13, Ui.Dim);
    private readonly CheckBox _showBlockedCheck = new();
    private readonly VBoxContainer _choicesBox = Ui.VBox(6);
    private readonly Label _citizensHeading = Ui.Heading("");
    private readonly VBoxContainer _citizensBox = Ui.VBox(6);
    private readonly VBoxContainer _tradeBox = Ui.VBox(6);
    private readonly Label _tradeHeading = Ui.Heading("");
    private readonly VBoxContainer _tradeRows = Ui.VBox(6);
    private readonly Label _tradeEmpty = Ui.Text("No active routes.", 13, Ui.Dim);
    private readonly VBoxContainer _specialistsBox = Ui.VBox(6);
    private readonly Label _buildingsText = Ui.Text("", 13, Ui.Dim);
    private int _queueCity = -1;
    private string? _shownTabsKey, _shownHurryKey, _shownConscriptKey, _shownChoicesKey, _shownCitizensKey;
    private string? _shownTradeKey, _shownSpecialistsKey;

    /// <summary>One production-queue row: created once per position and updated in place, so a reorder keeps its
    /// widgets and its plate pattern. <see cref="Index"/> is rewritten on every refresh and the callbacks read it at
    /// click time, so a row acts on the entry it currently shows.</summary>
    private sealed class QueueLine
    {
        public VBoxContainer Line = null!;
        public TextureRect Icon = null!;
        public Label Text = null!;
        public Button Earlier = null!, Later = null!, Remove = null!;
        public ProgressBar Bar = null!;
        public int Index;
    }

    /// <summary>Builds the right panel's one scroll container and its fixed widgets once.</summary>
    private void InitRightPanel()
    {
        var box = Ui.VBox(6);
        box.AddChild(Ui.Heading("Production queue"));
        _queueBox.AddChild(_queueEmpty);
        box.AddChild(_queueBox);
        _hurryBox.Visible = false;
        box.AddChild(_hurryBox);
        _conscriptBox.Visible = false;
        box.AddChild(_conscriptBox);
        box.AddChild(Ui.Heading("Add to queue"));
        _filterBox.Text = _filter;
        _filterBox.TextChanged += text => { _filter = text; LocalStateChanged(); };
        box.AddChild(_filterBox);
        _choiceTabs.AddThemeConstantOverride("h_separation", 4);
        _choiceTabs.AddThemeConstantOverride("v_separation", 4);
        _showBlockedCheck.Text = "Show unavailable";
        _showBlockedCheck.ButtonPressed = _showBlocked;
        _showBlockedCheck.FocusMode = FocusModeEnum.None;
        _showBlockedCheck.Toggled += on => { _showBlocked = on; LocalStateChanged(); };
        _choiceTabs.AddChild(_showBlockedCheck);
        box.AddChild(_choiceTabs);
        _noMatches.Visible = false;
        box.AddChild(_noMatches);
        _choicesBox.Visible = false;
        box.AddChild(_choicesBox);
        box.AddChild(_citizensHeading);
        box.AddChild(_citizensBox);
        box.AddChild(_tradeBox);
        _tradeBox.AddChild(_tradeHeading);
        _tradeBox.AddChild(_tradeRows);
        _tradeBox.AddChild(_tradeEmpty);
        box.AddChild(_specialistsBox);
        _specialistsBox.Visible = false;
        box.AddChild(Ui.Heading("Buildings"));
        box.AddChild(_buildingsText);
        _right.AddChild(Ui.Scroll(box));
    }

    /// <summary>
    /// Updates the right panel in place. Each section compares the text it renders against the key it last drew and
    /// only rebuilds when that moved; the queue keeps one row per position and rewrites it instead. The keys are
    /// worked out here, on the world-revision path only, never on the 60 Hz frame path.
    /// </summary>
    private void RefreshRight(City city, CityScreen c)
    {
        RefreshQueue(c);
        RefreshHurry(c);
        RefreshConscript(c);
        RefreshChoices(city, c);
        RefreshCitizens(c);
        RefreshTrade(c);
        RefreshSpecialists(c);
        Ui.Set(_buildingsText, c.Buildings.Count == 0 ? "None" : string.Join(", ", c.Buildings));
    }

    private void RefreshQueue(CityScreen c)
    {
        // Row identity is the city and the row count: a reorder rewrites the rows in place and never replaces them.
        if (_queueCity != c.Id)
        {
            foreach (var line in _queueLines) { _queueBox.RemoveChild(line.Line); line.Line.QueueFree(); }
            _queueLines.Clear();
            _queueCity = c.Id;
        }
        while (_queueLines.Count > c.Queue.Count)
        {
            var last = _queueLines[^1];
            _queueBox.RemoveChild(last.Line);
            last.Line.QueueFree();
            _queueLines.RemoveAt(_queueLines.Count - 1);
        }
        while (_queueLines.Count < c.Queue.Count) _queueBox.AddChild(NewQueueLine(c.Id).Line);
        _queueEmpty.Visible = c.Queue.Count == 0;
        for (int i = 0; i < c.Queue.Count; i++)
        {
            var row = c.Queue[i];
            var line = _queueLines[i];
            line.Index = row.Index;
            var texture = _icons.TextureOf(row.Id);
            if (line.Icon.Texture != texture) line.Icon.Texture = texture;
            Ui.Set(line.Text, $"{row.Name}" + (row.Turns is int t ? $"  ({t} turns)" : "  (process)"));
            line.Earlier.Disabled = c.IsPuppet || row.Index == 0;
            line.Later.Disabled = c.IsPuppet || row.Index >= c.Queue.Count - 1;
            if (line.Remove.Disabled != c.IsPuppet) line.Remove.Disabled = c.IsPuppet;
            line.Bar.Visible = !row.IsContinuous;
            if (!row.IsContinuous) Ui.SetBar(line.Bar, row.Progress, row.Cost, $"{row.Progress}/{row.Cost}");
        }
    }

    private QueueLine NewQueueLine(int cityId)
    {
        QueueLine line = null!;
        var icon = Ui.Icon(null, 28);
        var text = Ui.Text("", 14);
        var earlier = Ui.Button("▲", () => WithCity(cityId, city => _session.MoveInQueue(city, line.Index, -1)), "Earlier", size: 12);
        var later = Ui.Button("▼", () => WithCity(cityId, city => _session.MoveInQueue(city, line.Index, 1)), "Later", size: 12);
        var remove = Ui.Button("✕", () => WithCity(cityId, city => _session.RemoveFromQueue(city, line.Index)), "Remove", size: 12);
        var head = Ui.HBox(4);
        head.AddChild(icon);
        head.AddChild(text);
        head.AddChild(earlier);
        head.AddChild(later);
        head.AddChild(remove);
        var bar = Ui.Bar(0, 1, Ui.Accent, "0/1");
        var body = Ui.VBox(1);
        body.AddChild(head);
        body.AddChild(bar);
        line = new QueueLine { Line = body, Icon = icon, Text = text, Earlier = earlier, Later = later, Remove = remove, Bar = bar };
        _queueLines.Add(line);
        return line;
    }

    private void RefreshHurry(CityScreen c)
    {
        var key = new StringBuilder().Append(c.Id).Append(c.IsPuppet ? '1' : '0');
        foreach (var option in c.Hurry)
            key.Append('\n').Append(option.Id).Append('|').Append(option.Name).Append('|').Append(option.Gold)
                .Append('|').Append(option.Population).Append('|').Append(option.Blocked);
        string identity = key.ToString();
        _hurryBox.Visible = c.Hurry.Count > 0;
        if (identity == _shownHurryKey) return;
        _shownHurryKey = identity;
        Ui.Clear(_hurryBox);
        foreach (var option in c.Hurry)
        {
            var id = option.Id;
            string cost = option.Gold > 0 ? $"{option.Gold} gold" : $"{option.Population} population";
            _hurryBox.AddChild(Ui.Button($"Hurry: {cost}", () => WithCity(c.Id, city => _session.Hurry(city, id)), option.Blocked ?? option.Name, option.Blocked == null && !c.IsPuppet, 13));
        }
    }

    private void RefreshConscript(CityScreen c)
    {
        var option = c.Conscript;
        string details = option == null ? "" : $"Minimum population: {option.MinPopulation}. Experience: {option.Experience}. "
            + $"Draft anger: {option.AngerTurns} turns. Player limit: {option.MaxPlayerDrafts} per turn.";
        string identity = option == null ? $"{c.Id}" : $"{c.Id}|{option.UnitId}|{option.Name}|{option.PopulationCost}|{option.Blocked}|{details}|{c.IsPuppet}";
        _conscriptBox.Visible = option != null;
        if (identity == _shownConscriptKey) return;
        _shownConscriptKey = identity;
        Ui.Clear(_conscriptBox);
        if (option == null) return;
        _conscriptBox.AddChild(Ui.Button($"Draft {option.Name}: {option.PopulationCost} population", () => WithCity(c.Id, city => _session.Conscript(city)),
            option.Blocked ?? details, option.Blocked == null && !c.IsPuppet, 13));
    }

    private void RefreshChoices(City city, CityScreen screen)
    {
        var tabs = _session.ProductionTabs;
        string tabsKey = string.Join("\u001f", tabs.Select(t => t.Id + "|" + t.Label));
        if (tabsKey != _shownTabsKey)
        {
            _shownTabsKey = tabsKey;
            foreach (var button in _choiceTabButtons) { _choiceTabs.RemoveChild(button); button.QueueFree(); }
            _choiceTabButtons.Clear();
            for (int index = 0; index < tabs.Count; index++)
            {
                var id = tabs[index].Id;
                var button = Ui.Button(tabs[index].Label, () => SelectTab(id), size: 13);
                button.ToggleMode = true;
                button.ButtonGroup = _tabGroup;
                _choiceTabs.AddChild(button);
                _choiceTabs.MoveChild(button, index);
                _choiceTabButtons.Add(button);
            }
        }
        // The group keeps one tab pressed, so a click on the open tab leaves it open and never clears the list.
        for (int index = 0; index < _choiceTabButtons.Count && index < tabs.Count; index++)
            if (_choiceTabButtons[index].ButtonPressed != (tabs[index].Id == _tabId))
                _choiceTabButtons[index].ButtonPressed = tabs[index].Id == _tabId;
        if (tabs.Count == 0) { _choicesBox.Visible = false; _noMatches.Visible = false; return; }
        var tab = tabs.FirstOrDefault(t => t.Id == _tabId) ?? tabs[0];
        // Trimming once keeps a stray space from hiding every row.
        string filter = _filter.Trim();
        var list = _session.ProductionRows(city, tab, filter, _showBlocked);
        var shown = list.Rows;
        var key = new StringBuilder().Append(screen.Id).Append('|').Append(tab.Id).Append('\u001f').Append(filter)
            .Append('|').Append(screen.IsPuppet);
        foreach (var choice in shown)
            key.Append('\n').Append(choice.Id).Append('|').Append(choice.Name).Append('|').Append(choice.Cost).Append('|')
                .Append(choice.Turns).Append('|').Append(choice.Available).Append('|').Append(choice.IsContinuous).Append('|').Append(choice.Tooltip);
        string identity = key.ToString();
        if (identity != _shownChoicesKey)
        {
            _shownChoicesKey = identity;
            Ui.Clear(_choicesBox);
            foreach (var choice in shown) _choicesBox.AddChild(ChoiceButton(screen.Id, choice, !screen.IsPuppet));
        }
        _choicesBox.Visible = shown.Count > 0;
        // A filter that matches only blocked rows still needs an answer, or the section reads as if nothing exists.
        bool hint = shown.Count == 0 && filter.Length > 0;
        _noMatches.Visible = hint;
        if (hint) Ui.Set(_noMatches, list.Matches > 0
            ? $"Nothing available matches \u201c{filter}\u201d."
            : $"Nothing matches \u201c{filter}\u201d.");
    }

    /// <summary>Opens a tab. A click on the open tab is not a state change, so nothing rebuilds.</summary>
    private void SelectTab(string id)
    {
        if (_tabId == id) return;
        _tabId = id;
        LocalStateChanged();
    }

    private Control ChoiceButton(int cityId, ProductionChoice choice, bool cityCanQueue)
    {
        var row = Ui.HBox(6);
        row.AddChild(Ui.Icon(_icons.TextureOf(choice.Id), 28));
        string detail = choice.IsContinuous ? "converts production" : $"{choice.Cost}" + (choice.Turns is int t ? $" / {t}t" : "");
        var button = Ui.Button($"{choice.Name}   {detail}", () => WithCity(cityId, city => _session.Enqueue(city, choice.Kind, choice.Id, append: true)), choice.Tooltip, choice.Available && cityCanQueue, 13);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Alignment = HorizontalAlignment.Left;
        button.ClipText = true;
        button.CustomMinimumSize = new Vector2(220, 0);
        row.AddChild(button);
        return row;
    }

    private void RefreshCitizens(CityScreen c)
    {
        Ui.Set(_citizensHeading, $"Citizens ({c.Citizens})");
        var key = new StringBuilder().Append(c.Id).Append(c.IsPuppet ? '1' : '0').Append(c.Manual).Append(c.AvoidGrowth).Append(c.IdleCitizens);
        foreach (var tile in c.WorkedTiles) key.Append('|').Append(tile.Name).Append(tile.Food).Append(tile.Production).Append(tile.Commerce);
        foreach (var s in c.Specialists) key.Append('|').Append(s.Id).Append(s.Name).Append(s.Count).Append(s.Slots).Append(s.Effects);
        string identity = key.ToString();
        if (identity == _shownCitizensKey) return;
        _shownCitizensKey = identity;
        Ui.Clear(_citizensBox);
        _citizensBox.AddChild(Ui.Text($"{c.WorkedTiles.Count} working tiles (click map tiles to move citizens), {c.IdleCitizens} idle.", 13, Ui.Dim));
        var controls = Ui.HBox(4);
        controls.AddChild(Ui.Button(c.Manual ? "Governor: off" : "Governor: on", () => WithCity(c.Id, city => _session.AutoAssign(city)), "Let the governor assign citizens", !c.IsPuppet && c.Manual, 13));
        controls.AddChild(Ui.Button(c.AvoidGrowth ? "Avoid growth: on" : "Avoid growth: off", () => WithCity(c.Id, city => _session.ToggleAvoidGrowth(city)), enabled: !c.IsPuppet, size: 13));
        _citizensBox.AddChild(controls);
        foreach (var tile in c.WorkedTiles)
            _citizensBox.AddChild(Ui.Text($"{tile.Name}: {tile.Food}F {tile.Production}P {tile.Commerce}C", 13, Ui.Dim));
        foreach (var s in c.Specialists) _citizensBox.AddChild(SpecialistLine(c.Id, s, !c.IsPuppet));
    }

    private Control SpecialistLine(int cityId, SpecialistRow s, bool canAssign)
    {
        var row = Ui.HBox(4);
        var id = s.Id;
        row.AddChild(Ui.Text($"{s.Name}: {s.Count}/{s.Slots}" + (s.Effects.Length > 0 ? $"  ({s.Effects})" : ""), 13));
        row.AddChild(Ui.Button("−", () => WithCity(cityId, city => _session.ChangeSpecialist(city, id, -1)), enabled: canAssign && s.Count > 0 && id != _session.CitizenSpecialist, size: 12));
        row.AddChild(Ui.Button("+", () => WithCity(cityId, city => _session.ChangeSpecialist(city, id, 1)), enabled: canAssign && s.Count < s.Slots, size: 12));
        return row;
    }

    private void RefreshTrade(CityScreen c)
    {
        Ui.Set(_tradeHeading, $"Trade routes ({c.TradeRoutes.Count}/{c.MaxTradeRoutes})");
        var key = new StringBuilder().Append(c.Id);
        foreach (var route in c.TradeRoutes)
        {
            var partner = _session.Sim.State.FindCity(route.PartnerCity);
            key.Append('|').Append(route.PartnerCity).Append(partner?.Name).Append(route.Foreign).Append(route.Overseas).Append(route.ProfitHundredths);
        }
        string identity = key.ToString();
        if (identity == _shownTradeKey) return;
        _shownTradeKey = identity;
        Ui.Clear(_tradeRows);
        foreach (var route in c.TradeRoutes)
        {
            var partner = _session.Sim.State.FindCity(route.PartnerCity);
            string flags = string.Join(", ", new[] { route.Foreign ? "foreign" : "domestic", route.Overseas ? "overseas" : "" }.Where(x => x.Length > 0));
            string partnerName = partner?.Name ?? "City " + route.PartnerCity;
            _tradeRows.AddChild(Ui.Text($"{partnerName}  ·  {route.ProfitHundredths / 100} gold  ({flags})", 13, Ui.Dim));
        }
        _tradeRows.Visible = c.TradeRoutes.Count > 0;
        _tradeEmpty.Visible = c.TradeRoutes.Count == 0;
    }

    private void RefreshSpecialists(CityScreen c)
    {
        var key = new StringBuilder().Append(c.Id);
        foreach (var (id, count) in c.SettledSpecialists.OrderBy(p => p.Key, StringComparer.Ordinal))
            key.Append('|').Append(id).Append(count).Append(SpecialistName(id));
        string identity = key.ToString();
        if (identity == _shownSpecialistsKey) return;
        _shownSpecialistsKey = identity;
        Ui.Clear(_specialistsBox);
        _specialistsBox.Visible = c.SettledSpecialists.Count > 0;
        if (c.SettledSpecialists.Count == 0) return;
        _specialistsBox.AddChild(Ui.Heading("Settled great people"));
        foreach (var (id, count) in c.SettledSpecialists.OrderBy(p => p.Key, StringComparer.Ordinal))
            _specialistsBox.AddChild(Ui.Text($"{SpecialistName(id)} ×{count}", 13, Ui.Dim));
    }

    private string SpecialistName(string id) =>
        _session.Sim.Content.TryGet<Civ.Content.Schema.SpecialistDef>(id, out var specialist) ? specialist.Name : id;
}
