using Civ.Game;
using Godot;
namespace Civ.Client;

/// <summary>Religion, pedia and load dialogs, quick save and load, and the turn notification feed.</summary>
internal sealed partial class GameUi
{
    private enum Modal { None, Religion, Corporations, Espionage, Events, Conquest, Diplomacy, Pedia, TechTree, Load }
    private const string SaveDirectory = "user://saves";
    private const string QuickSave = SaveDirectory + "/quicksave.json";

    private readonly PanelContainer _modal = Ui.Panel(margin: 12);
    private readonly PanelContainer _feed = Ui.Panel(new Color(0.05f, 0.06f, 0.08f, 0.7f), 6);
    private readonly HashSet<int> _pendingConquests = new();
    private Modal _modalKind;
    private int _modalRevision;
    private int _shownModalRevision = -1;
    private string? _pediaCategory;
    private string _pediaQuery = "";
    private string? _pediaSelected;

    private void InitPanels()
    {
        _modal.SetAnchorsPreset(Control.LayoutPreset.Center);
        _modal.Visible = false;
        _feed.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _feed.OffsetTop = TopBarHeight + 4; _feed.GrowHorizontal = Control.GrowDirection.Begin;
        _feed.OffsetLeft = -330; _feed.OffsetRight = -6;
        _feed.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private void OpenModal(Modal kind)
    {
        _researchOpen = false;
        if (_governmentOpen) ToggleGovernment();
        _modalKind = _modalKind == kind ? Modal.None : kind;
        _modalRevision++;
    }

    internal void OpenDebugPanel(string name)
    {
        if (!Enum.TryParse<Modal>(name, true, out var kind) || kind == Modal.None) return;
        if (kind == Modal.Pedia) _pediaSelected = _session.Pedia.Search(null, "").FirstOrDefault()?.Id;
        if (_modalKind != kind) OpenModal(kind);
    }

    private void CloseModal() { _modalKind = Modal.None; _modalRevision++; }

    private void UpdateConquestModal(IReadOnlyList<PendingCityConquest> conquests)
    {
        bool isNew = conquests.Any(conquest => !_pendingConquests.Contains(conquest.CityId));
        _pendingConquests.Clear();
        foreach (var conquest in conquests) _pendingConquests.Add(conquest.CityId);
        if (conquests.Count == 0 && _modalKind == Modal.Conquest)
        {
            CloseModal();
            return;
        }
        if (isNew && _modalKind != Modal.Conquest) OpenModal(Modal.Conquest);
    }

    public void QuickSaveGame()
    {
        _session.SaveGame(ProjectSettings.GlobalizePath(QuickSave));
        _session.SetMessage("Game saved to " + ProjectSettings.GlobalizePath(QuickSave));
    }

    public void QuickLoadGame()
    {
        var path = ProjectSettings.GlobalizePath(QuickSave);
        if (System.IO.File.Exists(path)) _session.LoadGame(path); else _session.SetMessage("No quick save yet");
    }

    // --- notification feed -------------------------------------------------------------------------

    private void BuildFeed()
    {
        Ui.Clear(_feed);
        var notes = _session.RecentNotifications(2);
        _feed.Visible = notes.Count > 0 && _session.City == null;
        if (!_feed.Visible) return;
        var box = Ui.VBox(1);
        foreach (var note in notes.TakeLast(8))
            box.AddChild(Ui.Text($"T{note.Turn}  {note.Text}", 12, note.Kind == "combat" ? Ui.Bad : Colors.White));
        _feed.AddChild(box);
    }

    // --- modal dispatch -----------------------------------------------------------------------------

    /// <summary>Hides the modal without clearing it while it is closed; the caller decides when to rebuild it.</summary>
    private void BuildModal()
    {
        _modal.Visible = _modalKind != Modal.None && _session.City == null;
        if (!_modal.Visible) return;
        Ui.Clear(_modal);
        var box = Ui.VBox(6);
        var head = Ui.HBox();
        var title = Ui.Heading(_modalKind.ToString());
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(title);
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        head.AddChild(Ui.Button("Close", CloseModal));
        box.AddChild(head);
        BuildModalContent(box);
        float width = _modalKind == Modal.TechTree ? 1100 : 720;
        _modal.CustomMinimumSize = new Vector2(width, 0);
        _modal.AddChild(box);
        _modal.Position = (GetViewport().GetVisibleRect().Size - new Vector2(width, 560)) / 2;
    }

    private void BuildModalContent(VBoxContainer box)
    {
        switch (_modalKind)
        {
            case Modal.Religion: BuildReligion(box); break;
            case Modal.Corporations: BuildCorporations(box); break;
            case Modal.Espionage: BuildEspionage(box); break;
            case Modal.Events: BuildEvents(box); break;
            case Modal.Conquest: BuildConquest(box); break;
            case Modal.Diplomacy: BuildDiplomacy(box); break;
            case Modal.Pedia: BuildPedia(box); break;
            case Modal.Load: BuildLoad(box); break;
            case Modal.TechTree: BuildTechTree(box); break;
        }
    }

    // --- religion ---------------------------------------------------------------------------------------

    private void BuildReligion(VBoxContainer box)
    {
        var screen = _session.DescribeReligions();
        box.AddChild(Ui.Text($"State religion - {screen.StateReligionName ?? "none"}   ·   Conversion: {screen.ConversionAnarchyTurns} anarchy turns, "
            + $"{screen.CooldownTurns} cooldown   ·   Cooldown left: {screen.CooldownRemaining}", 14, Ui.Accent));
        var list = Ui.VBox(4);
        foreach (var r in screen.Religions)
        {
            var line = Ui.HBox(6);
            line.AddChild(Ui.Icon(_icons.TextureOf(r.Id), 32));
            string holy = r.HolyCityName is { } n ? $"holy city {n}" : r.Founded ? "founded" : "not founded" + (r.FoundingTech is { } t ? $" ({t})" : "");
            var info = Ui.VBox(0);
            info.AddChild(Ui.Text(r.Name + (r.IsState ? "  (state religion)" : ""), 15, Colors.White));
            info.AddChild(Ui.Text(holy, 12, Ui.Dim));
            line.AddChild(info);
            var id = r.Id;
            line.AddChild(Ui.Button("Adopt", () => _session.AdoptStateReligion(id), r.Blocked ?? StripMarkup(r.Help), r.CanAdopt));
            list.AddChild(line);
        }
        box.AddChild(Ui.Scroll(list, 340));
        box.AddChild(Ui.Button("No state religion", () => _session.AdoptStateReligion(null), screen.NoReligionBlocked,
            screen.StateReligion != null && screen.NoReligionBlocked == null));
    }

    // --- corporations, espionage, events ------------------------------------------------------------------

    private void BuildCorporations(VBoxContainer box)
    {
        var screen = _session.DescribeCorporations();
        var list = Ui.VBox(4);
        foreach (var c in screen.Corporations)
        {
            var line = Ui.HBox(6);
            line.AddChild(Ui.Icon(_icons.TextureOf(c.Id), 32));
            string state = c.Founded ? $"headquarters {c.HeadquartersName ?? "?"}, {c.Branches} cities" : "not founded";
            var info = Ui.VBox(0);
            info.AddChild(Ui.Text(c.Name, 15, Colors.White));
            info.AddChild(Ui.Text(state, 12, Ui.Dim));
            line.AddChild(info);
            list.AddChild(line);
        }
        foreach (var city in screen.Cities)
            list.AddChild(Ui.Text($"{city.CityName}: " + string.Join(", ", city.Corporations.Select(c => c.Name + (c.Headquarters ? "*" : ""))), 13, Ui.Accent));
        box.AddChild(Ui.Scroll(list, 340));
    }



    private void BuildEvents(VBoxContainer box)
    {
        var events = _session.DescribePendingEvents();
        var list = Ui.VBox(8);
        if (events.Count == 0) list.AddChild(Ui.Text("No events are waiting.", 13, Ui.Dim));
        foreach (var e in events)
        {
            list.AddChild(Ui.Text(e.Name + (e.CityName is { } n ? $" ({n})" : ""), 15, Ui.Accent));
            if (e.Help.Length > 0) list.AddChild(Ui.Text(StripMarkup(e.Help), 12, Ui.Dim));
            foreach (var o in e.Options)
            {
                string trigger = e.Trigger, option = o.Id;
                list.AddChild(Ui.Button(o.Name, () => _session.ChooseEventOption(trigger, option), o.Blocked ?? StripMarkup(o.Help), o.Blocked == null));
            }
        }
        box.AddChild(Ui.Scroll(list, 340));
    }

    private void BuildConquest(VBoxContainer box)
    {
        var conquests = _session.DescribePendingConquests();
        if (conquests.Count == 0)
        {
            box.AddChild(Ui.Text("No conquest choices are waiting.", 13, Ui.Dim));
            return;
        }
        foreach (var conquest in conquests)
        {
            box.AddChild(Ui.Text($"{conquest.Name} · population {conquest.Population} · occupation disorder {conquest.OccupationTurns} turns", 15, Ui.Accent));
            var actions = Ui.HBox(6);
            actions.AddChild(Ui.Button("Annex", () => _session.ResolveCityConquest(conquest.CityId, CityConquestOption.Annex)));
            actions.AddChild(Ui.Button("Puppet", () => _session.ResolveCityConquest(conquest.CityId, CityConquestOption.Puppet)));
            actions.AddChild(Ui.Button("Raze", () => _session.ResolveCityConquest(conquest.CityId, CityConquestOption.Raze), enabled: conquest.CanRaze));
            box.AddChild(actions);
        }
    }

    // --- diplomacy, score and victory --------------------------------------------------------------------

    private void BuildDiplomacy(VBoxContainer box)
    {
        var screen = _session.DescribeDiplomacy();
        var victory = _session.DescribeVictoryAndScores();
        var list = Ui.VBox(6);
        AddDiplomacyRivals(list, screen);
        AddDiplomacyDeals(list, screen);
        AddVictoryStatus(list, victory);
        box.AddChild(Ui.Scroll(list, 420));
    }

    private void AddDiplomacyRivals(VBoxContainer list, DiplomacyScreen screen)
    {
        if (!screen.Available) list.AddChild(Ui.Text("Diplomacy is not part of this ruleset.", 13, Ui.Dim));
        else if (screen.Rivals.Count == 0) list.AddChild(Ui.Text("You have not met any other leader yet.", 13, Ui.Dim));
        foreach (var rival in screen.Rivals) AddDiplomacyRival(list, rival);
    }

    private void AddDiplomacyRival(VBoxContainer list, DiplomacyRival rival)
    {
        list.AddChild(Ui.Text($"{rival.Leader} of {rival.Civilization} ({rival.Name})  -  {rival.Status}", 15, rival.AtWar ? Ui.Bad : Colors.White));
        var terms = new List<string>();
        if (rival.OpenBorders) terms.Add("open borders");
        if (rival.DefensivePact) terms.Add("defensive pact");
        string treatyText = terms.Count > 0 ? "  ·  " + string.Join(", ", terms) : "";
        list.AddChild(Ui.Text($"Attitude: {rival.Tier} ({rival.AttitudeScore:+#;-#;0})" + treatyText, 12, Ui.Accent));
        foreach (var line in rival.Breakdown.Where(item => item.Amount != 0))
            list.AddChild(Ui.Text($"    {line.Source}: {line.Amount:+#;-#;0}", 11, Ui.Dim));
        var actions = Ui.HBox(4);
        foreach (var action in rival.Actions)
            actions.AddChild(Ui.Button(action.Label, () => _session.DoDiplomacy(action), action.Blocked, action.Blocked == null, 12));
        list.AddChild(actions);
    }

    private void AddDiplomacyDeals(VBoxContainer list, DiplomacyScreen screen)
    {
        if (screen.Deals.Count > 0) list.AddChild(Ui.Heading("Active deals"));
        foreach (var deal in screen.Deals) AddDiplomacyDeal(list, deal);
    }

    private void AddDiplomacyDeal(VBoxContainer list, DiplomacyDealRow deal)
    {
        var line = Ui.HBox(6);
        line.AddChild(Ui.Text($"#{deal.Id} {deal.Status}: {deal.Summary}", 12, Colors.White));
        if (deal.Accept is { } accept) line.AddChild(Ui.Button(accept.Label, () => _session.DoDiplomacy(accept), accept.Blocked, accept.Blocked == null, 12));
        if (deal.Cancel is { } cancel) line.AddChild(Ui.Button(cancel.Label, () => _session.DoDiplomacy(cancel), cancel.Blocked, cancel.Blocked == null, 12));
        list.AddChild(line);
    }

    private void AddVictoryStatus(VBoxContainer list, VictoryScreen victory)
    {
        list.AddChild(Ui.Heading("Score and victory"));
        if (victory.Status is not { } status)
        {
            list.AddChild(Ui.Text("No victory rules in this ruleset.", 12, Ui.Dim));
            return;
        }
        string state = status.IsGameOver
            ? $"Game over: {victory.WinnerName ?? "no winner"} won by {victory.VictoryName ?? "unknown"} on turn {status.TurnWon}"
            : $"Game in progress  ·  turn limit {status.MaxTurns}";
        list.AddChild(Ui.Text(state, 13, status.IsGameOver ? Ui.Accent : Colors.White));
        if (victory.Conditions.Count > 0) list.AddChild(Ui.Text("Victory: " + string.Join(", ", victory.Conditions), 12, Ui.Dim));
        foreach (var score in victory.Scores)
            list.AddChild(Ui.Text($"{(score.IsLocal ? "> " : "  ")}{score.Name} ({score.Civilization}): {score.Score.Total}  "
                + $"[pop {score.Score.Population}, land {score.Score.Land}, techs {score.Score.Techs}, wonders {score.Score.Wonders}]", 12,
                score.IsLocal ? Ui.Accent : Colors.White));
    }
    // --- pedia ----------------------------------------------------------------------------------------------

    private void BuildPedia(VBoxContainer box)
    {
        var pedia = _session.Pedia;
        var search = new LineEdit { Text = _pediaQuery, PlaceholderText = "Search (Enter)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        search.TextSubmitted += text => { _pediaQuery = text; _modalRevision++; };
        box.AddChild(search);
        var tabs = new HFlowContainer();
        tabs.AddChild(Ui.Button("All", () => { _pediaCategory = null; _modalRevision++; }, null, _pediaCategory != null, 12));
        foreach (var category in pedia.Categories)
        {
            var c = category;
            tabs.AddChild(Ui.Button(c, () => { _pediaCategory = c; _modalRevision++; }, null, _pediaCategory != c, 12));
        }
        box.AddChild(tabs);
        var split = Ui.HBox(8);
        var list = Ui.VBox(1);
        foreach (var entry in pedia.Search(_pediaCategory, _pediaQuery).Take(300))
        {
            var id = entry.Id;
            list.AddChild(Ui.Button(entry.Title, () => { _pediaSelected = id; _modalRevision++; }, entry.Category, true, 12));
        }
        var left = Ui.Scroll(list, 320);
        left.CustomMinimumSize = new Vector2(250, 320);
        left.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        split.AddChild(left);
        split.AddChild(PediaArticle(pedia.Find(_pediaSelected ?? "")));
        box.AddChild(split);
    }

    private Control PediaArticle(PediaEntry? e)
    {
        var body = Ui.VBox(4);
        if (e == null) body.AddChild(Ui.Text("Pick an entry.", 14, Ui.Dim));
        else
        {
            var head = Ui.HBox(8);
            head.AddChild(Ui.Icon(_icons.TextureOf(e.Id), 40));
            head.AddChild(Ui.Text($"{e.Title}  ({e.Category})", 18, Colors.White));
            body.AddChild(head);
            if (e.Cost is int cost) body.AddChild(Ui.Text($"Cost: {cost}", 13, Ui.Accent));
            if (e.Prerequisites.Count > 0) body.AddChild(Ui.Text("Requires: " + string.Join(", ", e.Prerequisites), 13, Ui.Dim));
            foreach (var text in new[] { e.Help, e.Strategy, e.Pedia }.Where(t => t.Length > 0))
                body.AddChild(Ui.Text(StripMarkup(text), 13));
        }
        var scroll = Ui.Scroll(body, 320);
        scroll.CustomMinimumSize = new Vector2(430, 320);
        return scroll;
    }

    // --- load ----------------------------------------------------------------------------------------------------

    private void BuildLoad(VBoxContainer box)
    {
        var dir = ProjectSettings.GlobalizePath(SaveDirectory);
        var files = System.IO.Directory.Exists(dir)
            ? System.IO.Directory.GetFiles(dir, "*.json").OrderByDescending(System.IO.File.GetLastWriteTime).ToList() : [];
        if (files.Count == 0) box.AddChild(Ui.Text("No saves in " + dir, 14, Ui.Dim));
        var list = Ui.VBox(3);
        foreach (var file in files)
        {
            var path = file;
            list.AddChild(Ui.Button($"{System.IO.Path.GetFileNameWithoutExtension(path)}   ({System.IO.File.GetLastWriteTime(path):g})",
                () => { if (_session.LoadGame(path)) CloseModal(); }, path));
        }
        box.AddChild(Ui.Scroll(list, 300));
        box.AddChild(Ui.Button("Save as new slot", () =>
        {
            _session.SaveGame(System.IO.Path.Combine(ProjectSettings.GlobalizePath(SaveDirectory), $"turn{_session.Sim.State.Turn}_{DateTime.Now:HHmmss}.json"));
            _modalRevision++;
        }));
    }
}
