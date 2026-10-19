using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class GameUi
{
    private void BuildResearchList(VBoxContainer box, IReadOnlyList<TechChoice> choices)
    {
        var list = Ui.VBox(3);
        foreach (var tech in choices)
        {
            var row = Ui.VBox(2);
            row.AddChild(Ui.Button($"{tech.Name} — {tech.Cost} beakers [{tech.Era}]",
                () => ChooseResearch(tech.Id), StripMarkup(tech.Help)));
            if (_session.FreeResearchCredits > 0)
                row.AddChild(Ui.Button("Use free technology", () => ChooseResearch(tech.Id, true)));
            list.AddChild(row);
        }
        box.AddChild(Ui.Scroll(list, 420));
    }

    private void BuildResearchCards(VBoxContainer box, IReadOnlyList<TechChoice> choices)
    {
        var row = Ui.HBox(8);
        foreach (var tech in choices)
        {
            var card = Ui.Panel(margin: 12);
            card.CustomMinimumSize = new Vector2(220, 320);
            var content = Ui.VBox(10);
            content.AddChild(Ui.Icon(_icons.TextureOf(tech.Id), 48));
            content.AddChild(Ui.Heading(tech.Name));
            content.AddChild(Ui.Text(tech.Era, 13, Ui.Dim));
            content.AddChild(Ui.Text($"{tech.Cost} beakers" + (tech.Turns is int turns ? $" · {turns} turns" : "")));
            var help = Ui.Text(StripMarkup(tech.Help), 12);
            help.CustomMinimumSize = new Vector2(190, 0);
            content.AddChild(Ui.Scroll(help, 100));
            content.AddChild(Ui.Button("Research", () => ChooseResearch(tech.Id)));
            if (_session.FreeResearchCredits > 0)
                content.AddChild(Ui.Button("Use free technology", () => ChooseResearch(tech.Id, true)));
            card.AddChild(content);
            row.AddChild(card);
        }
        box.AddChild(row);
        box.AddChild(Ui.Text("The hand stays until research completes. Acquired cards leave the hand without replacing the remaining cards.", 13, Ui.Dim));
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
