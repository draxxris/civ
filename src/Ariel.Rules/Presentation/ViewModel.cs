using System.Collections.Immutable;
using Ariel.Game;

namespace Ariel.Presentation;

/// <summary>How a piece of text reads. The renderer maps each tone to its own colours, so a mod never names one.</summary>
public enum Tone { Normal, Dim, Accent, Good, Bad, Warn }

/// <summary>Relative text size: small notes, body text, and the title line of an entry.</summary>
public enum TextSize { Small, Body, Title }

/// <summary>A screen's own choices that are not game state, such as the selected rival, tab or target. The host keeps
/// one per open screen, hands it to every <see cref="IGameScreen.Describe"/> and replaces it when the player presses a
/// <see cref="ChangeState"/> control; it never reaches the simulation, a save or another player. Values are strings the
/// screen encodes itself. Two states with the same entries are equal.</summary>
public sealed class ScreenState : IEquatable<ScreenState>
{
    public static readonly ScreenState Empty = new(ImmutableSortedDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal));
    private readonly ImmutableSortedDictionary<string, string> _values;

    private ScreenState(ImmutableSortedDictionary<string, string> values) => _values = values;

    public IReadOnlyDictionary<string, string> Values => _values;

    public string? Get(string key) => _values.GetValueOrDefault(key);

    public int? Int(string key) => _values.TryGetValue(key, out var value) && int.TryParse(value, out int n) ? n : null;

    /// <summary>A copy with <paramref name="key"/> set, or removed when <paramref name="value"/> is null.</summary>
    public ScreenState With(string key, string? value) => new(value is null ? _values.Remove(key) : _values.SetItem(key, value));

    public ScreenState With(string key, int? value) => With(key, value?.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public ScreenState Without(params string[] keys) => new(_values.RemoveRange(keys));

    public bool Equals(ScreenState? other) => other is not null && _values.Count == other._values.Count
        && _values.All(p => other._values.TryGetValue(p.Key, out var v) && v == p.Value);

    public override bool Equals(object? obj) => Equals(obj as ScreenState);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (key, value) in _values) { hash.Add(key); hash.Add(value); }
        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(";", _values.Select(p => p.Key + "=" + p.Value));
}

/// <summary>What pressing a control does. The renderer never interprets one: it hands it back to the host.</summary>
public abstract record ScreenAction;

/// <summary>Submit a command for the local player, through the same validation and channel as any other order.</summary>
public sealed record SubmitCommand(GameCommand Command) : ScreenAction;

/// <summary>Replace the screen's local state, for example to select a row, a tab or a target. The host describes the
/// screen again with <paramref name="Next"/>.</summary>
public sealed record ChangeState(ScreenState Next) : ScreenAction;

/// <summary>One piece of a described screen or panel. <see cref="Id"/> is stable within its parent for the same
/// thing on every describe, so the renderer can keep its widgets and a test can find it.</summary>
public abstract record ViewElement(string Id);

/// <summary>A heading line.</summary>
public sealed record Heading(string Id, string Text) : ViewElement(Id);

/// <summary>A line or paragraph of text, optionally led by the icon of a content id.</summary>
public sealed record Paragraph(string Id, string Text, Tone Tone = Tone.Normal, TextSize Size = TextSize.Body,
    string? Icon = null, string? Tooltip = null) : ViewElement(Id);

/// <summary>One entry of a list: an icon slot (the icon of a content id), and beside it a title with a dim detail
/// line under it, as a catalogue shows a thing and its state.</summary>
public sealed record ListItem(string Id, string Title, string? Detail = null, string? Icon = null, Tone Tone = Tone.Normal,
    string? Tooltip = null) : ViewElement(Id);

/// <summary>A labelled value, as in the city screen's figures.</summary>
public sealed record StatLine(string Id, string Label, string Value, Tone Tone = Tone.Normal, string? Tooltip = null) : ViewElement(Id);

/// <summary>Progress toward a goal, such as points toward a threshold.</summary>
public sealed record Meter(string Id, string Label, long Value, long Maximum, Tone Tone = Tone.Good) : ViewElement(Id);

/// <summary>A control that does <see cref="Action"/>. It is disabled when <see cref="Action"/> is null or
/// <see cref="Disabled"/> gives a reason, which the renderer shows as its tooltip. <see cref="Selected"/> marks the
/// current tab or choice of a group.</summary>
public sealed record ActionButton(string Id, string Label, ScreenAction? Action, string? Disabled = null,
    string? Tooltip = null, bool Selected = false) : ViewElement(Id)
{
    public bool Enabled => Action is not null && Disabled is null && !Selected;
}

/// <summary>A drop-down choice. Picking an option does its action; <see cref="Placeholder"/> is shown when no option
/// is selected.</summary>
public sealed record Picker(string Id, IReadOnlyList<PickerOption> Options, string? Placeholder = null) : ViewElement(Id);

public sealed record PickerOption(string Id, string Label, ScreenAction Action, bool Selected = false);

/// <summary>Elements side by side, wrapping when the row is full.</summary>
public sealed record Row(string Id, IReadOnlyList<ViewElement> Items) : ViewElement(Id);

/// <summary>Elements one under another, under an optional title.</summary>
public sealed record Section(string Id, string? Title, IReadOnlyList<ViewElement> Items) : ViewElement(Id);

/// <summary>A table. A row with a <see cref="TableRow.Select"/> action can be clicked, and one marked
/// <see cref="TableRow.Selected"/> is highlighted.</summary>
public sealed record Table(string Id, IReadOnlyList<string> Columns, IReadOnlyList<TableRow> Rows) : ViewElement(Id);

public sealed record TableRow(string Id, IReadOnlyList<string> Cells, ScreenAction? Select = null, bool Selected = false,
    Tone Tone = Tone.Normal, string? Icon = null);

/// <summary>Columns side by side, each scrolling on its own. <see cref="Column.Weight"/> shares out the width.</summary>
public sealed record Columns(string Id, IReadOnlyList<Column> Items) : ViewElement(Id);

public sealed record Column(int Weight, IReadOnlyList<ViewElement> Items);

/// <summary>A whole described screen: its title and body, and how wide it wants to be in skin units.</summary>
public sealed record ScreenView(string Title, IReadOnlyList<ViewElement> Body, int Width = 720);
