using Godot;

namespace Ariel.Client;

/// <summary>
/// Standalone widget gallery for the brass skin: every skinned control in its states on the reference backdrop.
/// Run: <c>godot-mono --path src/Ariel.Godot res://UiGallery.tscn</c>. It loads no mods.
/// Options after <c>--</c>: <c>--screenshot PATH</c> saves a capture and quits; <c>--popup</c> opens the dropdown first;
/// <c>--frames N</c> waits N frames before the capture (default 30); <c>--replica</c> shows the widgets of
/// docs/references/ui.png at the reference's own positions and sizes, skinned at 3x, in a window the reference's
/// size, so crops compare one to one.
/// </summary>
public partial class UiGallery : Control
{
    private static readonly string[] Orders = ["Rush Production", "Purchase", "Wealth", "Research", "Culture", "Espionage"];

    private readonly OptionButton _dropdown = new();
    private string? _shotPath;
    private bool _openPopup, _replica;
    private int _frames = 30, _frame;

    public override void _Ready()
    {
        ParseArgs(OS.GetCmdlineUserArgs());
        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (_replica)
        {
            Theme = Ui.MakeTheme(ReplicaScale);
            Ui.EnableScrollArrows(GetTree(), ReplicaScale);
            ListRim.Attach(GetTree());
        }
        else Ui.InstallTheme(GetWindow());
        GD.Print($"Painted the brass theme in {clock.ElapsedMilliseconds} ms.");
        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(new Panel { ThemeTypeVariation = BrassSkin.BackdropPanel, AnchorRight = 1, AnchorBottom = 1 });
        if (_replica)
        {
            BuildReplica();
            return;
        }
        var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1 };
        foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 28);
        var columns = Ui.HBox(36);
        columns.AddChild(LeftColumn());
        columns.AddChild(RightColumn());
        margin.AddChild(columns);
        AddChild(margin);
    }

    public override void _Process(double delta)
    {
        if (_shotPath == null) return;
        _frame++;
        if (_openPopup && _frame == 5) _dropdown.ShowPopup();
        if (_frame < _frames) return;
        var error = GetViewport().GetTexture().GetImage().SavePng(_shotPath);
        GD.Print($"screenshot {_shotPath}: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private void ParseArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--screenshot" when i + 1 < args.Length: _shotPath = System.IO.Path.GetFullPath(args[++i]); break;
                case "--frames" when i + 1 < args.Length: _frames = int.Parse(args[++i]); break;
                case "--popup": _openPopup = true; break;
                case "--replica": _replica = true; break;
            }
        }
    }

    private static VBoxContainer LeftColumn()
    {
        var column = Ui.VBox(10);
        column.CustomMinimumSize = new Vector2(600, 0);

        column.AddChild(Ui.Heading("Buttons"));
        var buttons = Ui.HBox(12);
        buttons.AddChild(Wide(Ui.Button("Abandon", () => { }, enabled: false, size: 16)));
        buttons.AddChild(Wide(Ui.Button("Production", () => { }, size: 16)));
        var toggle = Ui.Button("Toggled", () => { }, size: 16);
        toggle.ToggleMode = true;
        toggle.ButtonPressed = true;
        buttons.AddChild(Wide(toggle));
        column.AddChild(buttons);
        var small = Ui.HBox(8);
        foreach (var label in new[] { "Small", "Buttons", "At 13 px" }) small.AddChild(Ui.Button(label, () => { }, size: 13));
        column.AddChild(small);

        column.AddChild(Ui.Heading("Progress bars"));
        var main = Ui.Bar(60, 100, null, "60 / 100");
        main.CustomMinimumSize = new Vector2(0, 24);
        column.AddChild(main);
        var bars = Ui.HBox(10);
        bars.AddChild(Wide(Ui.Bar(14, 20, Ui.Good, "14/20")));
        bars.AddChild(Wide(Ui.Bar(4, 20, Ui.Bad, "4/20")));
        bars.AddChild(Wide(Ui.Bar(1, 20, Ui.Accent, "1/20")));
        column.AddChild(bars);

        column.AddChild(Ui.Heading("Tabs"));
        column.AddChild(Tabs());

        column.AddChild(Ui.Heading("Check boxes and radio buttons"));
        var checks = Ui.HBox(60);
        checks.AddChild(CheckColumn());
        checks.AddChild(RadioColumn());
        column.AddChild(checks);
        return column;
    }

    private static TabContainer Tabs()
    {
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(0, 120) };
        foreach (var name in new[] { "Overview", "Production", "Buildings", "Espionage" })
        {
            var page = Ui.Text($"{name} page. Tabs stand on the panel's brass rail.", 14, BrassSkin.Text);
            page.Name = name;
            tabs.AddChild(page);
        }
        tabs.SetTabDisabled(3, true);
        return tabs;
    }

    private static VBoxContainer CheckColumn()
    {
        var box = Ui.VBox(6);
        box.SizeFlagsHorizontal = SizeFlags.Fill;
        box.AddChild(new CheckBox { Text = "Show unavailable", ButtonPressed = true });
        box.AddChild(new CheckBox { Text = "Grid" });
        box.AddChild(new CheckBox { Text = "Disabled, on", ButtonPressed = true, Disabled = true });
        box.AddChild(new CheckBox { Text = "Disabled, off", Disabled = true });
        return box;
    }

    private static VBoxContainer RadioColumn()
    {
        var box = Ui.VBox(6);
        box.SizeFlagsHorizontal = SizeFlags.Fill;
        var group = new ButtonGroup();
        foreach (var (text, on) in new[] { ("Peace", true), ("War", false), ("Vassal", false) })
            box.AddChild(new CheckBox { Text = text, ButtonGroup = group, ButtonPressed = on });
        box.AddChild(new CheckBox { Text = "Disabled", ButtonGroup = new ButtonGroup(), Disabled = true });
        return box;
    }

    private VBoxContainer RightColumn()
    {
        var column = Ui.VBox(10);
        column.CustomMinimumSize = new Vector2(420, 0);

        column.AddChild(Ui.Heading("Dropdown"));
        foreach (var order in Orders) _dropdown.AddItem(order);
        _dropdown.Selected = 0;
        var dropdowns = Ui.HBox(10);
        dropdowns.AddChild(Wide(_dropdown));
        var disabled = new OptionButton { Disabled = true };
        disabled.AddItem("Disabled");
        dropdowns.AddChild(Wide(disabled));
        column.AddChild(dropdowns);

        column.AddChild(Ui.Heading("Scrollable list"));
        var scrolling = List(Orders.Concat(Orders.Select(o => o + " II")), autoHeight: false);
        scrolling.CustomMinimumSize = new Vector2(0, 170);
        column.AddChild(scrolling);

        column.AddChild(Ui.Heading("List"));
        column.AddChild(List(Orders.Take(3), autoHeight: true));

        column.AddChild(Ui.Heading("Scroll bars, text field and text area"));
        var text = Ui.VBox(2);
        for (var i = 1; i <= 12; i++) text.AddChild(Ui.Text($"Scrolled line {i}", 13, BrassSkin.Text));
        var scroll = Ui.Scroll(text, 80);
        scroll.SizeFlagsVertical = SizeFlags.Fill;
        column.AddChild(scroll);
        column.AddChild(new HScrollBar { MaxValue = 100, Page = 30, Value = 20 });
        var fields = Ui.HBox(10);
        fields.AddChild(Wide(new LineEdit { PlaceholderText = "Search leaders" }));
        fields.AddChild(new SpinBox { MinValue = 0, MaxValue = 15, Value = 3 });
        column.AddChild(fields);
        column.AddChild(new TextEdit
        {
            Text = "Notes on the war with the north.\nThe softer well keeps longer text easy to read.",
            CustomMinimumSize = new Vector2(0, 70),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        });
        return column;
    }

    // ---- Replica of docs/references/ui.png ----------------------------------------------------------

    /// <summary>The reference is drawn at about 3.3 reference pixels per game pixel; 3 is the nearest whole scale.</summary>
    private const int ReplicaScale = 3;

    /// <summary>Each widget at its rect in the reference (full-size pixels: left, top, right, bottom).</summary>
    private void BuildReplica()
    {
        Place(Ui.Text("Progress Bar", 30, BrassSkin.Text, wrap: false), 63, 212, 600, 252);
        Place(Ui.Text("Tab Bar", 30, BrassSkin.Text, wrap: false), 63, 372, 600, 412);
        Place(Ui.Text("Checkbox", 30, BrassSkin.Text, wrap: false), 63, 545, 600, 585);
        Place(Ui.Text("Radio Button", 30, BrassSkin.Text, wrap: false), 604, 545, 1000, 585);
        Place(Ui.Text("Dropdown", 30, BrassSkin.Text, wrap: false), 1220, 212, 1600, 252);

        var abandon = new Button { Text = "Abandon", Disabled = true };
        abandon.AddThemeColorOverride("font_disabled_color", BrassSkin.Text);
        Place(abandon, 63, 69, 597, 175);
        Place(new Button { Text = "Track Quest" }, 630, 69, 1151, 175);

        var bar = new ProgressBar { MaxValue = 100, Value = 60, ShowPercentage = false };
        var value = Ui.Text("60 / 100", 30, BrassSkin.Text, wrap: false);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        value.VerticalAlignment = VerticalAlignment.Center;
        value.SetAnchorsPreset(LayoutPreset.FullRect);
        value.OffsetRight = -33;
        bar.AddChild(value);
        Place(bar, 63, 264, 1149, 341);

        var tabs = new TabContainer();
        foreach (var name in new[] { "Overview", "Production", "Buildings" }) tabs.AddChild(new Control { Name = name });
        tabs.AddThemeFontSizeOverride("font_size", 30);
        Place(tabs, 53, 424, 1161, 540);

        Place(new CheckBox { Text = "Show City Radius", ButtonPressed = true }, 72, 600, 560, 668);
        Place(new CheckBox { Text = "Auto-Assign Citizens" }, 72, 669, 560, 737);
        var group = new ButtonGroup();
        Place(new CheckBox { Text = "Food Focus", ButtonGroup = group, ButtonPressed = true }, 640, 600, 1100, 668);
        Place(new CheckBox { Text = "Production Focus", ButtonGroup = group }, 640, 669, 1100, 737);

        foreach (var order in new[] { "Rush Production", "Purchase", "Forced Labor", "Wealth", "Research" }) _dropdown.AddItem(order);
        _dropdown.Selected = 0;
        _dropdown.AddThemeFontSizeOverride("font_size", 30);
        Place(_dropdown, 1220, 267, 1616, 348);
        var list = List(new[] { "Rush Production", "Purchase", "Forced Labor", "Wealth", "Research", "Culture", "Espionage", "Ceremony" }, autoHeight: false);
        list.AddThemeFontSizeOverride("font_size", 30);
        Place(list, 1220, 368, 1619, 732);

        // Below the reference's area: widgets it does not show, at the same scale, for a close look.
        var notes = new TextEdit
        {
            Text = "Notes on the war with the north.\nThe softer well keeps longer text easy to read.",
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        notes.AddThemeFontSizeOverride("font_size", 30);
        Place(notes, 63, 790, 1149, 960);
        var search = new LineEdit { PlaceholderText = "Search leaders" };
        search.AddThemeFontSizeOverride("font_size", 30);
        Place(search, 1220, 790, 1619, 870);
        Place(new HScrollBar { MaxValue = 100, Page = 30, Value = 20 }, 1220, 900, 1619, 960);
    }

    private void Place(Control control, int left, int top, int right, int bottom)
    {
        if (control is CheckBox) control.AddThemeFontSizeOverride("font_size", 28);
        control.Position = new Vector2(left, top);
        control.Size = new Vector2(right - left, bottom - top);
        control.CustomMinimumSize = control.Size;
        AddChild(control);
    }

    private static ItemList List(IEnumerable<string> items, bool autoHeight)
    {
        var list = new ItemList { AutoHeight = autoHeight };
        foreach (var item in items) list.AddItem(item);
        list.Select(0);
        return list;
    }

    private static T Wide<T>(T control) where T : Control
    {
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return control;
    }
}
