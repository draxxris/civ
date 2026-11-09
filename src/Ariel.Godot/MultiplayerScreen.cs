using Ariel.Game;
using Ariel.Net;
using Godot;

namespace Ariel.Client;

/// <summary>
/// Multiplayer over LAN or direct IP: a connect page (name, host or join) and the lobby that follows it (seats, the
/// host's setup, chat, start). The lobby itself is engine-free (<see cref="Lobby"/>); this screen only draws it and
/// forwards clicks, redrawing when the lobby's revision moves.
/// </summary>
internal sealed partial class MultiplayerScreen : Control
{
    public const int DefaultPort = 47800;

    private readonly GameCatalog _catalog;
    private readonly LineEdit _name = new() { Text = System.Environment.UserName, CustomMinimumSize = new Vector2(240, 0) };
    private readonly LineEdit _address = new() { Text = "127.0.0.1", CustomMinimumSize = new Vector2(240, 0) };
    private readonly SpinBox _port = new() { MinValue = 1024, MaxValue = 65535, Value = DefaultPort, Step = 1 };
    private readonly Label _status = Ui.Text("", 14, Ui.Bad);
    private readonly VBoxContainer _page = Ui.VBox(10);
    private Lobby? _lobby;
    private Task<TcpTransport>? _joining;
    private int _shown = -1;
    private VBoxContainer? _seats, _setup;
    private Label? _lobbyMessage;
    private Button? _startButton;
    private string? _loadedSave;

    /// <summary>The lobby has a game to start; the receiver takes the lobby over.</summary>
    public event Action<Lobby>? Starting;
    public event Action? Back;

