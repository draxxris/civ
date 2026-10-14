using Godot;

namespace Civ.Client;

/// <summary>Religion, pedia and load dialogs, the save button, and the turn notification feed.</summary>
internal sealed partial class GameUi
{
    private enum Modal { None, Religion, Pedia, Load }

    private const string SaveDirectory = "user://saves";
    private const string QuickSave = SaveDirectory + "/quicksave.json";

    private readonly PanelContainer _modal = Ui.Panel(margin: 12);
    private readonly PanelContainer _feed = Ui.Panel(new Color(0.05f, 0.06f, 0.08f, 0.7f), 6);
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

    private void CloseModal() { _modalKind = Modal.None; _modalRevision++; }

    private void AddPanelButtons(HBoxContainer row)
    {
        row.AddChild(Ui.Button("Religion", () => OpenModal(Modal.Religion), "State religion and the religions of the world"));
        row.AddChild(Ui.Button("Pedia", () => OpenModal(Modal.Pedia), "Browse every tech, unit, building and more"));
        row.AddChild(Ui.Button("Save", QuickSaveGame, "Quick save (F5)"));
        row.AddChild(Ui.Button("Load", () => OpenModal(Modal.Load), "Load a saved game (F9)"));
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

    private void BuildModal()
    {
        _modal.Visible = _modalKind != Modal.None && _session.City == null;
        Ui.Clear(_modal);
        if (!_modal.Visible) return;
        var box = Ui.VBox(6);
        var head = Ui.HBox();
        head.AddChild(Ui.Heading(_modalKind.ToString()));
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        head.AddChild(Ui.Button("Close", CloseModal));
        box.AddChild(head);
        switch (_modalKind)
        {
            case Modal.Religion: BuildReligion(box); break;
            case Modal.Pedia: BuildPedia(box); break;
            case Modal.Load: BuildLoad(box); break;
        }
        _modal.CustomMinimumSize = new Vector2(720, 0);
        _modal.AddChild(box);
        _modal.Position = (GetViewport().GetVisibleRect().Size - new Vector2(720, 560)) / 2;
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
