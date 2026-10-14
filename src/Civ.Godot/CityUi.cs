using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>City screen: ledgers on the left, production and citizens on the right, the city's tiles on the map between.</summary>
internal sealed partial class CityUi : Control
{


    private const float LeftWidth = 370f, RightWidth = 440f, TopBarHeight = 44f;
    private const int MaxBlockedShown = 40;

    private readonly GameSession _session;
    private readonly IconCache _icons;
    private readonly PanelContainer _left = Ui.Panel();
    private readonly PanelContainer _right = Ui.Panel();
    private int _tab;
    private bool _showBlocked;

    /// <summary>Changes when a local choice (tab, filter) changes, so the owner knows to rebuild.</summary>
    public int State => _tab * 2 + (_showBlocked ? 1 : 0);

    public CityUi(GameSession session, IconCache icons)
    {
        _session = session; _icons = icons;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _left.SetAnchorsPreset(LayoutPreset.LeftWide);
        _left.OffsetTop = TopBarHeight + 4; _left.OffsetRight = LeftWidth; _left.OffsetBottom = -4;
        _right.SetAnchorsPreset(LayoutPreset.RightWide);
        _right.OffsetTop = TopBarHeight + 4; _right.OffsetLeft = -RightWidth; _right.OffsetBottom = -4;
        AddChild(_left);
        AddChild(_right);
    }

    public void Build(City city)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var screen = _session.DescribeCity(city);
        if (screen == null) return;
        long describe = clock.ElapsedMilliseconds;
        BuildLeft(screen);
        BuildRight(city, screen);
        if (MapRenderer.LogTimings) GD.Print($"city screen: describe {describe} ms, controls {clock.ElapsedMilliseconds - describe} ms");
    }

    // --- left: numbers and ledgers ----------------------------------------------------------------

    private void BuildLeft(CityScreen c)
    {
        Ui.Clear(_left);
        var box = Ui.VBox(6);
        var head = Ui.HBox();
        head.AddChild(Ui.Text($"{c.Name}  (size {c.Population})", 21, Colors.White, wrap: false));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        head.AddChild(Ui.Button("Close", _session.ClearSelection, "Back to the map (Esc)"));
        box.AddChild(head);
        if (c.Disorder) box.AddChild(Ui.Text("CIVIL DISORDER: nothing is produced.", 15, Ui.Bad));
        AddReligions(box, c);
        box.AddChild(FoodBox(c));
        box.AddChild(Stats(c));
        box.AddChild(Wellbeing(c));
        AddLedger(box, "Food", c.FoodLedger);
        AddLedger(box, "Production", c.ProductionLedger);
        AddLedger(box, "Commerce", c.CommerceLedger);
        AddLedger(box, "Happiness", c.HappinessLedger);
        AddLedger(box, "Health", c.HealthLedger);
        _left.AddChild(Ui.Scroll(box));
    }

    private static Control FoodBox(CityScreen c)
    {
        var box = Ui.VBox(2);
        string grow = c.FoodSurplus > 0 ? (c.TurnsToGrow is int t ? $"grows in {t} turns" : "") : c.FoodSurplus < 0 ? "starving" : "no growth";
        box.AddChild(Ui.Text($"Food {c.FoodStored}/{c.FoodThreshold}   ({Labels.Signed(c.FoodSurplus)} per turn, {grow})", 14));
        box.AddChild(Ui.Bar(c.FoodStored, c.FoodThreshold, c.FoodSurplus < 0 ? Ui.Bad : Ui.Good, $"{c.FoodStored}/{c.FoodThreshold}"));
        return box;
    }

    private static Control Stats(CityScreen c)
    {
        var grid = new GridContainer { Columns = 2 };
        void Row(string name, string value) { grid.AddChild(Ui.Text(name, 14, Ui.Dim)); grid.AddChild(Ui.Text(value, 14)); }
        Row("Production", $"{c.Production} per turn");
        Row("Commerce", $"{c.CommercePerTurn.Gold} gold, {c.CommercePerTurn.Research} research");
        Row("Culture", $"{c.CommercePerTurn.Culture} per turn" + (c.CultureLevel is { } l ? $"  ({l})" : ""));
        Row("Espionage", $"{c.CommercePerTurn.Espionage} per turn");
        Row("Maintenance", $"{c.Maintenance} gold");
        Row("Great people", c.GreatPeopleThreshold > 0 ? $"{c.GreatPeopleProgress}/{c.GreatPeopleThreshold}  ·  +{c.GreatPeopleRate}/turn" : "—");
        return grid;
    }

    private static Control Wellbeing(CityScreen c)
    {
        var box = Ui.VBox(2);
        box.AddChild(Ui.Text($"Happy {c.Happy}  ·  Unhappy {c.Unhappy}" + (c.Angry > 0 ? $"  ·  Angry {c.Angry}" : ""), 14, c.Unhappy > c.Happy ? Ui.Bad : Ui.Good));
        box.AddChild(Ui.Text($"Healthy {c.Healthy}  ·  Unhealthy {c.Unhealthy}", 14, c.Unhealthy > c.Healthy ? Ui.Bad : Ui.Good));
        return box;
    }

    private static void AddLedger(VBoxContainer box, string title, IReadOnlyList<LedgerRow> rows)
    {
        if (rows.Count == 0) return;
        box.AddChild(Ui.Text(title, 15, Ui.Accent));
        foreach (var row in rows) box.AddChild(Ui.Text(row.Text, 13, row.Amount < 0 ? Ui.Bad : Ui.Dim));
    }

    private void AddReligions(VBoxContainer box, CityScreen c)
    {
        if (c.Religions.Count == 0) return;
        var row = Ui.HBox(6);
        foreach (var r in c.Religions)
        {
            row.AddChild(Ui.Icon(_icons.TextureOf(r.Id), 24));
            row.AddChild(Ui.Text(r.Name + (r.Holy ? " ★ holy city" : "") + (r.State ? " (state)" : ""), 13, r.Holy ? Ui.Accent : Ui.Dim, wrap: false));
        }
        box.AddChild(row);
    }
}
