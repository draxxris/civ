using System.Text;

namespace Ariel.Client;

/// <summary>
/// Walks described elements in document order: depth first, children in list order. A renderer and its callers agree
/// on this order, so a control can be wired to "the n-th action" and keep working when a later describe draws the same
/// thing with fresh commands (the same text, a different unit to act with). Every <see cref="ActionButton"/>, every
/// <see cref="PickerOption"/> and every <see cref="TableRow"/> takes exactly one slot, null when it has no action.
/// </summary>
public static class ViewWalk
{
    /// <summary>Appends a key of everything the elements draw to <paramref name="key"/> and their actions, in document
    /// order, to <paramref name="actions"/>. Equal keys draw the same widgets.</summary>
    public static void Index(IReadOnlyList<ViewElement> elements, StringBuilder key, List<ScreenAction?> actions)
    {
        foreach (var element in elements) Index(element, key, actions);
    }

    private static void Index(ViewElement element, StringBuilder key, List<ScreenAction?> actions)
    {
        key.Append('\u001e').Append(element.GetType().Name).Append('\u001f').Append(element.Id);
        switch (element)
        {
            case Heading h: Field(key, h.Text); break;
            case Paragraph t: Field(key, t.Text).Append((int)t.Tone).Append((int)t.Size); Field(key, t.Icon); Field(key, t.Tooltip); break;
            case ListItem l: Field(key, l.Title); Field(key, l.Detail); Field(key, l.Icon).Append((int)l.Tone); Field(key, l.Tooltip); break;
            case StatLine s: Field(key, s.Label); Field(key, s.Value).Append((int)s.Tone); Field(key, s.Tooltip); break;
            case Meter m: Field(key, m.Label).Append(m.Value).Append('/').Append(m.Maximum).Append((int)m.Tone); break;
            case ActionButton b:
                Field(key, b.Label); Field(key, b.Disabled); Field(key, b.Tooltip).Append(b.Selected).Append(b.Action is null);
                actions.Add(b.Action);
                break;
            case Picker p:
                Field(key, p.Placeholder);
                foreach (var option in p.Options) { Field(key, option.Id); Field(key, option.Label).Append(option.Selected); actions.Add(option.Action); }
                break;
            case Row r: Index(r.Items, key, actions); key.Append('\u001d'); break;
            case Section s: Field(key, s.Title); Index(s.Items, key, actions); key.Append('\u001d'); break;
            case Table t:
                foreach (var column in t.Columns) Field(key, column);
                foreach (var row in t.Rows)
                {
                    Field(key, row.Id).Append(row.Selected).Append((int)row.Tone).Append(row.Select is null);
                    Field(key, row.Icon);
                    foreach (var cell in row.Cells) Field(key, cell);
                    actions.Add(row.Select);
                }
                break;
            case Columns c:
                foreach (var column in c.Items) { key.Append('\u001c').Append(column.Weight); Index(column.Items, key, actions); }
                key.Append('\u001d');
                break;
        }
    }

    private static StringBuilder Field(StringBuilder key, string? value) => key.Append('\u001f').Append(value ?? "\u0000");

    /// <summary>Every element under the roots, the roots included, depth first.</summary>
    public static IEnumerable<ViewElement> All(IEnumerable<ViewElement> roots)
    {
        foreach (var element in roots)
        {
            yield return element;
            var children = element switch
            {
                Row r => r.Items,
                Section s => s.Items,
                Columns c => c.Items.SelectMany(column => column.Items).ToList(),
                _ => (IReadOnlyList<ViewElement>)[],
            };
            foreach (var child in All(children)) yield return child;
        }
    }

    /// <summary>Every element of a screen, header, body and footer, depth first.</summary>
    public static IEnumerable<ViewElement> All(ScreenView view) => All(view.Elements);

    /// <summary>The first element of type <typeparamref name="T"/> with this id anywhere on the screen, or null.</summary>
    public static T? Find<T>(ScreenView view, string id) where T : ViewElement => Find<T>(view.Elements, id);

    /// <summary>What the control with this id anywhere on the screen does, or null.</summary>
    public static ScreenAction? ActionOf(ScreenView view, string id) => ActionOf(view.Elements, id);

    /// <summary>Every piece of text the screen shows, header and footer included.</summary>
    public static string Text(ScreenView view) => Text(view.Elements);

    /// <summary>The first element of type <typeparamref name="T"/> with this id under the roots, or null.</summary>
    public static T? Find<T>(IEnumerable<ViewElement> roots, string id) where T : ViewElement =>
        All(roots).OfType<T>().FirstOrDefault(e => e.Id == id);

    /// <summary>What the control with this id does: a button's action, a picker option's or a table row's selection.
    /// Null when no control has the id, or it has no action.</summary>
    public static ScreenAction? ActionOf(IEnumerable<ViewElement> roots, string id)
    {
        foreach (var element in All(roots))
        {
            switch (element)
            {
                case ActionButton b when b.Id == id: return b.Action;
                case Picker p when p.Options.FirstOrDefault(o => o.Id == id) is { } option: return option.Action;
                case Table t when t.Rows.FirstOrDefault(r => r.Id == id) is { } row: return row.Select;
            }
        }
        return null;
    }

    /// <summary>Every piece of text the elements show, one per line, for searching and for tests.</summary>
    public static string Text(IEnumerable<ViewElement> roots)
    {
        var text = new StringBuilder();
        foreach (var element in All(roots))
        {
            switch (element)
            {
                case Heading h: text.AppendLine(h.Text); break;
                case Paragraph t: text.AppendLine(t.Text); break;
                case ListItem l: text.AppendLine(l.Title); if (l.Detail is { } detail) text.AppendLine(detail); break;
                case StatLine s: text.Append(s.Label).Append(": ").AppendLine(s.Value); break;
                case Meter m: text.Append(m.Label).Append(' ').Append(m.Value).Append('/').Append(m.Maximum).AppendLine(); break;
                case ActionButton b: text.AppendLine(b.Label); break;
                case Section { Title: { } title }: text.AppendLine(title); break;
                case Picker p: foreach (var o in p.Options) text.AppendLine(o.Label); break;
                case Table t:
                    text.AppendLine(string.Join(" | ", t.Columns));
                    foreach (var row in t.Rows) text.AppendLine(string.Join(" | ", row.Cells));
                    break;
            }
        }
        return text.ToString();
    }
}
