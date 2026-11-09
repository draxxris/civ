using System.Text.Json;
using Ariel.Game;
using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// The pre-game room. The host owns the <see cref="LobbyState"/>: clients send requests, the host applies them and
/// broadcasts the whole state, so every peer always shows the same seats. Starting freezes the setup into
/// <see cref="SimulationSettings"/> on the host and sends them to everyone, so each peer builds exactly the same world.
/// Engine-free; a front end calls <see cref="Poll"/> each frame and redraws when <see cref="Revision"/> moves.
/// </summary>
public sealed class Lobby : IDisposable
{
    private readonly GameCatalog _catalog;
    private ITransport? _transport;

    public bool IsHost { get; }
    public string LocalName { get; }
    public int LocalPeer { get; }
    public LobbyState State { get; private set; }
    public ChatLog Chat { get; } = new();
    /// <summary>Bumps whenever anything shown changes: state, chat, or the lobby closing or starting.</summary>
    public int Revision { get; private set; }
    /// <summary>Why the lobby ended without a game (host left, refused), or null.</summary>
    public string? Closed { get; private set; }
    /// <summary>Set once the game starts; then <see cref="Release"/> hands the connection to the game.</summary>
    public GameStart? Starting { get; private set; }

    private Lobby(GameCatalog catalog, ITransport transport, string name, bool host, LobbyState state)
    {
        _catalog = catalog;
        _transport = transport;
        IsHost = host;
        LocalName = name;
        LocalPeer = transport.LocalPeer;
        State = state;
        transport.Received += OnReceived;
        transport.PeerJoined += OnPeerJoined;
        transport.PeerLeft += OnPeerLeft;
    }

    /// <summary>Open a lobby on <paramref name="transport"/> as its host (peer 0), seated in the first of
    /// <paramref name="seats"/> seats. The rest start as open human seats.</summary>
    public static Lobby Host(GameCatalog catalog, ITransport transport, string name, LobbySetup setup, int seats = 2)
    {
        if (transport.LocalPeer != 0) throw new ArgumentException("The host is peer 0", nameof(transport));
        var seatList = Enumerable.Range(0, Math.Max(2, seats)).Select(i => new LobbySeat(SeatKind.Human, i == 0 ? 0 : null)).ToList();
        var state = new LobbyState(1, setup, seatList, [new LobbyPeer(0, name, true)]);
        return new Lobby(catalog, transport, name, host: true, state);
    }

    /// <summary>Enter a host's lobby over a connected transport; the host's state arrives with the next poll.</summary>
    public static Lobby Join(GameCatalog catalog, ITransport transport, string name)
    {
        var lobby = new Lobby(catalog, transport, name, host: false,
            new LobbyState(0, LobbySetup.Default(catalog), [], [new LobbyPeer(transport.LocalPeer, name, true)]));
        lobby.Request(new Message(MessageType.Hello) { Name = name, ContentHash = catalog.Content.ContentHash });
        return lobby;
    }

    /// <summary>Receive what arrived. Does nothing once the game is starting: lockstep input must wait in the transport
    /// until the game exists to take it.</summary>
    public void Poll()
    {
        if (Starting == null) _transport?.Poll();
    }

    // --- requests anyone can make ------------------------------------------------------------

    /// <summary>Sit in an open human seat, leaving any other.</summary>
    public void TakeSeat(int seat) => Request(new Message(MessageType.Take) { Seat = seat });

    /// <summary>Stand up and watch until starting, when unseated peers are turned away.</summary>
    public void LeaveSeat() => Request(new Message(MessageType.Leave));

    /// <summary>Choose the leader of the local player's seat; empty for random.</summary>
    public void ChooseLeader(string leader, string civilization = "") =>
        Request(new Message(MessageType.Leader) { Leader = leader, Civilization = civilization });

