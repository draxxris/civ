using Ariel.Content;
using Ariel.Game;
using Godot;

namespace Ariel.Client;

/// <summary>Scrollable prerequisite graph. Browsing never issues a research command.</summary>
internal sealed partial class TechTreeBrowser : VBoxContainer
{
    private readonly TechTreeView _tree;
    private readonly int _credits;
    private readonly Func<string, bool, bool> _select;
    private readonly VBoxContainer _details = Ui.VBox(3);
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private readonly ScrollContainer _scroll = new() { CustomMinimumSize = new Vector2(1040, 330) };
    private readonly TreeCanvas _canvas = new();
    private readonly Dictionary<string, Vector2> _positions = new(StringComparer.Ordinal);

    public TechTreeBrowser(TechTreeView tree, int credits, Func<string, bool, bool> select)
    {
        _tree = tree; _credits = credits; _select = select;
        var search = new LineEdit { PlaceholderText = "Find a technology" };
        search.TextChanged += Find;
        AddChild(search);
        AddChild(Ui.Text("Gold: current · Green: selectable · Blue: known · Gray: locked. Lines: required, alternative (gold), forbidden (red).", 12, Ui.Dim));
        BuildGraph();
        _scroll.AddChild(_canvas);
        AddChild(_scroll);
        AddChild(Ui.Scroll(_details, 120));
        if (tree.Nodes.Count == 0) _details.AddChild(Ui.Text("The technology tree is empty."));
        else ShowDetails(tree.Nodes.FirstOrDefault(n => n.Current) ?? tree.Nodes.FirstOrDefault(n => n.Selectable) ?? tree.Nodes[0]);
    }

    private void BuildGraph()
    {
        var columns = _tree.Nodes.Select(n => n.Column).Distinct().Order().ToArray();
        var used = new HashSet<(int, int)>();
        foreach (var node in _tree.Nodes)
        {
            int col = Array.IndexOf(columns, node.Column);
            int row = Math.Max(0, node.Row);
            while (!used.Add((col, row))) row++;
            var position = new Vector2(30 + col * 250, 25 + row * 90);
            _positions.Add(node.Id, position);
            var button = Ui.Button(node.Name + "\n" + Status(node), () => ShowDetails(node), node.Name);
            button.Position = position;
            button.Size = new Vector2(210, 64);
            button.Modulate = NodeColor(node);
            _buttons.Add(node.Id, button);
            _canvas.AddChild(button);
        }
        _canvas.CustomMinimumSize = new Vector2(
            _positions.Values.Select(p => p.X + 260).DefaultIfEmpty(1040).Max(),
            _positions.Values.Select(p => p.Y + 100).DefaultIfEmpty(330).Max());
        foreach (var node in _tree.Nodes)
            AddEdges(node.Id, node.Requirements, Ui.Dim);
    }

    private void AddEdges(string target, Requirement? requirement, Color color)
    {
        switch (requirement)
        {
            case Requirement.Leaf { Kind: "tech" } leaf:
                if (_positions.TryGetValue(leaf.Id, out var from))
                    _canvas.Edges.Add((from + new Vector2(210, 32), _positions[target] + new Vector2(0, 32), color));
                break;
            case Requirement.All all:
                foreach (var item in all.Items) AddEdges(target, item, color);
                break;
            case Requirement.Any any:
                foreach (var item in any.Items) AddEdges(target, item, Ui.Accent);
                break;
            case Requirement.Not not: AddEdges(target, not.Item, Ui.Bad); break;
        }
    }

    private void Find(string query)
    {
        var match = _tree.Nodes.FirstOrDefault(n => n.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (match is null) return;
        ShowDetails(match);
        var position = _positions[match.Id];
        _scroll.ScrollHorizontal = (int)Math.Max(0, position.X - 250);
        _scroll.ScrollVertical = (int)Math.Max(0, position.Y - 90);
    }

    private void ShowDetails(TechTreeNode node)
    {
        Ui.Clear(_details);
        foreach (var (id, button) in _buttons) button.ButtonPressed = id == node.Id;
        _details.AddChild(Ui.Heading($"{node.Name} — {node.Era} — {Status(node)}"));
        _details.AddChild(Ui.Text($"Research: {node.Progress}/{node.Cost}" + (node.Repeatable ? " · Repeatable" : ""), 13));
        _details.AddChild(Ui.Text("Prerequisites: " + RequirementsText(node.Requirements), 13));
        if (node.Failures.Count > 0) _details.AddChild(Ui.Text(string.Join(". ", node.Failures), 12, Ui.Dim));
        var actions = Ui.HBox();
        actions.AddChild(Ui.Button("Research", () => _select(node.Id, false), enabled: node.Selectable));
        if (_credits > 0) actions.AddChild(Ui.Button("Use free technology", () => _select(node.Id, true), enabled: node.Selectable));
        _details.AddChild(actions);
        _details.AddChild(Ui.Text(GameUi.StripMarkup(node.Help), 12));
    }

    private string RequirementsText(Requirement? requirement) => requirement switch
    {
        null => "None",
        Requirement.Leaf leaf => _tree.Nodes.FirstOrDefault(n => n.Id == leaf.Id)?.Name ?? leaf.Id,
        Requirement.All all => Group("all of", all.Items),
        Requirement.Any any => Group("any of", any.Items),
        Requirement.Not not => "not (" + RequirementsText(not.Item) + ")",
        _ => "Unknown",
    };
    private string Group(string label, IReadOnlyList<Requirement> items) => label + " (" + string.Join(", ", items.Select(RequirementsText)) + ")";
    private static string Status(TechTreeNode node) => node.Current ? "Current" : node.Selectable ? "Selectable"
        : node.Known ? "Known" : node.Researchable ? "Not offered" : "Locked";
    private static Color NodeColor(TechTreeNode node) => node.Current ? Ui.Accent : node.Selectable ? Ui.Good
        : node.Known ? new Color(0.6f, 0.8f, 1f) : Ui.Dim;
}

internal sealed partial class TreeCanvas : Control
{
    internal List<(Vector2 From, Vector2 To, Color Color)> Edges { get; } = [];
    public override void _Draw()
    {
        foreach (var (from, to, color) in Edges)
        {
            var bend = new Vector2((from.X + to.X) / 2, from.Y);
            var end = new Vector2(bend.X, to.Y);
            DrawLine(from, bend, color, 1.5f, true);
            DrawLine(bend, end, color, 1.5f, true);
            DrawLine(end, to, color, 1.5f, true);
            DrawLine(to - new Vector2(6, 4), to, color, 1.5f, true);
            DrawLine(to - new Vector2(6, -4), to, color, 1.5f, true);
        }
    }
}
