using Godot;

namespace Ariel.Client;

/// <summary>A chat log and an input line, shared by the lobby and the game. Redraws only when the log changes.</summary>
internal sealed partial class ChatView : VBoxContainer
{
    private readonly ChatLog _log;
    private readonly Action<string> _say;
    private readonly VBoxContainer _lines = Ui.VBox(1);
    private readonly ScrollContainer _scroll;
    private readonly LineEdit _input = new() { PlaceholderText = "Say something (Enter)" };
    private int _shown = -1;

    public ChatView(ChatLog log, Action<string> say, int minHeight = 120)
    {
        _log = log;
        _say = say;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        _scroll = Ui.Scroll(_lines, minHeight);
        AddChild(_scroll);
        AddChild(_input);
        _input.TextSubmitted += text =>
        {
            _say(text);
            _input.Clear();
        };
    }

    public override void _Process(double delta)
    {
        if (_log.Revision == _shown) return;
        _shown = _log.Revision;
        Ui.Clear(_lines);
        foreach (var line in _log.Lines.Skip(Math.Max(0, _log.Lines.Count - 60)))
            _lines.AddChild(line.Peer < 0 ? Ui.Text(line.Text, 13, Ui.Dim) : Ui.Text($"{line.Name}: {line.Text}", 13));
        Callable.From(() => _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue).CallDeferred();
    }
}

/// <summary>In-game multiplayer overlay: chat above the end-turn button, and a banner when the connection fails.</summary>
internal sealed partial class MultiplayerOverlay : CanvasLayer
{
    private readonly MultiplayerGame _game;
    private readonly Label _problem = Ui.Text("", 15, Ui.Bad);
    private string? _shownProblem;

    public MultiplayerOverlay(MultiplayerGame game) => _game = game;

    public override void _Ready()
    {
        var panel = Ui.Panel();
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        panel.OffsetLeft = -372; panel.OffsetRight = -12; panel.OffsetTop = -300; panel.OffsetBottom = -76;
        var column = Ui.VBox(4);
        _problem.Visible = false;
        column.AddChild(_problem);
        column.AddChild(new ChatView(_game.Chat, _game.Say));
        panel.AddChild(column);
        AddChild(panel);
    }

    public override void _Process(double delta)
    {
        var problem = _game.Session.ConnectionProblem;
        if (ReferenceEquals(problem, _shownProblem)) return;
        _shownProblem = problem;
        _problem.Text = problem ?? "";
        _problem.Visible = problem != null;
    }
}
