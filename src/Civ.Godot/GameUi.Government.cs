using Godot;

namespace Civ.Client;

internal sealed partial class GameUi
{
    private readonly PanelContainer _governmentDialog = Ui.Panel(margin: 12);
    private readonly Dictionary<string, string> _proposedCivics = new();
    private bool _governmentOpen;
    private int _governmentRevision;
    private int _shownGovernmentRevision = -1;

    public void ToggleGovernment()
    {
        _governmentOpen = !_governmentOpen;
        _researchOpen = false;
        _proposedCivics.Clear();
        _governmentRevision++;
    }

    private void BuildGovernmentDialog()
    {
        _governmentDialog.Visible = _governmentOpen;
        Ui.Clear(_governmentDialog);
        if (!_governmentOpen) return;
        var proposed = _proposedCivics.Values.ToArray();
        var government = _session.Government(proposed);
        var box = Ui.VBox(8);
        var head = Ui.HBox();
        head.AddChild(Ui.Heading("Civics"));
        head.AddChild(Ui.Button("Close", ToggleGovernment));
        box.AddChild(head);
        box.AddChild(Ui.Text($"Anarchy: {government.AnarchyTurns} turns remaining   ·   Revolution cooldown: {government.CooldownTurns} turns", 14));
        var list = Ui.VBox(6);
        foreach (var group in government.Groups) AddCivicOption(list, group);
        box.AddChild(Ui.Scroll(list, 410));
        var preview = _session.PreviewCivics(proposed);
        box.AddChild(Ui.Text($"Proposed revolution: {preview.AnarchyTurns} anarchy turns, {preview.CooldownTurns} cooldown turns", 14, Ui.Accent));
        if (preview.Blocked != null) box.AddChild(Ui.Text(preview.Blocked, 13, Ui.Dim));
        box.AddChild(Ui.Button("Adopt selected civics", () =>
        {
            if (_session.AdoptCivics(proposed)) ToggleGovernment();
        }, preview.Blocked, preview.Blocked == null));
        _governmentDialog.CustomMinimumSize = new Vector2(700, 0);
        _governmentDialog.AddChild(box);
        _governmentDialog.Position = (GetViewport().GetVisibleRect().Size - new Vector2(700, 600)) / 2;
    }

    private void AddCivicOption(VBoxContainer box, CivicGroup group)
    {
        box.AddChild(Ui.Heading(group.Name));
        var grid = new GridContainer { Columns = 3 };
        foreach (var civic in group.Choices)
        {
            var button = Ui.Button(civic.Name + (civic.Active ? " (active)" : ""), () =>
            {
                _proposedCivics[group.Id] = civic.Id;
                _governmentRevision++;
            }, CivicTooltip(civic), civic.Blockers.Count == 0, 13);
            button.ClipText = true;
            button.CustomMinimumSize = new Vector2(200, 0);
            button.ToggleMode = true;
            button.ButtonPressed = group.Selected == civic.Id;
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            grid.AddChild(button);
        }
        box.AddChild(grid);
    }

    private static string CivicTooltip(CivicChoice civic) => civic.Blockers.Count > 0
        ? string.Join("\n", civic.Blockers) : StripMarkup(civic.Help);
}
