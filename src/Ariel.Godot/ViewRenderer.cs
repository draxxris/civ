using System.Text;
using Godot;

namespace Ariel.Client;

/// <summary>
/// Draws described elements (<see cref="ViewElement"/>) in the brass skin, for every mod screen and panel alike. It
/// rebuilds its widgets only when what they draw changed (<see cref="ViewWalk.Index"/>); otherwise it keeps them and
/// only takes the fresh actions, so a button pressed after a quiet world change acts with the current command. Each
/// control is wired to its slot in document order, the order <see cref="ViewWalk"/> lists the actions in.
/// </summary>
internal sealed class ViewRenderer(IconCache icons, Action<ScreenAction?> press)
{
    private readonly List<ScreenAction?> _actions = new();
    private readonly List<ScrollContainer> _scrolls = new();
    private readonly StringBuilder _key = new();
    private string? _shownKey;
    private int _slot;
    private const int LongLabel = 18;

    /// <summary>Draws <paramref name="elements"/> into <paramref name="box"/> when they changed since the last call;
    /// true when it rebuilt. Scroll positions of the columns survive a rebuild.</summary>
    public bool Show(Container box, IReadOnlyList<ViewElement> elements)
    {
        _key.Clear();
        _actions.Clear();
        ViewWalk.Index(elements, _key, _actions);
        string key = _key.ToString();
        if (key == _shownKey) return false;
        _shownKey = key;
        var scrolled = _scrolls.Select(s => IsInstanceValid(s) ? s.ScrollVertical : 0).ToArray();
        _scrolls.Clear();
        Ui.Clear(box);
        _slot = 0;
        foreach (var element in elements) box.AddChild(Build(element));
        for (int i = 0; i < Math.Min(scrolled.Length, _scrolls.Count); i++)
            _scrolls[i].SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scrolled[i]);
        return true;
    }

    /// <summary>Forgets what is drawn, so the next <see cref="Show"/> rebuilds and starts at the top, as a newly opened
    /// screen should.</summary>
    public void Forget()
    {
        _shownKey = null;
        _scrolls.Clear();
    }

    private static bool IsInstanceValid(GodotObject o) => GodotObject.IsInstanceValid(o);

    private Control Build(ViewElement element) => element switch
    {
        Heading h => Ui.Heading(h.Text),
        Paragraph t => TextOf(t),
        ListItem l => ListItemOf(l),
        StatLine s => StatOf(s),
        Meter m => MeterOf(m),
        ActionButton b => ButtonOf(b),
        Picker p => PickerOf(p),
        Row r => RowOf(r),
        Section s => SectionOf(s),
        Table t => TableOf(t),
        Columns c => ColumnsOf(c),
        _ => Ui.Text(element.Id, 12, Ui.Dim),
    };

    internal static Color ColourOf(Tone tone) => tone switch
    {
        Tone.Dim => Ui.Dim,
        Tone.Accent => Ui.Accent,
        Tone.Good => Ui.Good,
        Tone.Bad => Ui.Bad,
        Tone.Warn => Ui.Warn,
        _ => Colors.White,
    };

    private static int SizeOf(TextSize size) => size switch { TextSize.Small => 12, TextSize.Title => 15, _ => 13 };

    private Control TextOf(Paragraph t)
    {
        var label = Ui.Text(t.Text, SizeOf(t.Size), t.Tone == Tone.Normal && t.Size != TextSize.Title ? null : ColourOf(t.Tone));
        if (t.Tooltip is { } tip) { label.TooltipText = tip; label.MouseFilter = Control.MouseFilterEnum.Pass; }
        if (t.Icon is not { } icon) return label;
        var row = Ui.HBox(6);
        row.AddChild(Ui.Icon(icons.TextureOf(icon), t.Size == TextSize.Title ? 32 : 24));
        row.AddChild(label);
        return row;
    }

    private Control ListItemOf(ListItem l)
    {
        var line = Ui.HBox(6);
        if (l.Icon is { } icon) line.AddChild(Ui.Icon(icons.TextureOf(icon), 32));
        var info = Ui.VBox(0);
        info.AddChild(Ui.Text(l.Title, 15, l.Tone == Tone.Normal ? Colors.White : ColourOf(l.Tone)));
        if (l.Detail is { } detail) info.AddChild(Ui.Text(detail, 12, Ui.Dim));
        if (l.Tooltip is { } tip) { info.TooltipText = tip; info.MouseFilter = Control.MouseFilterEnum.Pass; }
        line.AddChild(info);
        return line;
    }

    private static Control StatOf(StatLine s)
    {
        var row = Ui.HBox(8);
        var name = Ui.Text(s.Label, 14, Ui.Dim, wrap: false);
        name.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        row.AddChild(name);
        var value = Ui.Text(s.Value, 14, s.Tone == Tone.Normal ? null : ColourOf(s.Tone));
        if (s.Tooltip is { } tip) { value.TooltipText = tip; value.MouseFilter = Control.MouseFilterEnum.Pass; }
        row.AddChild(value);
        return row;
    }

    private static Control MeterOf(Meter m)
    {
        var box = Ui.VBox(2);
        box.AddChild(Ui.Text(m.Label, 13));
        box.AddChild(Ui.Bar(m.Value, m.Maximum, ColourOf(m.Tone), $"{m.Value}/{m.Maximum}"));
        return box;
    }

    private Control ButtonOf(ActionButton b)
    {
        int slot = _slot++;
        var button = Ui.Button(b.Label, () => press(_actions[slot]), b.Disabled ?? b.Tooltip, b.Enabled, 13);
        // A long label (a name) may be cut short in a narrow column; a short one (a step, a verb) always shows whole.
        if (b.Label.Length > LongLabel)
        {
            button.ClipText = true;
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        }
        return button;
    }

    private Control PickerOf(Picker p)
    {
        int first = _slot;
        _slot += p.Options.Count;
        var picker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FitToLongestItem = false, ClipText = true };
        int offset = 0;
        if (p.Placeholder is { } placeholder && !p.Options.Any(o => o.Selected))
        {
            picker.AddItem(placeholder, -1);
            offset = 1;
        }
        for (int i = 0; i < p.Options.Count; i++) picker.AddItem(p.Options[i].Label, i);
        int selected = p.Options.ToList().FindIndex(o => o.Selected);
        picker.Select(selected >= 0 ? selected + offset : 0);
        picker.ItemSelected += index =>
        {
            int option = picker.GetItemId((int)index);
            if (option >= 0) press(_actions[first + option]);
        };
        return picker;
    }

    private Control RowOf(Row r)
    {
        var row = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 4);
        foreach (var item in r.Items)
        {
            var child = Build(item);
            // A label in a flow row keeps its own width rather than taking the whole line.
            if (child is Label label) { label.AutowrapMode = TextServer.AutowrapMode.Off; label.SizeFlagsHorizontal = Control.SizeFlags.Fill; }
            // A list entry takes the rest of its line, so the controls after it sit at the line's end and its wrapping
            // text has room rather than the near-zero width a wrapping label asks for.
            else if (item is ListItem) child.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(child);
        }
        return row;
    }

    private Control SectionOf(Section s)
    {
        var box = Ui.VBox(4);
        if (s.Title is { } title) box.AddChild(Ui.Heading(title));
        foreach (var item in s.Items) box.AddChild(Build(item));
        return box;
    }

    private Control TableOf(Table t)
    {
        var grid = new GridContainer { Columns = Math.Max(1, t.Columns.Count), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 3);
        foreach (var column in t.Columns) grid.AddChild(Ui.Text(column, 12, Ui.Dim, wrap: false));
        foreach (var row in t.Rows)
        {
            int slot = _slot++;
            for (int i = 0; i < grid.Columns; i++)
            {
                string cell = i < row.Cells.Count ? row.Cells[i] : "";
                if (i == 0 && row.Select is not null)
                {
                    var button = Ui.Button(cell, () => press(_actions[slot]), cell, !row.Selected, 13);
                    button.ClipText = true;
                    button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    grid.AddChild(WithIcon(button, i == 0 ? row.Icon : null));
                    continue;
                }
                var label = Ui.Text(cell, 13, row.Selected ? Ui.Accent : ColourOf(row.Tone), wrap: false);
                grid.AddChild(WithIcon(label, i == 0 ? row.Icon : null));
            }
        }
        return grid;
    }

    private Control WithIcon(Control control, string? icon)
    {
        if (icon is null) return control;
        var row = Ui.HBox(6);
        row.AddChild(Ui.Icon(icons.TextureOf(icon), 24));
        row.AddChild(control);
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return row;
    }

    private Control ColumnsOf(Columns c)
    {
        var split = Ui.HBox(10);
        split.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        split.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        foreach (var column in c.Items)
        {
            var box = Ui.VBox(6);
            foreach (var item in column.Items) box.AddChild(Build(item));
            var scroll = Ui.Scroll(box, 420);
            scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.SizeFlagsStretchRatio = Math.Max(1, column.Weight);
            _scrolls.Add(scroll);
            split.AddChild(scroll);
        }
        return split;
    }
}
