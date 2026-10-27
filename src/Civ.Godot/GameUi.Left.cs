using System.Text;
using Civ.Game;
using Godot;

namespace Civ.Client;

internal sealed partial class GameUi
{
    // The left panel's skeleton, built once in InitLeft. RefreshLeft only writes the values that changed and rebuilds
    // the order grid or the city list when their identity key moves, so the panel keeps its scroll position, its
    // canvas item RIDs (and so its scrape pattern) and its children across a click.
    private readonly Label _unitEmpty = Ui.Text("Nothing selected. Click a unit or city.", 14, Ui.Dim);
    private readonly VBoxContainer _unitSection = Ui.VBox(4);
    private readonly TextureRect _unitIcon = Ui.Icon(null, 48);
    private readonly Label _unitName = Ui.Text("", 19, Colors.White);
    private readonly Label _unitStats = Ui.Text("", 13, Ui.Dim);
    private readonly Label _unitMovesHealth = Ui.Text("", 14);
    private readonly ProgressBar _unitHealthBar = Ui.Bar(0, 100, null);
    private readonly Label _unitXp = Ui.Text("", 13, Ui.Dim);
    private readonly Label _unitPromotions = Ui.Text("", 13, Ui.Dim);
    private readonly GridContainer _ordersGrid = new() { Columns = 2 };
    private readonly VBoxContainer _cityList = Ui.VBox(3);
    private readonly List<Button> _orderButtons = new();
    private Color? _unitHealthTint;
    private string? _shownOrderKey;
    private string? _shownCityKey;

    /// <summary>
    /// Builds the left panel's one scroll container and its fixed widgets once: the empty-selection label, the unit
    /// row, the separator, the Cities heading and the city list. The labels are kept as fields so a refresh rewrites
    /// their text instead of their Controls.
    /// </summary>
    private void InitLeft()
    {
        var title = Ui.VBox(0);
        title.AddChild(_unitName);
        title.AddChild(_unitStats);
        var head = Ui.HBox(8);
        head.AddChild(_unitIcon);
        head.AddChild(title);
        _unitSection.AddChild(head);
        _unitSection.AddChild(_unitMovesHealth);
        _unitSection.AddChild(_unitHealthBar);
        _unitSection.AddChild(_unitXp);
        _unitSection.AddChild(_unitPromotions);
        _unitSection.AddChild(Ui.Text("Orders", 14, Ui.Accent));
        _ordersGrid.AddThemeConstantOverride("h_separation", 4);
        _ordersGrid.AddThemeConstantOverride("v_separation", 4);
        _unitSection.AddChild(_ordersGrid);
        _unitSection.AddChild(Ui.Text("Right-click a tile to move or attack. Tab: next unit.", 12, Ui.Dim));

        var box = Ui.VBox(8);
        box.AddChild(_unitEmpty);
        box.AddChild(_unitSection);
        box.AddChild(new HSeparator());
        box.AddChild(Ui.Heading("Cities"));
        box.AddChild(_cityList);
        _left.AddChild(Ui.Scroll(box));
    }

    /// <summary>
    /// Updates the left panel in place. The identity keys are worked out here, which only runs on a revision change,
    /// never on the 60 Hz frame path; <see cref="GameUi.Refresh"/> returns before this when the revision is unchanged.
    /// </summary>
    private void RefreshLeft()
    {
        var unit = _session.DescribeUnit(_session.Unit);
        _unitEmpty.Visible = unit == null;
        _unitSection.Visible = unit != null;
        if (unit != null) FillUnitRow(unit);
        RefreshOrders(unit);
        RefreshCityList();
    }

