using System.Text;
using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>City screen: ledgers on the left, production and citizens on the right, the city's tiles on the map between.
/// Both panels are built once and refreshed in place; each section rebuilds only when the key it renders moved.</summary>
internal sealed partial class CityUi : Control
{


    private const float LeftWidth = 370f, RightWidth = 440f, TopBarHeight = GameUi.TopBarHeight;
    private static readonly string[] StatNames = ["Production", "Commerce", "Culture", "Espionage", "Maintenance", "Great people"];

    private readonly GameSession _session;
    private readonly IconCache _icons;
    private readonly PanelContainer _left = Ui.Panel();
    private readonly PanelContainer _right = Ui.Panel();
    private ProductionTab _tab = ProductionTab.All;
    private string _filter = "";
    private bool _showBlocked;
    private int _localState;
    private CityScreen? _screen;
    private int _screenCity = -1;

    // Left panel skeleton. Every widget here and in InitRightPanel is created once, so the panels keep their canvas
    // item RIDs (and with them their scrape pattern) and their scroll position across a refresh.
    private readonly Label _leftTitle = Ui.Text("", 21, Colors.White, wrap: false);
    private readonly VBoxContainer _statusBox = Ui.VBox(6);
    private readonly Label _foodText = Ui.Text("", 14);
    private readonly ProgressBar _foodBar = Ui.Bar(0, 1, Ui.Good);
    private readonly Label[] _statValues = new Label[StatNames.Length];
    private readonly Label _happyText = Ui.Text("", 14);
    private readonly Label _healthText = Ui.Text("", 14);
    private readonly VBoxContainer[] _ledgers = new VBoxContainer[5];
    private readonly string?[] _shownLedgerKeys = new string?[5];
    private string? _shownStatusKey;
    private Color? _foodFill;

    /// <summary>Changes when a local choice (tab, filter, unavailable toggle) changes, so the owner knows to rebuild.
    /// Only the click and keystroke handlers write it, so the per-frame check stays one integer comparison, and a counter
    /// cannot repeat an earlier value the way a derived hash could.</summary>
    public int State => _localState;

    private void LocalStateChanged() => _localState++;

    public CityUi(GameSession session, IconCache icons)
    {
        _session = session; _icons = icons;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _left.SetAnchorsPreset(LayoutPreset.LeftWide);
        _left.OffsetTop = TopBarHeight + 4; _left.OffsetRight = LeftWidth; _left.OffsetBottom = -4;
        _right.SetAnchorsPreset(LayoutPreset.RightWide);
        _right.OffsetTop = TopBarHeight + 4; _right.OffsetLeft = -RightWidth; _right.OffsetBottom = -4;
        InitLeftPanel();
        InitRightPanel();
        AddChild(_left);
        AddChild(_right);
    }

    /// <summary>
    /// Drops the local choices and the cached description when the city screen closes. Without this the next city opened
    /// ten turns later still started under the previous city's name filter, and the last description (with its ledger and
    /// tile lists) stayed reachable from the closed panel.
    /// </summary>
    public void Close()
    {
        _screen = null;
        _screenCity = -1;
        _tab = ProductionTab.All;
        _filter = "";
        _showBlocked = false;
        // The widgets persist, so the reset has to reach them too; their handlers write the same values back.
        if (_filterBox.Text.Length > 0) _filterBox.Text = "";
        if (_showBlockedCheck.ButtonPressed) _showBlockedCheck.ButtonPressed = false;
    }

