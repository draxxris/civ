using Ariel.Game;
using Godot;

namespace Ariel.Client;

/// <summary>Read-only rival intelligence. Private values come only from permission-filtered snapshots.</summary>
internal sealed partial class GameUi
{
    private int? _espionageRival;
    private int? _espionageCity;
    private int _espionageScroll;

    public void ToggleEspionage() => OpenModal(Modal.Espionage);

    private void BuildEspionage(VBoxContainer box)
    {
        var screen = _session.DescribeEspionage();
        if (!screen.Rivals.Any(r => r.TeamId == _espionageRival)) SelectEspionageRival(null);
        var split = Ui.HBox(10);
        var rivals = Ui.VBox(4);
        foreach (var row in screen.Rivals) AddEspionageRival(rivals, row);
        if (screen.Rivals.Count == 0) rivals.AddChild(Ui.Text("You have not met any living rival.", 13, Ui.Dim));
        var rivalScroll = Ui.Scroll(rivals, 420);
        rivalScroll.CustomMinimumSize = new Vector2(210, 420);
        rivalScroll.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        split.AddChild(rivalScroll);

        var details = Ui.VBox(6);
        if (_espionageRival is int team && _session.DescribeRivalIntelligence(team) is { } intel)
            AddRivalIntelligence(details, intel);
        else
        {
            SelectEspionageRival(null);
            details.AddChild(Ui.Text("Select a rival to inspect intelligence.", 14, Ui.Dim));
        }
        var scroll = Ui.Scroll(details, 420);
        scroll.CustomMinimumSize = new Vector2(476, 420);
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, _espionageScroll);
        scroll.GetVScrollBar().ValueChanged += value => _espionageScroll = (int)value;
        split.AddChild(scroll);
        box.AddChild(split);
        var missions = Ui.VBox(4);
        AddSelectedSpyActions(missions);
        box.AddChild(Ui.Scroll(missions, 80));
    }

    private void SelectEspionageRival(int? team)
    {
        _espionageRival = team;
        _espionageCity = null;
        _espionageScroll = 0;
    }

    private void AddEspionageRival(VBoxContainer list, EspionageRow row)
    {
        var select = Ui.Button(row.Name, () => { SelectEspionageRival(row.TeamId); _modalRevision++; },
            row.Name, _espionageRival != row.TeamId, 13);
        select.ClipText = true;
        select.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        list.AddChild(select);
        list.AddChild(Ui.Text($"{row.Points} points", 12, Ui.Dim));
        var weights = Ui.HBox(4);
        weights.AddChild(Ui.Text($"Weight {row.Weight}", 12));
        weights.AddChild(Ui.Button("−", () => _session.SetEspionageWeight(row.TeamId, row.Weight - 1), "Lower weight", row.Weight > 0));
        weights.AddChild(Ui.Button("+", () => _session.SetEspionageWeight(row.TeamId, row.Weight + 1), "Raise weight"));
        list.AddChild(weights);
    }

    private void AddRivalIntelligence(VBoxContainer list, RivalIntelligence intel)
    {
        list.AddChild(Ui.Heading(intel.Name));
        list.AddChild(Ui.Text($"{intel.Points} points · weight {intel.Weight} · spending forecast {intel.Spending} points/turn", 13, Ui.Accent));
        int turn = _session.Summary().Turn;
        AddCounterProtection(list, "Our protection against this rival", "Their missions against us", intel.ProtectionAgainstRival, turn);
        AddCounterProtection(list, "Rival protection against us", "Our missions against them", intel.RivalProtection, turn);
        foreach (var permission in intel.Permissions)
            list.AddChild(Ui.Text($"{PassiveLabel(permission.Kind)}: "
                + (permission.Unlocked ? "unlocked" : "locked") + $" · requires {permission.Threshold} points", 12,
                permission.Unlocked ? Ui.Good : Ui.Dim));
        AddRivalDemographics(list, intel.Demographics);
        AddRivalResearch(list, intel.Research);
        AddRivalCities(list, intel.Cities);
    }

    private static string PassiveLabel(PassiveEspionageKind kind) => kind switch
    {
        PassiveEspionageKind.Demographics => "Demographics",
        PassiveEspionageKind.CityVisibility => "City visibility",
        PassiveEspionageKind.InspectResearch => "Inspect research",
        PassiveEspionageKind.InvestigateCity => "Investigate city",
        _ => kind.ToString()
    };

    private static void AddCounterProtection(VBoxContainer list, string title, string direction, CounterEspionageEffect? effect, int turn)
    {
        string status = effect is null ? "inactive" : $"{Math.Max(0, effect.ExpiresTurn - turn)} turns remaining · {direction} cost {effect.CostPercent}% of normal";
        list.AddChild(Ui.Text($"{title}: {status}", 12, effect is null ? Ui.Dim : Ui.Accent));
    }

    private static void AddRivalDemographics(VBoxContainer list, RivalDemographics? demographics)
    {
        list.AddChild(Ui.Heading("Demographics and treasury"));
        if (demographics is null)
        {
            list.AddChild(Ui.Text("Locked — unlock Demographics to see statistics and each player's gold reserves.", 13, Ui.Dim));
            return;
        }
        list.AddChild(Ui.Text($"{demographics.Cities} cities · population {demographics.Population} · {demographics.Units} units", 13));
        foreach (var treasury in demographics.Treasuries)
            list.AddChild(Ui.Text($"{treasury.Name} (player {treasury.PlayerId}): {treasury.Gold} gold reserves", 13));
    }

    private static void AddRivalResearch(VBoxContainer list, RivalResearch? research)
    {
        list.AddChild(Ui.Heading("Research"));
        if (research is null)
        {
            list.AddChild(Ui.Text("Locked or unavailable — requires Inspect research.", 13, Ui.Dim));
            return;
        }
        list.AddChild(Ui.Text(research.Current is { } current
            ? $"{current.Name}: {research.Progress}/{research.Cost} research" : "No current research.", 13));
        list.AddChild(Ui.Text(research.Queue.Count == 0 ? "Research queue is empty."
            : "Research queue: " + string.Join(" → ", research.Queue.Select(t => t.Name)), 13));
    }

    private void AddRivalCities(VBoxContainer list, IReadOnlyList<RivalCityIntelligence>? cities)
    {
        list.AddChild(Ui.Heading("Cities · read-only"));
        if (cities is null)
        {
            _espionageCity = null;
            list.AddChild(Ui.Text("Locked — unlock City visibility to see rival cities.", 13, Ui.Dim));
            return;
        }
        if (!cities.Any(c => c.Id == _espionageCity)) _espionageCity = null;
        var picker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FitToLongestItem = false, ClipText = true };
        picker.AddItem(cities.Count == 0 ? "No rival cities." : "Select a city", -1);
        foreach (var city in cities) picker.AddItem($"{city.Name} (player {city.Owner})", city.Id);
        picker.Select(_espionageCity is int id ? picker.GetItemIndex(id) : 0);
        picker.ItemSelected += index => { _espionageCity = index == 0 ? null : picker.GetItemId((int)index); _modalRevision++; };
        list.AddChild(picker);
        if (cities.FirstOrDefault(c => c.Id == _espionageCity) is not { } selected) return;
        list.AddChild(Ui.Text($"{selected.Name} · {selected.Position}", 14, Ui.Accent));
        AddRivalCityDetails(list, selected.Details);
    }

    private static void AddRivalCityDetails(VBoxContainer list, RivalCityDetails? details)
    {
        if (details is null)
        {
            list.AddChild(Ui.Text("Locked or unavailable — Investigate city is required for statistics and production queues.", 13, Ui.Dim));
            return;
        }
        list.AddChild(Ui.Text($"Population {details.Population} · stored food {details.FoodStored}\n"
            + $"Food {details.Food} · production {details.Production} · commerce {details.Commerce}\n"
            + $"Happy {details.Happy} / unhappy {details.Unhappy} · healthy {details.Healthy} / unhealthy {details.Unhealthy}", 13));
        list.AddChild(Ui.Heading("Production queue · read-only"));
        if (details.Queue.Count == 0) list.AddChild(Ui.Text("Production queue is empty.", 13, Ui.Dim));
        foreach (var item in details.Queue)
        {
            string progress = item.IsContinuous ? "continuous" : $"{item.Progress}/{item.Cost}";
            string turns = item.Turns is int t ? $" · {t} turns" : "";
            list.AddChild(Ui.Text($"{item.Index + 1}. {item.Name} · {progress}{turns}", 13));
        }
    }

    private void AddSelectedSpyActions(VBoxContainer box)
    {
        var actions = _session.DescribeUnit(_session.Unit)?.Actions.Where(a => a.Id.StartsWith("spy:", StringComparison.Ordinal)).ToArray() ?? [];
        box.AddChild(Ui.Text("Spy missions use the selected unit at its location, not the inspected city.", 12, Ui.Dim));
        var row = new HFlowContainer();
        foreach (var action in actions)
        {
            string label = action.Command is ExecuteEspionageMissionCommand { Mission: EspionageMissionKind.CounterEspionage }
                ? "Counter-espionage" : action.Label;
            row.AddChild(Ui.Button(label, () => _session.RunAction(action.Id), action.Tooltip, action.Command is not null, 12));
        }
        if (actions.Length == 0) row.AddChild(Ui.Text("Select a spy in a rival city with moves and enough points for a legal mission.", 12, Ui.Dim));
        box.AddChild(row);
    }
}