    /// <summary>Fills the unit row's already-built widgets; a stacked-unit list would call this once per unit.</summary>
    private void FillUnitRow(UnitPanel unit)
    {
        var texture = _icons.TextureOf(_session.Unit!.Type);
        if (_unitIcon.Texture != texture) _unitIcon.Texture = texture;
        Ui.Set(_unitName, unit.Name);
        Ui.Set(_unitStats, unit.Stats);
        Ui.Set(_unitMovesHealth, $"Moves {unit.Moves}   Health {unit.Health}%");
        if (_unitHealthBar.Value != unit.Health) _unitHealthBar.Value = unit.Health;
        var tint = unit.Health > 50 ? Ui.Good : Ui.Bad;
        if (_unitHealthTint != tint)
        {
            _unitHealthBar.AddThemeStyleboxOverride("fill", BrassSkin.BarFill(tint));
            _unitHealthTint = tint;
        }
        Ui.Set(_unitXp, $"{unit.Experience}   ·   {unit.Activity}");
        string promotions = unit.Promotions.Count > 0 ? "Promotions: " + string.Join(", ", unit.Promotions) : "";
        _unitPromotions.Visible = promotions.Length > 0;
        Ui.Set(_unitPromotions, promotions);
    }

    /// <summary>
    /// Rebuilds the order buttons only when the selected unit or its action set changed, and otherwise updates the
    /// existing buttons' disabled state and tooltip. A hidden unit section keeps its last set: a key that has not
    /// moved means those buttons are already right for the unit that is selected again.
    /// </summary>
    private void RefreshOrders(UnitPanel? unit)
    {
        if (unit == null) return;
        var key = OrderKey(unit);
        if (key != _shownOrderKey)
        {
            _shownOrderKey = key;
            Ui.Clear(_ordersGrid);
            _orderButtons.Clear();
            foreach (var action in unit.Actions)
            {
                var button = Ui.Button(action.Label, () => _session.RunAction(action.Id), action.Disabled ?? action.Tooltip, action.Command != null, 13);
                button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                _ordersGrid.AddChild(button);
                _orderButtons.Add(button);
            }
        }
        for (int i = 0; i < unit.Actions.Count && i < _orderButtons.Count; i++)
        {
            var action = unit.Actions[i];
            var button = _orderButtons[i];
            bool disabled = action.Command == null;
            if (button.Disabled != disabled) button.Disabled = disabled;
            string tooltip = action.Disabled ?? action.Tooltip;
            if (button.TooltipText != tooltip) button.TooltipText = tooltip;
        }
    }

    /// <summary>The unit id plus its actions' ids and labels: everything a change of would need different buttons.</summary>
    private static string OrderKey(UnitPanel unit)
    {
        var key = new StringBuilder().Append(unit.Id);
        foreach (var action in unit.Actions) key.Append('\u001f').Append(action.Id).Append('\u001e').Append(action.Label);
        return key.ToString();
    }

    private List<City> HumanCities() =>
        _session.Sim.State.Cities.Where(c => c.Owner == GameSession.Human).OrderBy(c => c.Id).ToList();

    /// <summary>Rebuilds the city buttons only when a row's id, name, population or religion badge changed.</summary>
    private void RefreshCityList()
    {
        var cities = HumanCities();
        var key = new StringBuilder();
        foreach (var city in cities)
            key.Append(city.Id).Append('\u001e').Append(city.Name).Append('\u001e').Append(city.Population)
                .Append('\u001e').Append(_session.ReligionBadge(city)).Append('\u001f');
        string identity = key.ToString();
        if (identity == _shownCityKey) return;
        _shownCityKey = identity;
        Ui.Clear(_cityList);
        foreach (var city in cities)
        {
            var id = city.Id;
            _cityList.AddChild(Ui.Button($"{city.Name}  ({city.Population})" + (_session.ReligionBadge(city) is { Length: > 0 } badge ? $"  [{badge}]" : ""), () => _session.SelectCity(id), "Open the city screen"));
        }
        if (cities.Count == 0) _cityList.AddChild(Ui.Text("None yet. Found one with a settler (B).", 13, Ui.Dim));
    }
}
