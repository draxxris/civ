using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class GameUi
{
    private void BuildLeft()
    {
        Ui.Clear(_left);
        var box = Ui.VBox(8);
        if (_session.DescribeUnit(_session.Unit) is { } unit) box.AddChild(UnitSection(unit));
        else box.AddChild(Ui.Text("Nothing selected. Click a unit or city.", 14, Ui.Dim));
        box.AddChild(new HSeparator());
        box.AddChild(Ui.Heading("Cities"));
        box.AddChild(CityList());
        _left.AddChild(Ui.Scroll(box));
    }

    private Control UnitSection(UnitPanel unit)
    {
        var box = Ui.VBox(4);
        var head = Ui.HBox(8);
        head.AddChild(Ui.Icon(_icons.TextureOf(_session.Unit!.Type), 48));
        var title = Ui.VBox(0);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AddChild(Ui.Text(unit.Name, 19, Colors.White));
        title.AddChild(Ui.Text(unit.Stats, 13, Ui.Dim));
        head.AddChild(title);
        box.AddChild(head);
        box.AddChild(Ui.Text($"Moves {unit.Moves}   Health {unit.Health}%", 14));
        box.AddChild(Ui.Bar(unit.Health, 100, unit.Health > 50 ? Ui.Good : Ui.Bad));
        box.AddChild(Ui.Text($"{unit.Experience}   ·   {unit.Activity}", 13, Ui.Dim));
        if (unit.Promotions.Count > 0) box.AddChild(Ui.Text("Promotions: " + string.Join(", ", unit.Promotions), 13, Ui.Dim));
        box.AddChild(Ui.Text("Orders", 14, Ui.Accent));
        box.AddChild(ActionGrid(unit));
        box.AddChild(Ui.Text("Right-click a tile to move or attack. Tab: next unit.", 12, Ui.Dim));
        return box;
    }

    private Control ActionGrid(UnitPanel unit)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var action in unit.Actions)
        {
            var button = Ui.Button(action.Label, () => _session.RunAction(action.Id), action.Disabled ?? action.Tooltip, action.Command != null, 13);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            grid.AddChild(button);
        }
        return grid;
    }

    private Control CityList()
    {
        var box = Ui.VBox(3);
        foreach (var city in _session.Sim.State.Cities.Where(c => c.Owner == GameSession.Human).OrderBy(c => c.Id))
        {
            var id = city.Id;
            box.AddChild(Ui.Button($"{city.Name}  ({city.Population})", () => _session.SelectCity(id), "Open the city screen"));
        }
        if (box.GetChildCount() == 0) box.AddChild(Ui.Text("None yet. Found one with a settler (B).", 13, Ui.Dim));
        return box;
    }
}