    /// <summary>Refreshes both panels in place for the selected city.</summary>
    public void Build(City city)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var screen = _session.DescribeCity(city);
        if (screen == null) return;
        long describe = clock.ElapsedMilliseconds;
        _screen = screen;
        _screenCity = city.Id;
        RefreshLeft(screen);
        RefreshRight(city, screen);
        if (MapRenderer.LogTimings) GD.Print($"city screen: describe {describe} ms, controls {clock.ElapsedMilliseconds - describe} ms");
    }

    /// <summary>
    /// Redraws only the parts a local input (tab, filter, unavailable toggle) changes. Such an input touches no simulation
    /// state, and describing the city costs tens of milliseconds on a developed one, so the cached description is reused:
    /// an open city screen needs one choice list per keystroke rather than a whole screen.
    /// </summary>
    public void RefreshForLocalInput()
    {
        if (_screen is not { } screen || _session.Sim.State.FindCity(_screenCity) is not { } city) return;
        RefreshChoices(city, screen);
    }

    /// <summary>
    /// Runs a persistent widget's callback against the city that has this id in the current simulation. The panels keep
    /// their widgets across refreshes and a game load replaces the whole simulation, so a callback must never act on
    /// the City object it was built with; it acts on the live one, and does nothing when that city is gone.
    /// </summary>
    private void WithCity(int id, Action<City> action)
    {
        if (_session.Sim.State.FindCity(id) is { } city) action(city);
    }

    // --- left: numbers and ledgers ----------------------------------------------------------------

    /// <summary>Builds the left panel's one scroll container and its fixed widgets once.</summary>
    private void InitLeftPanel()
    {
        var head = Ui.HBox();
        head.AddChild(_leftTitle);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        head.AddChild(Ui.Button("Close", _session.ClearSelection, "Back to the map (Esc)"));
        var stats = new GridContainer { Columns = 2 };
        for (int i = 0; i < StatNames.Length; i++)
        {
            stats.AddChild(Ui.Text(StatNames[i], 14, Ui.Dim));
            _statValues[i] = Ui.Text("", 14);
            stats.AddChild(_statValues[i]);
        }
        var food = Ui.VBox(2);
        food.AddChild(_foodText);
        food.AddChild(_foodBar);
        var wellbeing = Ui.VBox(2);
        wellbeing.AddChild(_happyText);
        wellbeing.AddChild(_healthText);
        var box = Ui.VBox(6);
        box.AddChild(head);
        _statusBox.Visible = false;
        box.AddChild(_statusBox);
        box.AddChild(food);
        box.AddChild(stats);
        box.AddChild(wellbeing);
        for (int i = 0; i < _ledgers.Length; i++) { _ledgers[i] = Ui.VBox(6); _ledgers[i].Visible = false; box.AddChild(_ledgers[i]); }
        _left.AddChild(Ui.Scroll(box));
    }

    /// <summary>
    /// Updates the left panel in place. The title, food, stats and wellbeing are rewritten, and only the status block
    /// and the five ledgers rebuild, each when the text it renders changed. The keys are worked out here, which only
    /// runs on a world revision or a local tab click, never on the 60 Hz frame path.
    /// </summary>
    private void RefreshLeft(CityScreen c)
    {
        Ui.Set(_leftTitle, $"{c.Name}  (size {c.Population})");
        RefreshStatus(c);
        FillFood(c);
        FillStats(c);
        FillWellbeing(c);
        RefreshLedger(0, "Food", c.FoodLedger);
        RefreshLedger(1, "Production", c.ProductionLedger);
        RefreshLedger(2, "Commerce", c.CommerceLedger);
        RefreshLedger(3, "Happiness", c.HappinessLedger);
        RefreshLedger(4, "Health", c.HealthLedger);
    }

    /// <summary>Rebuilds the status block only when something it renders moved, then adds it in its original order.</summary>
    private void RefreshStatus(CityScreen c)
    {
        // The city id leads the key because some of these widgets act on the city they were built for. Every field is
        // separated, so two different tuples can never format to the same text.
        var key = new StringBuilder().Append(c.Id);
        key.Append('|').Append(c.OccupationTurns).Append('|').Append(c.Disorder).Append('|').Append(c.IsPuppet)
            .Append('|').Append(c.PendingConquestChoice);
        PendingCityConquest? conquest = null;
        if (c.PendingConquestChoice)
        {
            conquest = _session.DescribePendingConquests().FirstOrDefault(item => item.CityId == c.Id);
            key.Append('|').Append(conquest?.CanRaze);
        }
        foreach (var r in c.Religions) key.Append('|').Append(r.Id).Append('~').Append(r.Name).Append('~').Append(r.Holy).Append('~').Append(r.State);
        foreach (var corp in c.Corporations) key.Append('|').Append(corp.Id).Append('~').Append(corp.Name).Append('~').Append(corp.Headquarters).Append('~').Append(corp.Active);
        if (c.Stability is { } s)
        {
            key.Append('|').Append(s.Status).Append('~').Append(s.Index).Append('~').Append(s.ChangePerTurn).Append('~')
                .Append(s.CooldownTurns).Append('~').Append(s.CanSuppress);
            foreach (var row in s.Ledger) key.Append('~').Append(row.Text);
        }
        foreach (var w in c.WonderScripts) key.Append('|').Append(w.Name).Append('~').Append(w.Active).Append('~').Append(w.TurnsToSpawn).Append('~').Append(w.SpawnUnit);
        string identity = key.ToString();
        if (identity == _shownStatusKey) return;
        _shownStatusKey = identity;
        Ui.Clear(_statusBox);
        if (c.OccupationTurns > 0) _statusBox.AddChild(Ui.Text($"OCCUPATION DISORDER ({c.OccupationTurns} turns remaining): nothing is produced.", 15, Ui.Bad));
        else if (c.Disorder) _statusBox.AddChild(Ui.Text("CIVIL DISORDER: nothing is produced.", 15, Ui.Bad));
        if (c.IsPuppet)
        {
            _statusBox.AddChild(Ui.Text("PUPPET CITY: autonomous governance (-50% city-count maintenance & unhappiness).", 14, Ui.Accent));
            _statusBox.AddChild(Ui.Button("Annex City", () => WithCity(c.Id, city => _session.AnnexCity(city))));
        }
        if (c.PendingConquestChoice) AddConquestChoices(_statusBox, c, conquest);
        AddReligions(_statusBox, c);
        AddCorporations(_statusBox, c);
        AddStability(_statusBox, c);
        AddWonderScripts(_statusBox, c);
        _statusBox.Visible = _statusBox.GetChildCount() > 0;
    }

    private void AddConquestChoices(VBoxContainer box, CityScreen screen, PendingCityConquest? conquest)
    {
        var actions = Ui.HBox(6);
        actions.AddChild(Ui.Button("Puppet (Keep)", () => _session.ResolveCityConquest(screen.Id, CityConquestOption.Puppet)));
        actions.AddChild(Ui.Button("Raze City", () => _session.ResolveCityConquest(screen.Id, CityConquestOption.Raze), enabled: conquest?.CanRaze == true));
        box.AddChild(actions);
    }

    private void FillFood(CityScreen c)
    {
        string grow = c.FoodSurplus > 0 ? (c.TurnsToGrow is int t ? $"grows in {t} turns" : "") : c.FoodSurplus < 0 ? "starving" : "no growth";
        Ui.Set(_foodText, $"Food {c.FoodStored}/{c.FoodThreshold}   ({Labels.Signed(c.FoodSurplus)} per turn, {grow})");
        Ui.SetBar(_foodBar, c.FoodStored, c.FoodThreshold, $"{c.FoodStored}/{c.FoodThreshold}");
        var tint = c.FoodSurplus < 0 ? Ui.Bad : Ui.Good;
        if (_foodFill != tint)
        {
            _foodBar.AddThemeStyleboxOverride("fill", BrassSkin.BarFill(tint));
            _foodFill = tint;
        }
    }

    private void FillStats(CityScreen c)
    {
        Ui.Set(_statValues[0], $"{c.Production} per turn");
        Ui.Set(_statValues[1], $"{c.CommercePerTurn.Gold} gold, {c.CommercePerTurn.Research} research");
        Ui.Set(_statValues[2], $"{c.CommercePerTurn.Culture} per turn" + (c.CultureLevel is { } l ? $"  ({l})" : ""));
        Ui.Set(_statValues[3], $"{c.CommercePerTurn.Espionage} per turn");
        Ui.Set(_statValues[4], $"{c.Maintenance} gold");
        Ui.Set(_statValues[5], c.GreatPeopleThreshold > 0 ? $"{c.GreatPeopleProgress}/{c.GreatPeopleThreshold}  ·  +{c.GreatPeopleRate}/turn" : "—");
    }

    private void FillWellbeing(CityScreen c)
    {
        Ui.Set(_happyText, $"Happy {c.Happy}  ·  Unhappy {c.Unhappy}" + (c.Angry > 0 ? $"  ·  Angry {c.Angry}" : ""));
        Ui.Tint(_happyText, c.Unhappy > c.Happy ? Ui.Bad : Ui.Good);
        Ui.Set(_healthText, $"Healthy {c.Healthy}  ·  Unhealthy {c.Unhealthy}");
        Ui.Tint(_healthText, c.Unhealthy > c.Healthy ? Ui.Bad : Ui.Good);
    }

    private void RefreshLedger(int slot, string title, IReadOnlyList<LedgerRow> rows)
    {
        var key = new StringBuilder(title);
        foreach (var row in rows) key.Append('\n').Append(row.Amount < 0 ? '-' : '+').Append(row.Text);
        string identity = key.ToString();
        if (identity == _shownLedgerKeys[slot]) return;
        _shownLedgerKeys[slot] = identity;
        Ui.Clear(_ledgers[slot]);
        _ledgers[slot].Visible = rows.Count > 0;
        if (rows.Count == 0) return;
        _ledgers[slot].AddChild(Ui.Text(title, 15, Ui.Accent));
        foreach (var row in rows) _ledgers[slot].AddChild(Ui.Text(row.Text, 13, row.Amount < 0 ? Ui.Bad : Ui.Dim));
    }

    private void AddReligions(VBoxContainer box, CityScreen c)
    {
        if (c.Religions.Count == 0) return;
        var row = Ui.HBox(6);
        foreach (var r in c.Religions)
        {
            row.AddChild(Ui.Icon(_icons.TextureOf(r.Id), 24));
            row.AddChild(Ui.Text(r.Name + (r.Holy ? " ★ holy city" : "") + (r.State ? " (state)" : ""), 13, r.Holy ? Ui.Accent : Ui.Dim, wrap: false));
        }
        box.AddChild(row);
    }

    private void AddStability(VBoxContainer box, CityScreen c)
    {
        if (c.Stability is not { } s) return;
        box.AddChild(Ui.Heading($"Stability: {s.Status} ({s.Index})"));
        string extra = s.CooldownTurns > 0 ? $", rebellion cooldown {s.CooldownTurns} turns" : "";
        box.AddChild(Ui.Text($"{Labels.Signed(s.ChangePerTurn)} per turn{extra}", 13, s.Status == CityStabilityStatus.Stable ? Ui.Dim : Ui.Bad));
        foreach (var row in s.Ledger) box.AddChild(Ui.Text(row.Text, 12, Ui.Dim));
        if (s.CanSuppress) box.AddChild(Ui.Button("Suppress Rebellion", () => _session.SuppressRebellion(c.Id), "Pay to reduce instability", size: 13));
    }

    private void AddWonderScripts(VBoxContainer box, CityScreen c)
    {
        foreach (var w in c.WonderScripts)
        {
            string line = !w.Active ? "inactive" : w.TurnsToSpawn is int t ? $"next {w.SpawnUnit} in {t} turns" : "active";
            box.AddChild(Ui.Text($"{w.Name}: {line}", 13, Ui.Dim));
        }
    }

    private void AddCorporations(VBoxContainer box, CityScreen c)
    {
        if (c.Corporations.Count == 0) return;
        var row = Ui.HBox(6);
        foreach (var corp in c.Corporations)
        {
            row.AddChild(Ui.Icon(_icons.TextureOf(corp.Id), 24));
            row.AddChild(Ui.Text(corp.Name + (corp.Headquarters ? " ★ headquarters" : "") + (corp.Active ? "" : " (inactive)"), 13, corp.Headquarters ? Ui.Accent : Ui.Dim, wrap: false));
        }
        box.AddChild(row);
    }
}