    public void Say(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || _transport == null) return;
        _transport.Broadcast(NetMessage.OfText(NetMessageKind.Chat, LocalPeer, text));
        Chat.Add(new ChatLine(LocalPeer, LocalName, text));
        Revision++;
    }

    // --- host controls -----------------------------------------------------------------------

    public void SetSetup(LobbySetup setup) => HostChange(s => s with { Setup = setup });

    /// <summary>Make a seat human or AI; a seat made AI frees its player.</summary>
    public void SetSeatKind(int seat, SeatKind kind) =>
        HostChangeSeat(seat, s => s with { Kind = kind, Peer = kind == SeatKind.Ai ? null : s.Peer });

    public void SetTeam(int seat, int team) => HostChangeSeat(seat, s => s with { Team = team });

    /// <summary>Set any seat's leader, typically an AI's; empty for random.</summary>
    public void SetSeatLeader(int seat, string leader, string civilization = "") =>
        HostChangeSeat(seat, s => s with { Leader = leader, Civilization = civilization });

    public void AddSeat(SeatKind kind = SeatKind.Ai) => HostChange(s => s with { Seats = [.. s.Seats, new LobbySeat(kind)] });

    public void RemoveSeat(int seat)
    {
        if (State.Seats.Count <= 2 || seat < 0 || seat >= State.Seats.Count) return;
        HostChange(s => s with { Seats = s.Seats.Where((_, i) => i != seat).ToList() });
    }

    /// <summary>Freeze the setup and start everyone's game. Returns why it cannot start, or null.</summary>
    public string? Start()
    {
        if (!IsHost || _transport == null || Starting != null) return "Only the host starts the game.";
        if (State.StartProblem() is { } problem) return problem;
        var settings = State.ToSettings(_catalog);
        var message = new Message(MessageType.Start) { Settings = SaveGame.SaveSettings(settings), State = State };
        foreach (var peer in State.Peers.Where(p => p.Peer != 0 && State.SeatOf(p.Peer) == null))
            Send(peer.Peer, new Message(MessageType.Refuse) { Reason = "The game started without you." });
        _transport.Broadcast(Encode(message));
        Begin(settings, State);
        return null;
    }

    /// <summary>Hand the connection to the game that is starting; the lobby stops listening to it.</summary>
    public ITransport Release()
    {
        var transport = _transport ?? throw new InvalidOperationException("The lobby has no connection");
        transport.Received -= OnReceived;
        transport.PeerJoined -= OnPeerJoined;
        transport.PeerLeft -= OnPeerLeft;
        _transport = null;
        return transport;
    }

    public void Dispose()
    {
        _transport?.Dispose();
        _transport = null;
    }

    // --- protocol ----------------------------------------------------------------------------

    private enum MessageType { Hello, Take, Leave, Leader, State, Start, Refuse }

    private sealed record Message(MessageType Type)
    {
        public string? Name { get; init; }
        public string? ContentHash { get; init; }
        public int Seat { get; init; }
        public string? Leader { get; init; }
        public string? Civilization { get; init; }
        public LobbyState? State { get; init; }
        public string? Settings { get; init; }
        public string? Reason { get; init; }
    }

    private NetMessage Encode(Message m) => NetMessage.OfText(NetMessageKind.Lobby, LocalPeer, JsonSerializer.Serialize(m));

    private void Send(int peer, Message m) => _transport?.SendTo(peer, Encode(m));

    /// <summary>A request goes to the host; the host applies its own directly.</summary>
    private void Request(Message m)
    {
        if (IsHost) Handle(LocalPeer, m);
        else Send(0, m);
    }

    private void OnReceived(NetMessage m)
    {
        if (m.Kind == NetMessageKind.Chat && m.Text is { } text)
        {
            Chat.Add(new ChatLine(m.Peer, State.NameOf(m.Peer), text));
            Revision++;
            return;
        }
        if (m.Kind != NetMessageKind.Lobby || m.Text is null) return;
        Message? message;
        try { message = JsonSerializer.Deserialize<Message>(m.Text); }
        catch (JsonException) { return; }
        if (message != null) Handle(m.Peer, message);
    }

    private void Handle(int from, Message m)
    {
        if (IsHost) HandleRequest(from, m);
        else if (from == 0) HandleFromHost(m);
    }

    private void HandleFromHost(Message m)
    {
        switch (m.Type)
        {
            case MessageType.State when m.State is { } state:
                State = state;
                Revision++;
                break;
            case MessageType.Start when m.State is { } state && m.Settings is { } settings:
                State = state;
                Begin(SaveGame.LoadSettings(settings), state);
                break;
            case MessageType.Refuse:
                Closed = m.Reason ?? "The host turned you away.";
                Revision++;
                break;
        }
    }

    private void HandleRequest(int from, Message m)
    {
        switch (m.Type)
        {
            case MessageType.Hello:
                bool matches = m.ContentHash == _catalog.Content.ContentHash;
                HostChange(s => s with { Peers = [.. s.Peers.Where(p => p.Peer != from), new LobbyPeer(from, m.Name ?? $"Player {from}", matches)] });
                if (!matches) Notice($"{m.Name} has different mods and cannot take a seat.");
                break;
            case MessageType.Take:
                if (m.Seat < 0 || m.Seat >= State.Seats.Count) return;
                var seat = State.Seats[m.Seat];
                if (seat.Kind != SeatKind.Human || (seat.Peer != null && seat.Peer != from)) return;
                if (State.Peers.FirstOrDefault(p => p.Peer == from) is not { ContentMatches: true }) return;
                HostChange(s => s with
                {
                    Seats = s.Seats.Select((x, i) => i == m.Seat ? x with { Peer = from } : x.Peer == from ? x with { Peer = null } : x).ToList(),
                });
                break;
            case MessageType.Leave:
                HostChange(s => s with { Seats = s.Seats.Select(x => x.Peer == from ? x with { Peer = null } : x).ToList() });
                break;
            case MessageType.Leader when State.SeatOf(from) is int own:
                HostChangeSeat(own, x => x with { Leader = m.Leader ?? "", Civilization = m.Civilization ?? "" });
                break;
        }
    }

    private void OnPeerJoined(int peer)
    {
        if (!IsHost) return;
        Send(peer, new Message(MessageType.State) { State = State });
    }

    private void OnPeerLeft(int peer)
    {
        if (!IsHost)
        {
            if (peer != 0) return;
            Closed = "The host left.";
            Revision++;
            return;
        }
        string name = State.NameOf(peer);
        HostChange(s => s with
        {
            Peers = s.Peers.Where(p => p.Peer != peer).ToList(),
            Seats = s.Seats.Select(x => x.Peer == peer ? x with { Peer = null } : x).ToList(),
        });
        Notice($"{name} left.");
    }

    private void Notice(string text)
    {
        Chat.Notice(text);
        Revision++;
    }

    private void HostChange(Func<LobbyState, LobbyState> change)
    {
        if (!IsHost || Starting != null) return;
        State = change(State) with { Revision = State.Revision + 1 };
        Revision++;
        _transport?.Broadcast(Encode(new Message(MessageType.State) { State = State }));
    }

    private void HostChangeSeat(int seat, Func<LobbySeat, LobbySeat> change)
    {
        if (seat < 0 || seat >= State.Seats.Count) return;
        HostChange(s => s with { Seats = s.Seats.Select((x, i) => i == seat ? change(x) : x).ToList() });
    }

    private void Begin(SimulationSettings settings, LobbyState state)
    {
        var names = state.Peers.ToDictionary(p => p.Peer, p => p.Name);
        if (state.SeatOf(LocalPeer) is not int seat)
        {
            Closed ??= "The game started without you.";
            Revision++;
            return;
        }
        Starting = new GameStart(settings, state.SeatedPeers(), seat, names);
        Revision++;
    }
}
