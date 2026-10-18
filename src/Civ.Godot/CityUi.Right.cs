using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class CityUi
{
    private void BuildRight(City city, CityScreen c)
    {
        Ui.Clear(_right);
        var box = Ui.VBox(6);
        box.AddChild(Ui.Heading("Production queue"));
        AddQueue(box, city, c);
        AddHurry(box, city, c);
        AddConscript(box, city, c);
        box.AddChild(Ui.Heading("Add to queue"));
        AddChoices(box, city, c);
        box.AddChild(Ui.Heading($"Citizens ({c.Citizens})"));
        AddCitizens(box, city, c);
        AddTradeRoutes(box, c);
        AddSettledSpecialists(box, c);
        box.AddChild(Ui.Heading("Buildings"));
        box.AddChild(Ui.Text(c.Buildings.Count == 0 ? "None" : string.Join(", ", c.Buildings), 13, Ui.Dim));
        _right.AddChild(Ui.Scroll(box));
    }

    private void AddQueue(VBoxContainer box, City city, CityScreen c)
    {
        if (c.Queue.Count == 0) box.AddChild(Ui.Text("Nothing queued. Pick something below.", 13, Ui.Bad));
        foreach (var row in c.Queue)
        {
            var line = Ui.VBox(1);
            var head = Ui.HBox(4);
            var index = row.Index;
            head.AddChild(Ui.Icon(_icons.TextureOf(row.Id), 28));
            head.AddChild(Ui.Text($"{row.Name}" + (row.Turns is int t ? $"  ({t} turns)" : "  (process)"), 14));
            head.AddChild(Ui.Button("▲", () => _session.MoveInQueue(city, index, -1), "Earlier", !c.IsPuppet && index > 0, 12));
            head.AddChild(Ui.Button("▼", () => _session.MoveInQueue(city, index, 1), "Later", !c.IsPuppet && index < c.Queue.Count - 1, 12));
            head.AddChild(Ui.Button("✕", () => _session.RemoveFromQueue(city, index), "Remove", enabled: !c.IsPuppet, size: 12));
            line.AddChild(head);
            if (!row.IsContinuous) line.AddChild(Ui.Bar(row.Progress, row.Cost, Ui.Accent, $"{row.Progress}/{row.Cost}"));
            box.AddChild(line);
        }
    }

    private void AddHurry(VBoxContainer box, City city, CityScreen c)
    {
        foreach (var option in c.Hurry)
        {
            var id = option.Id;
            string cost = option.Gold > 0 ? $"{option.Gold} gold" : $"{option.Population} population";
            box.AddChild(Ui.Button($"Hurry: {cost}", () => _session.Hurry(city, id), option.Blocked ?? option.Name, option.Blocked == null && !c.IsPuppet, 13));
        }
    }

    private void AddConscript(VBoxContainer box, City city, CityScreen c)
    {
        if (c.Conscript is not { } option) return;
        string details = $"Minimum population: {option.MinPopulation}. Experience: {option.Experience}. "
            + $"Draft anger: {option.AngerTurns} turns. Player limit: {option.MaxPlayerDrafts} per turn.";
        box.AddChild(Ui.Button($"Draft {option.Name}: {option.PopulationCost} population", () => _session.Conscript(city),
            option.Blocked ?? details, option.Blocked == null && !c.IsPuppet, 13));
    }

    private void AddChoices(VBoxContainer box, City city, CityScreen screen)
    {
        var tabs = Ui.HBox(4);
        var kinds = _session.ProductionKinds;
        for (int index = 0; index < kinds.Count; index++)
        {
            var t = index;
            var button = Ui.Button(kinds[index].Label, () => _tab = t, size: 13);
            button.ToggleMode = true; button.ButtonPressed = _tab == index;
            tabs.AddChild(button);
        }
        var blocked = new CheckBox { Text = "Show unavailable", ButtonPressed = _showBlocked, FocusMode = FocusModeEnum.None };
        blocked.Toggled += on => _showBlocked = on;
        tabs.AddChild(blocked);
        box.AddChild(tabs);
        if (kinds.Count == 0) return;
        var kind = kinds[Math.Clamp(_tab, 0, kinds.Count - 1)];
        var choices = _session.ProductionChoices(city, kind.Id);
        foreach (var choice in choices.Where(p => p.Available)) box.AddChild(ChoiceButton(city, choice, !screen.IsPuppet));
        if (!_showBlocked) return;
        foreach (var choice in choices.Where(p => !p.Available).Take(MaxBlockedShown))
            box.AddChild(ChoiceButton(city, choice, !screen.IsPuppet));
    }

    private Control ChoiceButton(City city, ProductionChoice choice, bool cityCanQueue)
    {
        var row = Ui.HBox(6);
        row.AddChild(Ui.Icon(_icons.TextureOf(choice.Id), 28));
        string detail = choice.IsContinuous ? "converts production" : $"{choice.Cost}" + (choice.Turns is int t ? $" / {t}t" : "");
        var button = Ui.Button($"{choice.Name}   {detail}", () => _session.Enqueue(city, choice.Kind, choice.Id, append: true), choice.Tooltip, choice.Available && cityCanQueue, 13);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Alignment = HorizontalAlignment.Left;
        button.ClipText = true;
        button.CustomMinimumSize = new Vector2(220, 0);
        row.AddChild(button);
        return row;
    }

    private void AddTradeRoutes(VBoxContainer box, CityScreen c)
    {
        box.AddChild(Ui.Heading($"Trade routes ({c.TradeRoutes.Count}/{c.MaxTradeRoutes})"));
        foreach (var route in c.TradeRoutes)
        {
            var partner = _session.Sim.State.FindCity(route.PartnerCity);
            string flags = string.Join(", ", new[] { route.Foreign ? "foreign" : "domestic", route.Overseas ? "overseas" : "" }.Where(x => x.Length > 0));
            string partnerName = partner?.Name ?? "City " + route.PartnerCity;
            box.AddChild(Ui.Text($"{partnerName}  ·  {route.ProfitHundredths / 100} gold  ({flags})", 13, Ui.Dim));
        }
        if (c.TradeRoutes.Count == 0) box.AddChild(Ui.Text("No active routes.", 13, Ui.Dim));
    }

    private void AddSettledSpecialists(VBoxContainer box, CityScreen c)
    {
        if (c.SettledSpecialists.Count == 0) return;
        box.AddChild(Ui.Heading("Settled great people"));
        foreach (var (id, count) in c.SettledSpecialists.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            string name = _session.Sim.Content.TryGet<Civ.Content.Schema.SpecialistDef>(id, out var specialist) ? specialist.Name : id;
            box.AddChild(Ui.Text($"{name} ×{count}", 13, Ui.Dim));
        }
    }

    private void AddCitizens(VBoxContainer box, City city, CityScreen c)
    {
        box.AddChild(Ui.Text($"{c.WorkedTiles.Count} working tiles (click map tiles to move citizens), {c.IdleCitizens} idle.", 13, Ui.Dim));
        var controls = Ui.HBox(4);
        controls.AddChild(Ui.Button(c.Manual ? "Governor: off" : "Governor: on", () => _session.AutoAssign(city), "Let the governor assign citizens", !c.IsPuppet && c.Manual, 13));
        controls.AddChild(Ui.Button(c.AvoidGrowth ? "Avoid growth: on" : "Avoid growth: off", () => _session.ToggleAvoidGrowth(city), enabled: !c.IsPuppet, size: 13));
        box.AddChild(controls);
        foreach (var tile in c.WorkedTiles)
            box.AddChild(Ui.Text($"{tile.Name}: {tile.Food}F {tile.Production}P {tile.Commerce}C", 13, Ui.Dim));
        foreach (var s in c.Specialists) box.AddChild(SpecialistLine(city, s, !c.IsPuppet));
    }

    private Control SpecialistLine(City city, SpecialistRow s, bool canAssign)
    {
        var row = Ui.HBox(4);
        var id = s.Id;
        row.AddChild(Ui.Text($"{s.Name}: {s.Count}/{s.Slots}" + (s.Effects.Length > 0 ? $"  ({s.Effects})" : ""), 13));
        row.AddChild(Ui.Button("−", () => _session.ChangeSpecialist(city, id, -1), enabled: canAssign && s.Count > 0 && id != _session.CitizenSpecialist, size: 12));
        row.AddChild(Ui.Button("+", () => _session.ChangeSpecialist(city, id, 1), enabled: canAssign && s.Count < s.Slots, size: 12));
        return row;
    }
}