    public MultiplayerScreen(GameCatalog catalog)
    {
        _catalog = catalog;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new ColorRect { Color = new Color(0.05f, 0.07f, 0.1f) };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 32);
        margin.AddChild(_page);
        AddChild(margin);
        ShowConnect();
    }

    public override void _Process(double delta)
    {
        if (_joining is { IsCompleted: true } joining)
        {
            _joining = null;
            if (joining.IsCompletedSuccessfully) Enter(Lobby.Join(_catalog, joining.Result, PlayerName()));
            else Ui.Set(_status, "Could not join: " + joining.Exception?.GetBaseException().Message);
        }
        if (_lobby == null) return;
        _lobby.Poll();
        if (_lobby.Revision == _shown) return;
        _shown = _lobby.Revision;
        if (_lobby.Starting != null)
        {
            var lobby = _lobby;
            _lobby = null;
            Starting?.Invoke(lobby);
            return;
        }
        if (_lobby.Closed is { } reason)
        {
            _lobby.Dispose();
            _lobby = null;
            ShowConnect();
            Ui.Set(_status, reason);
            return;
        }
        RefreshLobby();
    }

    public override void _ExitTree()
    {
        _lobby?.Dispose();
        _lobby = null;
    }

    private string PlayerName() => _name.Text.Trim().Length == 0 ? "Player" : _name.Text.Trim();

    // --- connect page ------------------------------------------------------------------------

    private void ShowConnect()
    {
        Ui.Clear(_page);
        _page.AddChild(Ui.Heading("Multiplayer"));
        _page.AddChild(Ui.Text("Play over a LAN or a direct IP address. The host needs its port reachable by the others.", 14, Ui.Dim));
        _page.AddChild(Row("Your name", _name));
        _page.AddChild(Row("Port", _port));
        var host = Ui.HBox(10);
        host.AddChild(Ui.Button("Host a game", HostGame, size: 16));
        _page.AddChild(host);
        _page.AddChild(Row("Host address", _address));
        var join = Ui.HBox(10);
        join.AddChild(Ui.Button("Join game", JoinGame, size: 16));
        _page.AddChild(join);
        _page.AddChild(_status);
        _page.AddChild(Ui.Button("Back", () => Back?.Invoke(), size: 15));
    }

    private static Control Row(string label, Control control)
    {
        var row = Ui.HBox(10);
        row.AddChild(Cell(Ui.Text(label, 15, wrap: false), 140));
        row.AddChild(control is OptionButton ? Cell(control, 220) : control);
        return row;
    }

    /// <summary>A fixed-width cell, so rows of controls line up in columns.</summary>
    private static Control Cell(Control control, float width)
    {
        control.CustomMinimumSize = new Vector2(width, control.CustomMinimumSize.Y);
        control.SizeFlagsHorizontal = SizeFlags.Fill;
        return control;
    }

    /// <summary>Host at once, for the lobby screenshot.</summary>
    public void DebugHost()
    {
        _port.Value = DefaultPort + 7;
        HostGame();
        _lobby?.AddSeat(SeatKind.Ai);
        _lobby?.Say("Welcome!");
    }

    private void HostGame()
    {
        try
        {
            var transport = TcpTransport.Host((int)_port.Value);
            Enter(Lobby.Host(_catalog, transport, PlayerName(), LobbySetup.Default(_catalog)));
        }
        catch (System.Net.Sockets.SocketException e) { Ui.Set(_status, "Could not host: " + e.Message); }
    }

    private void JoinGame()
    {
        if (_joining != null) return;
        Ui.Set(_status, "Connecting…");
        string address = _address.Text.Trim();
        int port = (int)_port.Value;
        _joining = Task.Run(() => TcpTransport.JoinAsync(address, port));
    }

    // --- lobby page --------------------------------------------------------------------------

    private void Enter(Lobby lobby)
    {
        _lobby = lobby;
        _shown = -1;
        Ui.Clear(_page);
        _page.AddChild(Ui.Heading(lobby.IsHost ? $"Hosting on port {(int)_port.Value}" : "Lobby"));
        var body = Ui.HBox(24);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        var left = Ui.VBox(8);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _setup = Ui.VBox(4);
        _seats = Ui.VBox(4);
        left.AddChild(_setup);
        left.AddChild(Ui.Heading("Seats"));
        left.AddChild(Ui.Scroll(_seats, 200));
        body.AddChild(left);
        var right = Ui.VBox(6);
        right.CustomMinimumSize = new Vector2(300, 0);
        right.AddChild(Ui.Heading("Chat"));
        right.AddChild(new ChatView(lobby.Chat, lobby.Say, 260));
        body.AddChild(right);
        _page.AddChild(body);
        var buttons = Ui.HBox(10);
        _lobbyMessage = Ui.Text("", 14, Ui.Warn);
        _lobbyMessage.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        buttons.AddChild(_lobbyMessage);
        buttons.AddChild(Ui.Button("Leave", Leave, size: 15));
        if (lobby.IsHost)
        {
            _startButton = Ui.Button("Start game", () => Ui.Set(_lobbyMessage!, _lobby?.Start() ?? ""), size: 17);
            _startButton.CustomMinimumSize = new Vector2(200, 44);
            buttons.AddChild(_startButton);
        }
        _page.AddChild(buttons);
        RefreshLobby();
    }

    private void Leave()
    {
        _lobby?.Dispose();
        _lobby = null;
        ShowConnect();
        Ui.Set(_status, "");
    }

    private void RefreshLobby()
    {
        if (_lobby is not { } lobby || _setup == null || _seats == null) return;
        var state = lobby.State;
        BuildSetup(lobby, state.Setup);
        Ui.Clear(_seats);
        for (int i = 0; i < state.Seats.Count; i++) _seats.AddChild(SeatRow(lobby, state, i));
        if (lobby.IsHost && state.SavedTurn == null)
        {
            var add = Ui.HBox(8);
            add.AddChild(Ui.Button("Add AI seat", () => lobby.AddSeat(SeatKind.Ai)));
            add.AddChild(Ui.Button("Add human seat", () => lobby.AddSeat(SeatKind.Human)));
            _seats.AddChild(add);
        }
        if (_lobbyMessage != null) Ui.Set(_lobbyMessage, lobby.IsHost ? state.StartProblem() ?? "Ready to start." : "Waiting for the host to start.");
        if (_startButton != null) _startButton.Disabled = state.StartProblem() != null;
    }

    private void BuildSetup(Lobby lobby, LobbySetup setup)
    {
        Ui.Clear(_setup!);
        _setup!.AddChild(Ui.Heading("Game"));
        string timer = setup.TurnTimerSeconds > 0 ? $", {setup.TurnTimerSeconds} s turn timer" : "";
        if (!lobby.IsHost || lobby.State.SavedTurn != null)
        {
            string Name(IReadOnlyList<SetupChoice> choices, string id) => choices.FirstOrDefault(c => c.Id == id)?.Name ?? id;
            if (lobby.State.SavedTurn is int turn) _setup.AddChild(Ui.Text($"Continuing a saved game at turn {turn}.", 14, Ui.Accent));
            _setup.AddChild(Ui.Text($"{Name(_catalog.WorldSizes, setup.WorldSize)} world, {Name(_catalog.GameSpeeds, setup.GameSpeed)} speed, "
                + $"{Name(_catalog.Handicaps, setup.Handicap)} difficulty, {setup.TurnMode} turns, seed {setup.Seed}{timer}", 14));
            if (lobby.IsHost)
            {
                _setup.AddChild(Row("Saved game", SaveChoice(lobby)));
                _setup.AddChild(Row("Turn timer", TimerBox(lobby, setup)));
            }
            return;
        }
        _setup.AddChild(Row("Saved game", SaveChoice(lobby)));
        _setup.AddChild(Row("World size", Choice(_catalog.WorldSizes, setup.WorldSize, id => lobby.SetSetup(setup with { WorldSize = id }))));
        if (_catalog.MapGenerators.Count > 0)
            _setup.AddChild(Row("Map", Choice(_catalog.MapGenerators, setup.MapGenerator, id => lobby.SetSetup(setup with { MapGenerator = id }))));
        _setup.AddChild(Row("Speed", Choice(_catalog.GameSpeeds, setup.GameSpeed, id => lobby.SetSetup(setup with { GameSpeed = id }))));
        _setup.AddChild(Row("Difficulty", Choice(_catalog.Handicaps, setup.Handicap, id => lobby.SetSetup(setup with { Handicap = id }))));
        var modes = Enum.GetValues<TurnMode>().Select(m => new SetupChoice(m.ToString(), m.ToString())).ToList();
        _setup.AddChild(Row("Turns", Choice(modes, setup.TurnMode.ToString(), id => lobby.SetSetup(setup with { TurnMode = Enum.Parse<TurnMode>(id) }))));
        var seed = new LineEdit { Text = setup.Seed.ToString(), CustomMinimumSize = new Vector2(160, 0) };
        seed.TextSubmitted += text => { if (ulong.TryParse(text, out var s)) lobby.SetSetup(setup with { Seed = s }); };
        seed.FocusExited += () => { if (ulong.TryParse(seed.Text, out var s) && s != setup.Seed) lobby.SetSetup(setup with { Seed = s }); };
        _setup.AddChild(Row("Seed", seed));
        _setup.AddChild(Row("Turn timer", TimerBox(lobby, setup)));
    }

    /// <summary>Seconds each turn segment may last before the host ends it; 0 is no timer.</summary>
    private static SpinBox TimerBox(Lobby lobby, LobbySetup setup)
    {
        var box = new SpinBox { MinValue = 0, MaxValue = 3600, Step = 15, Value = setup.TurnTimerSeconds, Suffix = "s (0 = off)" };
        box.ValueChanged += v => lobby.SetSetup(setup with { TurnTimerSeconds = (int)v });
        return box;
    }

    /// <summary>A new game, or one of the saves in the game's save folder, newest first.</summary>
    private OptionButton SaveChoice(Lobby lobby)
    {
        var dir = ProjectSettings.GlobalizePath(GameUi.SaveDirectory);
        var files = System.IO.Directory.Exists(dir)
            ? System.IO.Directory.GetFiles(dir, "*.json").OrderByDescending(System.IO.File.GetLastWriteTime).ToList() : [];
        var choices = new List<SetupChoice> { new("", "New game") };
        choices.AddRange(files.Select(f => new SetupChoice(f, System.IO.Path.GetFileNameWithoutExtension(f))));
        string selected = lobby.State.SavedTurn == null ? "" : _loadedSave ?? "";
        return Choice(choices, selected, path =>
        {
            if (path.Length == 0) { _loadedSave = null; lobby.ClearSave(); return; }
            string? problem;
            try { problem = lobby.LoadSave(System.IO.File.ReadAllText(path)); }
            catch (System.IO.IOException e) { problem = e.Message; }
            if (problem == null) _loadedSave = path;
            else if (_lobbyMessage != null) Ui.Set(_lobbyMessage, problem);
        });
    }

    private Control SeatRow(Lobby lobby, LobbyState state, int index)
    {
        var seat = state.Seats[index];
        var row = Ui.HBox(8);
        row.AddChild(Cell(Ui.Text($"Seat {index + 1}", 14, Ui.Accent, wrap: false), 60));
        if (lobby.IsHost)
            row.AddChild(Cell(Ui.Button(seat.Kind == SeatKind.Ai ? "AI" : "Human",
                () => lobby.SetSeatKind(index, seat.Kind == SeatKind.Ai ? SeatKind.Human : SeatKind.Ai), "Switch between a human and an AI seat"), 80));
        string who = seat.Kind == SeatKind.Ai ? "Computer" : seat.Peer is int peer ? state.NameOf(peer) : "Open";
        row.AddChild(Cell(Ui.Text(who, 14, seat.Peer == lobby.LocalPeer ? Ui.Good : null, wrap: false), 130));
        bool mine = seat.Kind == SeatKind.Human && seat.Peer == lobby.LocalPeer;
        Control action = new Control();
        if (seat.Kind == SeatKind.Human && seat.Peer == null && state.Peers.Any(p => p.Peer == lobby.LocalPeer && p.ContentMatches))
            action = Ui.Button("Take", () => lobby.TakeSeat(index));
        else if (mine && !lobby.IsHost) action = Ui.Button("Stand up", lobby.LeaveSeat);
        row.AddChild(Cell(action, 90));
        bool saved = state.SavedTurn != null;
        bool canPickLeader = !saved && (mine || (lobby.IsHost && seat.Kind == SeatKind.Ai));
        string leaderKey = seat.Leader.Length == 0 ? "" : seat.Leader + "|" + seat.Civilization;
        if (canPickLeader)
        {
            var leaders = new List<SetupChoice> { new("", "Random leader") };
            leaders.AddRange(_catalog.Leaders.Select(l => new SetupChoice(l.Id + "|" + l.CivilizationId, $"{l.Name} ({l.CivilizationName})")));
            row.AddChild(Cell(Choice(leaders, leaderKey, key =>
            {
                var (leader, civ) = key.Length == 0 ? ("", "") : (key.Split('|')[0], key.Split('|')[1]);
                if (mine) lobby.ChooseLeader(leader, civ);
                else lobby.SetSeatLeader(index, leader, civ);
            }), 240));
        }
        else
        {
            var chosen = _catalog.Leaders.FirstOrDefault(l => l.Id == seat.Leader && (seat.Civilization.Length == 0 || l.CivilizationId == seat.Civilization));
            row.AddChild(Cell(Ui.Text(chosen == null ? "Random leader" : $"{chosen.Name} ({chosen.CivilizationName})", 14, Ui.Dim, wrap: false), 240));
        }
        if (lobby.IsHost && !saved)
        {
            var team = new SpinBox { MinValue = 0, MaxValue = 31, Value = seat.Team >= 0 ? seat.Team : index, Prefix = "Team" };
            team.ValueChanged += v => lobby.SetTeam(index, (int)v);
            row.AddChild(team);
            if (state.Seats.Count > 2 && index > 0) row.AddChild(Ui.Button("Remove", () => lobby.RemoveSeat(index)));
        }
        else if (seat.Team >= 0) row.AddChild(Ui.Text($"Team {seat.Team}", 14, Ui.Dim, wrap: false));
        return row;
    }

    private static OptionButton Choice(IReadOnlyList<SetupChoice> choices, string selected, Action<string> chosen)
    {
        var button = new OptionButton { Theme = Ui.Theme, FitToLongestItem = false, ClipText = true };
        for (int i = 0; i < choices.Count; i++)
        {
            button.AddItem(choices[i].Name, i);
            if (choices[i].Id == selected) button.Selected = i;
        }
        button.ItemSelected += i => chosen(choices[(int)i].Id);
        return button;
    }
}
