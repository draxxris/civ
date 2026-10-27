using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class GameUi
{
    // Research dialog skeleton, built once by InitResearchDialog and refreshed in place. The choice widgets rebuild
    // only when the choice set, its layout or the free-credit state changes, so raising the Science budget rewrites
    // their captions instead of re-creating them (and re-randomising every plate's scrape pattern).
    private readonly Label _researchTitle = Ui.Heading("");
    private readonly VBoxContainer _researchList = Ui.VBox(3);
    private ScrollContainer _researchListScroll = null!;
    private readonly HBoxContainer _researchCardsRow = Ui.HBox(8);
    private readonly VBoxContainer _researchCards = Ui.VBox(6);
    private readonly Label _researchEmpty = Ui.Text("No eligible technologies are available.", 14, Ui.Dim);
    private readonly Label _researchFree = Ui.Text("", 14, Ui.Accent);
    private readonly List<ResearchChoiceUi> _researchChoiceUis = new();
    private Button _researchBrowse = null!;
    private string? _shownResearchChoicesKey;

    /// <summary>One research choice's widgets: a single button in the list layout, a card's labels otherwise.</summary>
    private sealed class ResearchChoiceUi
    {
        public Button? Row;
        public Label? Name, Era, Cost, Help;

        /// <summary>Writes the captions of an already-built choice, which move when costs or turn counts do.</summary>
        public void Set(TechChoice choice, bool cards)
        {
            if (!cards) { Ui.Set(Row!, $"{choice.Name} — {choice.Cost} beakers [{choice.Era}]"); return; }
            Ui.Set(Name!, choice.Name);
            Ui.Set(Era!, choice.Era);
            Ui.Set(Cost!, $"{choice.Cost} beakers" + (choice.Turns is int turns ? $" · {turns} turns" : ""));
            Ui.Set(Help!, StripMarkup(choice.Help));
        }
    }

    private void InitResearchDialog()
    {
        var box = Ui.VBox(6);
        var head = Ui.HBox();
        head.AddChild(_researchTitle);
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        head.AddChild(Ui.Button("Close", () => _researchOpen = false));
        box.AddChild(head);
        _researchBrowse = Ui.Button("Browse tech tree", () => OpenModal(Modal.TechTree));
        _researchBrowse.Visible = false;
        box.AddChild(_researchBrowse);
        _researchListScroll = Ui.Scroll(_researchList, 420);
        box.AddChild(_researchListScroll);
        // Hidden until the cards layout is in use: a wrapped label measured at zero width would otherwise grow the
        // dialog to the height of its wrapped lines before the first refresh.
        _researchCards.Visible = false;
        _researchCards.AddChild(_researchCardsRow);
        _researchCards.AddChild(Ui.Text("The hand stays until research completes. Acquired cards leave the hand without replacing the remaining cards.", 13, Ui.Dim));
        box.AddChild(_researchCards);
        _researchEmpty.Visible = false;
        box.AddChild(_researchEmpty);
        _researchFree.Visible = false;
        box.AddChild(_researchFree);
        _researchDialog.AddChild(box);
    }

    /// <summary>
    /// Refreshes the research dialog in place: the title, the free-credit line and the browse button are written every
    /// time, and the choice widgets are rebuilt only when their identity key moved. The key is worked out here, on the
    /// revision-change path only, never on the 60 Hz frame path.
    /// </summary>
    private void RefreshResearchChoices(IReadOnlyList<TechChoice> choices, bool cards)
    {
        _researchListScroll.Visible = !cards;
        _researchCards.Visible = cards;
        int free = _session.FreeResearchCredits;
        string key = (cards ? "cards" : "list") + (free > 0 ? "+free" : "") + "\n" + string.Join("\n", choices.Select(c => c.Id));
        if (key != _shownResearchChoicesKey)
        {
            _shownResearchChoicesKey = key;
            _researchChoiceUis.Clear();
            Ui.Clear(_researchList);
            Ui.Clear(_researchCardsRow);
            foreach (var tech in choices)
                _researchChoiceUis.Add(cards ? AddResearchCard(tech) : AddResearchRow(tech));
        }
        for (int i = 0; i < choices.Count && i < _researchChoiceUis.Count; i++) _researchChoiceUis[i].Set(choices[i], cards);
    }

    private ResearchChoiceUi AddResearchRow(TechChoice tech)
    {
        var ui = new ResearchChoiceUi { Row = Ui.Button("", () => ChooseResearch(tech.Id), StripMarkup(tech.Help)) };
        var row = Ui.VBox(2);
        row.AddChild(ui.Row);
        if (_session.FreeResearchCredits > 0) row.AddChild(Ui.Button("Use free technology", () => ChooseResearch(tech.Id, true)));
        _researchList.AddChild(row);
        return ui;
    }

    private ResearchChoiceUi AddResearchCard(TechChoice tech)
    {
        var card = Ui.Panel(margin: 12);
        card.CustomMinimumSize = new Vector2(220, 320);
        var content = Ui.VBox(10);
        content.AddChild(Ui.Icon(_icons.TextureOf(tech.Id), 48));
        var ui = new ResearchChoiceUi { Name = Ui.Heading(""), Era = Ui.Text("", 13, Ui.Dim), Cost = Ui.Text("", 14), Help = Ui.Text("", 12) };
        content.AddChild(ui.Name);
        content.AddChild(ui.Era);
        content.AddChild(ui.Cost);
        ui.Help.CustomMinimumSize = new Vector2(190, 0);
        content.AddChild(Ui.Scroll(ui.Help, 100));
        content.AddChild(Ui.Button("Research", () => ChooseResearch(tech.Id)));
        if (_session.FreeResearchCredits > 0) content.AddChild(Ui.Button("Use free technology", () => ChooseResearch(tech.Id, true)));
        card.AddChild(content);
        _researchCardsRow.AddChild(card);
        return ui;
    }

    private void ChooseResearch(string id, bool free = false)
    {
        if (_session.Research(id, free)) _researchOpen = false;
    }

    private void BuildTechTree(VBoxContainer box)
    {
        if (_session.DescribeTechTree() is not { } tree)
        {
            box.AddChild(Ui.Text("The tech tree is not enabled."));
            return;
        }
        box.AddChild(new TechTreeBrowser(tree, _session.FreeResearchCredits, (id, free) => _session.Research(id, free)));
    }
}
