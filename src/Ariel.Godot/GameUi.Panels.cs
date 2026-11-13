using Ariel.Game;
using Godot;
namespace Ariel.Client;

/// <summary>Conquest, pedia and load dialogs, the screens mods register, quick save and load, and the turn
/// notification feed.</summary>
internal sealed partial class GameUi
{
    private enum Modal { None, Conquest, Pedia, TechTree, Load, Screen }
    internal const string SaveDirectory = "user://saves";
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
    private Modal _shownModalKind;
    private LineEdit _pediaSearch = null!;
    private HFlowContainer _pediaTabsRow = null!;
    private HBoxContainer _pediaSplit = null!;
    private VBoxContainer _pediaList = null!;
    private VBoxContainer _pediaArticleBody = null!;
    private ScrollContainer _pediaArticleScroll = null!;
    private readonly List<(Button Button, string? Category)> _pediaTabs = new();
    private string? _shownPediaTabsKey;
    private string? _shownPediaListKey;
    private string? _shownPediaArticleKey;
    // A screen's pinned header, scrolling body and pinned footer each have their own renderer, so each keeps its
    // widgets until what it draws changes.
    private ViewRenderer? _screenRenderer, _screenHeaderRenderer, _screenFooterRenderer;
    private VBoxContainer? _screenBody, _screenHeader, _screenFooter;
    private string? _shownScreenId;

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
        if (_modalKind == Modal.Screen) _session.Screens.Close();
        _modalKind = _modalKind == kind ? Modal.None : kind;
        _modalRevision++;
        if (_modalKind == Modal.Pedia) ResetPediaKeys();
    }

    /// <summary>Opens a registered screen, or closes it when it is the one open.</summary>
    public void ToggleScreen(string id)
    {
        if (_modalKind == Modal.Screen && _session.Screens.OpenId == id) { CloseModal(); return; }
        _researchOpen = false;
        if (_governmentOpen) ToggleGovernment();
        if (!_session.Screens.Open(id)) return;
        _modalKind = Modal.Screen;
        _modalRevision++;
    }

    /// <summary>Opens a base dialog by name, for captures. False when no dialog has that name.</summary>
    internal bool OpenDebugPanel(string name)
    {
        if (!Enum.TryParse<Modal>(name, true, out var kind) || kind is Modal.None or Modal.Screen) return false;
        if (kind == Modal.Pedia) _pediaSelected = _session.Pedia.Search(null, "").FirstOrDefault()?.Id;
        if (_modalKind != kind) OpenModal(kind);
        return true;
    }

    private void CloseModal()
    {
        if (_modalKind == Modal.Screen) _session.Screens.Close();
        _modalKind = Modal.None;
        _modalRevision++;
    }

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
        string path = ProjectSettings.GlobalizePath(QuickSave);
        // A failed save has already said why; do not cover it with a success message.
        if (_session.SaveGame(path)) _session.SetMessage("Game saved to " + path);
        // The new file belongs in an open Load modal's list, which only re-reads the folder on this counter.
        _modalRevision++;
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
            box.AddChild(Ui.Text($"T{note.Turn}  {note.Text}", 12, ViewRenderer.ColourOf(note.Tone)));
        _feed.AddChild(box);
    }

    // --- modal dispatch -----------------------------------------------------------------------------

    /// <summary>
    /// Hides the modal without clearing it while it is closed; the caller decides when to rebuild it. The pedia is
    /// built once per open and then refreshed in place, so clicking a row no longer re-creates its whole body.
    /// </summary>
    private void BuildModal()
    {
        _modal.Visible = _modalKind != Modal.None && _session.City == null;
        if (!_modal.Visible)
        {
            // The tree is kept for the next showing, but it no longer stands for the selected kind: the city screen can
            // hide the modal while another kind is picked, and the pedia's in-place path must not mistake that old tree
            // for its own.
            _shownModalKind = Modal.None;
            return;
        }
        if (_modalKind == Modal.Pedia && _shownModalKind == Modal.Pedia) { RefreshPedia(); return; }
        if (_modalKind == Modal.Screen && _shownModalKind == Modal.Screen && _shownScreenId == _session.Screens.OpenId) { RefreshScreen(); return; }
        _shownModalKind = _modalKind;
        Ui.Clear(_modal);
        var box = Ui.VBox(6);
        var head = Ui.HBox();
        var view = _modalKind == Modal.Screen ? _session.Screens.View : null;
        if (_modalKind == Modal.Screen && view is null) { CloseModal(); _modal.Visible = false; return; }
        var title = Ui.Heading(view?.Title ?? _modalKind.ToString());
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(title);
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        head.AddChild(Ui.Button("Close", CloseModal));
        box.AddChild(head);
        BuildModalContent(box);
        float width = _modalKind == Modal.TechTree ? 1100 : view?.Width ?? 720;
        _modal.CustomMinimumSize = new Vector2(width, 0);
        _modal.AddChild(box);
        _modal.Position = (GetViewport().GetVisibleRect().Size - new Vector2(width, 560)) / 2;
    }

    private void BuildModalContent(VBoxContainer box)
    {
        switch (_modalKind)
        {
            case Modal.Conquest: BuildConquest(box); break;
            case Modal.Screen: BuildScreen(box); break;
            case Modal.Pedia: BuildPedia(box); break;
            case Modal.Load: BuildLoad(box); break;
            case Modal.TechTree: BuildTechTree(box); break;
        }
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

    // --- mod screens ------------------------------------------------------------------------------------

    /// <summary>Builds the open screen once: its pinned header, its body and its pinned footer. Later describes refresh
    /// each in place through its renderer, which rebuilds only the widgets whose drawing changed.</summary>
    private void BuildScreen(VBoxContainer box)
    {
        _screenRenderer ??= new ViewRenderer(_icons, action => _session.Screens.Press(action));
        _screenHeaderRenderer ??= new ViewRenderer(_icons, action => _session.Screens.Press(action));
        _screenFooterRenderer ??= new ViewRenderer(_icons, action => _session.Screens.Press(action));
        _screenRenderer.Forget(); _screenHeaderRenderer.Forget(); _screenFooterRenderer.Forget();
        _screenHeader = Ui.VBox(6);
        _screenBody = Ui.VBox(6);
        _screenFooter = Ui.VBox(6);
        _shownScreenId = _session.Screens.OpenId;
        var view = _session.Screens.View!;
        box.AddChild(_screenHeader);
        // A screen laid out in columns scrolls each column; anything else scrolls as one.
        if (view.Body.Any(e => e is Columns)) box.AddChild(_screenBody);
        else box.AddChild(Ui.Scroll(_screenBody, 420));
        box.AddChild(_screenFooter);
        ShowScreen(view);
    }

    private void RefreshScreen()
    {
        if (_session.Screens.View is not { } view || _screenBody is null) { CloseModal(); return; }
        ShowScreen(view);
    }

    private void ShowScreen(ScreenView view)
    {
        _screenHeaderRenderer!.Show(_screenHeader!, view.Header);
        _screenRenderer!.Show(_screenBody!, view.Body);
        _screenFooterRenderer!.Show(_screenFooter!, view.Footer);
        // An empty region takes no room, so a screen without a header or footer looks as it did.
        _screenHeader!.Visible = view.Header.Count > 0;
        _screenFooter!.Visible = view.Footer.Count > 0;
    }

    // --- pedia ----------------------------------------------------------------------------------------------

    private void BuildPedia(VBoxContainer box)
    {
        _pediaSearch = new LineEdit { Text = _pediaQuery, PlaceholderText = "Search (Enter)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _pediaSearch.TextSubmitted += text => { _pediaQuery = text; _modalRevision++; };
        box.AddChild(_pediaSearch);
        _pediaTabsRow = new HFlowContainer();
        box.AddChild(_pediaTabsRow);
        _pediaList = Ui.VBox(1);
        var left = Ui.Scroll(_pediaList, 320);
        left.CustomMinimumSize = new Vector2(250, 320);
        left.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        _pediaSplit = Ui.HBox(8);
        _pediaSplit.AddChild(left);
        _pediaSplit.AddChild(PediaArticle());
        box.AddChild(_pediaSplit);
        ResetPediaKeys();
        RefreshPedia();
    }

    private void ResetPediaKeys()
    {
        _shownPediaTabsKey = null;
        _shownPediaListKey = null;
        _shownPediaArticleKey = null;
    }

    /// <summary>
    /// Refreshes the pedia in place: the tabs only when the category list changed (otherwise only their disabled
    /// state), the list only when the category, the query or the ordered result ids changed, and the article only when
    /// the selection changed. A row click therefore keeps every list button, its canvas item and its plate pattern.
    /// The keys are only worked out here, on the modal-revision path, never on the per-frame path.
    /// </summary>
    private void RefreshPedia()
    {
        var pedia = _session.Pedia;
        // The field holds what the user is typing; write Text only when it cannot clobber the caret.
        if (!_pediaSearch.HasFocus() && _pediaSearch.Text != _pediaQuery) _pediaSearch.Text = _pediaQuery;

        string tabsKey = string.Join("\n", pedia.Categories);
        if (tabsKey != _shownPediaTabsKey)
        {
            _shownPediaTabsKey = tabsKey;
            _pediaTabs.Clear();
            Ui.Clear(_pediaTabsRow);
            AddPediaTab("All", null);
            foreach (var category in pedia.Categories) AddPediaTab(category, category);
        }
        // A tab shows "selected" by being disabled, exactly as the old build passed `enabled`.
        foreach (var (button, category) in _pediaTabs) button.Disabled = _pediaCategory == category;

        var entries = pedia.Search(_pediaCategory, _pediaQuery).Take(300).ToList();
        string listKey = (_pediaCategory ?? "") + "\n" + _pediaQuery + "\n" + string.Join("\n", entries.Select(e => e.Id));
        if (listKey != _shownPediaListKey)
        {
            _shownPediaListKey = listKey;
            Ui.Clear(_pediaList);
            foreach (var entry in entries)
            {
                var id = entry.Id;
                _pediaList.AddChild(Ui.Button(entry.Title, () => { _pediaSelected = id; _modalRevision++; }, entry.Category, true, 12));
            }
        }

        // Everything the article renders comes from the static catalog, so the selection is its whole key.
        if (_pediaSelected != _shownPediaArticleKey)
        {
            _shownPediaArticleKey = _pediaSelected;
            Ui.Clear(_pediaArticleBody);
            FillPediaArticle(pedia.Find(_pediaSelected ?? ""));
            // The article scroll is kept alive now, so a new entry has to start at the top as a new scroll did.
            _pediaArticleScroll.ScrollVertical = 0;
        }
    }

    private void AddPediaTab(string text, string? category)
    {
        var button = Ui.Button(text, () => { _pediaCategory = category; _modalRevision++; }, null, _pediaCategory != category, 12);
        _pediaTabs.Add((button, category));
        _pediaTabsRow.AddChild(button);
    }

    /// <summary>Builds the article's one scroll container and body; a selection change only refills the body.</summary>
    private Control PediaArticle()
    {
        _pediaArticleBody = Ui.VBox(4);
        _pediaArticleScroll = Ui.Scroll(_pediaArticleBody, 320);
        _pediaArticleScroll.CustomMinimumSize = new Vector2(430, 320);
        return _pediaArticleScroll;
    }

    /// <summary>Fills the article body with the selected entry, or the placeholder when nothing is selected.</summary>
    private void FillPediaArticle(PediaEntry? e)
    {
        var body = _pediaArticleBody;
        if (e == null) body.AddChild(Ui.Text("Pick an entry.", 14, Ui.Dim));
        else
        {
            var head = Ui.HBox(8);
            head.AddChild(Ui.Icon(_icons.TextureOf(e.Id), 40));
            head.AddChild(Ui.Text($"{e.Title}  ({e.Category})", 18, Colors.White));
            body.AddChild(head);
            if (e.Cost is int cost) body.AddChild(Ui.Text($"Cost: {cost}", 13, Ui.Accent));
            if (e.Prerequisites.Count > 0) body.AddChild(Ui.Text("Requires: " + string.Join(", ", e.Prerequisites), 13, Ui.Dim));
            foreach (var effect in e.Effects) body.AddChild(Ui.Text(effect, 13, Ui.Accent));
            foreach (var text in new[] { e.Help, e.Strategy, e.Pedia }.Where(t => t.Length > 0))
                body.AddChild(Ui.Text(StripMarkup(text), 13));
        }
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
