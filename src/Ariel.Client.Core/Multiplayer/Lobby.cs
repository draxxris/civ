using Ariel.Game;
using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// The pre-game room. The host owns the <see cref="LobbyState"/>: clients send requests, the host applies them and
/// broadcasts the whole state, so every peer always shows the same seats. Starting freezes the setup into
/// <see cref="SimulationSettings"/> on the host, or hands out the save the host loaded, so each peer builds exactly the
/// same game. A peer that connects to a game already running is answered by that game instead, and resumes it from a
/// snapshot. Engine-free; a front end calls <see cref="Poll"/> each frame and redraws when <see cref="Revision"/> moves.
/// </summary>
public sealed class Lobby : IDisposable
{
    private readonly GameCatalog _catalog;
    private readonly List<NetMessage> _early = [];
    private ITransport? _transport;
    private string? _save;

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

    /// <summary>Enter a host's lobby, or its running game, over a connected transport; the host answers on a later poll.</summary>
    public static Lobby Join(GameCatalog catalog, ITransport transport, string name)
    {
        var lobby = new Lobby(catalog, transport, name, host: false,
            new LobbyState(0, LobbySetup.Default(catalog), [], [new LobbyPeer(transport.LocalPeer, name, true)]));
        lobby.Request(new PeerMessage(PeerMessageType.Hello) { Name = name, ContentHash = catalog.Content.ContentHash });
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
    public void TakeSeat(int seat) => Request(new PeerMessage(PeerMessageType.Take) { Seat = seat });

    /// <summary>Stand up and watch until starting, when unseated peers are turned away.</summary>
    public void LeaveSeat() => Request(new PeerMessage(PeerMessageType.Leave));

    /// <summary>Choose the leader of the local player's seat; empty for random. Not for a saved game.</summary>
    public void ChooseLeader(string leader, string civilization = "") =>
        Request(new PeerMessage(PeerMessageType.Leader) { Leader = leader, Civilization = civilization });

    public void Say(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || _transport == null) return;
        _transport.Broadcast(NetMessage.OfText(NetMessageKind.Chat, LocalPeer, text));
        Chat.Add(new ChatLine(LocalPeer, LocalName, text));
        Revision++;
    }

    // --- host controls -----------------------------------------------------------------------

    public void SetSetup(LobbySetup setup) => HostChange(s => s.SavedTurn == null ? s with { Setup = setup }
        : s with { Setup = s.Setup with { TurnTimerSeconds = setup.TurnTimerSeconds } });

    /// <summary>Make a seat human or AI; a seat made AI frees its player.</summary>
    public void SetSeatKind(int seat, SeatKind kind) =>
        HostChangeSeat(seat, s => s with { Kind = kind, Peer = kind == SeatKind.Ai ? null : s.Peer });

    public void SetTeam(int seat, int team)
    {
        if (State.SavedTurn == null) HostChangeSeat(seat, s => s with { Team = team });
    }

    /// <summary>Set any seat's leader, typically an AI's; empty for random.</summary>
    public void SetSeatLeader(int seat, string leader, string civilization = "")
    {
        if (State.SavedTurn == null) HostChangeSeat(seat, s => s with { Leader = leader, Civilization = civilization });
    }

    public void AddSeat(SeatKind kind = SeatKind.Ai)
    {
        if (State.SavedTurn == null) HostChange(s => s with { Seats = [.. s.Seats, new LobbySeat(kind)] });
    }

    public void RemoveSeat(int seat)
    {
        if (State.SavedTurn != null || State.Seats.Count <= 2 || seat < 0 || seat >= State.Seats.Count) return;
        HostChange(s => s with { Seats = s.Seats.Where((_, i) => i != seat).ToList() });
    }

    /// <summary>Continue a saved game: its players become the seats, with its setup. Returns why the save cannot be
    /// used, or null. The host keeps its seat if that player is human.</summary>
    public string? LoadSave(string json)
    {
        if (!IsHost) return "Only the host loads a game.";
        Simulation sim;
        try { sim = SaveGame.Load(_catalog.Content, json); }
        catch (InvalidDataException e)
        {
            return "Can't load: " + e.Message;
        }
        var settings = sim.Settings;
        var majors = sim.State.Players.Where(p => p.Id < settings.PlayerCount).OrderBy(p => p.Id).ToList();
        var seats = majors.Select(p => new LobbySeat(p.Ai == null ? SeatKind.Human : SeatKind.Ai, null, p.Leader, p.Civilization, p.TeamId)).ToList();
        int hostSeat = seats.FindIndex(s => s.Kind == SeatKind.Human);
        if (hostSeat >= 0) seats[hostSeat] = seats[hostSeat] with { Peer = 0 };
        var setup = new LobbySetup(settings.WorldSize ?? "", settings.GameSpeed, settings.Handicap, settings.MapGenerator, settings.StartingEra,
            settings.Seed, settings.TurnMode, settings.GameOptions, State.Setup.TurnTimerSeconds);
        _save = json;
        HostChange(s => s with { Setup = setup, Seats = seats, SavedTurn = sim.State.Turn });
        return null;
    }

    /// <summary>Go back from a loaded save to a new game, with two seats and the host in the first.</summary>
    public void ClearSave()
    {
        if (!IsHost || _save == null) return;
        _save = null;
        HostChange(s => s with
        {
            SavedTurn = null,
            Seats = [new LobbySeat(SeatKind.Human, 0), new LobbySeat(SeatKind.Human)],
        });
    }

    /// <summary>Freeze the setup and start everyone's game. Returns why it cannot start, or null.</summary>
    public string? Start()
    {
        if (!IsHost || _transport == null || Starting != null) return "Only the host starts the game.";
        if (State.StartProblem() is { } problem) return problem;
        var settings = _save == null ? State.ToSettings(_catalog) : null;
        var message = new PeerMessage(PeerMessageType.Start)
        {
            Settings = settings == null ? null : SaveGame.SaveSettings(settings), Save = _save, State = State,
            TurnTimerSeconds = State.Setup.TurnTimerSeconds,
        };
        foreach (var peer in State.Peers.Where(p => p.Peer != 0 && State.SeatOf(p.Peer) == null))
            Send(peer.Peer, new PeerMessage(PeerMessageType.Refuse) { Reason = "The game started without you." });
        _transport.Broadcast(message.Encode(LocalPeer));
        Begin(settings, _save, State);
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

    private void Send(int peer, PeerMessage m) => _transport?.SendTo(peer, m.Encode(LocalPeer));

    /// <summary>A request goes to the host; the host applies its own directly.</summary>
    private void Request(PeerMessage m)
    {
        if (IsHost) HandleRequest(LocalPeer, m);
        else Send(0, m);
    }

    private void OnReceived(NetMessage m)
    {
        // Lockstep input from peers already playing, and anything at all once the game is starting (a poll drains the
        // whole inbox, past the start message): the game replays it.
        if (Starting != null || PeerMessage.IsLockstep(m))
        {
            _early.Add(m);
            return;
        }
        if (m.Kind == NetMessageKind.Chat && m.Text is { } text)
        {
            Chat.Add(new ChatLine(m.Peer, State.NameOf(m.Peer), text));
            Revision++;
            return;
        }
        if (PeerMessage.Decode(m) is not { } message) return;
        if (IsHost) HandleRequest(m.Peer, message);
        else if (m.Peer == 0) HandleFromHost(message);
    }

    private void HandleFromHost(PeerMessage m)
    {
        switch (m.Type)
        {
            case PeerMessageType.State when m.State is { } state:
                State = state;
                Revision++;
                break;
            case PeerMessageType.Start when m.State is { } state:
                State = state;
                Begin(m.Settings is { } settings ? SaveGame.LoadSettings(settings) : null, m.Save, state, m.TurnTimerSeconds);
                break;
            case PeerMessageType.Resume when m.Save is { } save && m.Peers is { } peers && m.Seats is { } seats && m.Names is { } names:
                Starting = new GameStart(null, peers, m.Seat, names)
                {
                    Save = save, ResumeTick = m.Tick, Seats = seats, Early = _early, Batches = m.Batches ?? [],
                    TurnTimerSeconds = m.TurnTimerSeconds,
                };
                Revision++;
                break;
            case PeerMessageType.Refuse:
                Closed = m.Reason ?? "The host turned you away.";
                Revision++;
                break;
        }
    }

    private void HandleRequest(int from, PeerMessage m)
    {
        switch (m.Type)
        {
            case PeerMessageType.Hello:
                bool matches = m.ContentHash == _catalog.Content.ContentHash;
                HostChange(s => s with { Peers = [.. s.Peers.Where(p => p.Peer != from), new LobbyPeer(from, m.Name ?? $"Player {from}", matches)] });
                if (!matches) Notice($"{m.Name} has different mods and cannot take a seat.");
                break;
            case PeerMessageType.Take:
                if (m.Seat < 0 || m.Seat >= State.Seats.Count) return;
                var seat = State.Seats[m.Seat];
                if (seat.Kind != SeatKind.Human || (seat.Peer != null && seat.Peer != from)) return;
                if (State.Peers.FirstOrDefault(p => p.Peer == from) is not { ContentMatches: true }) return;
                HostChange(s => s with
                {
                    Seats = s.Seats.Select((x, i) => i == m.Seat ? x with { Peer = from } : x.Peer == from ? x with { Peer = null } : x).ToList(),
                });
                break;
            case PeerMessageType.Leave:
                HostChange(s => s with { Seats = s.Seats.Select(x => x.Peer == from ? x with { Peer = null } : x).ToList() });
                break;
            case PeerMessageType.Leader when State.SavedTurn == null && State.SeatOf(from) is int own:
                HostChangeSeat(own, x => x with { Leader = m.Leader ?? "", Civilization = m.Civilization ?? "" });
                break;
        }
    }

    private void OnPeerJoined(int peer)
    {
        if (IsHost) Send(peer, new PeerMessage(PeerMessageType.State) { State = State });
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
        _transport?.Broadcast(new PeerMessage(PeerMessageType.State) { State = State }.Encode(LocalPeer));
    }

    private void HostChangeSeat(int seat, Func<LobbySeat, LobbySeat> change)
    {
        if (seat < 0 || seat >= State.Seats.Count) return;
        HostChange(s => s with { Seats = s.Seats.Select((x, i) => i == seat ? change(x) : x).ToList() });
    }

    private void Begin(SimulationSettings? settings, string? save, LobbyState state, int? turnTimer = null)
    {
        if (state.SeatOf(LocalPeer) is not int seat)
        {
            Closed ??= "The game started without you.";
            Revision++;
            return;
        }
        var seats = new Dictionary<int, int>();
        foreach (int peer in state.SeatedPeers()) seats[peer] = state.SeatOf(peer)!.Value;
        Starting = new GameStart(settings, state.SeatedPeers(), seat, state.Peers.ToDictionary(p => p.Peer, p => p.Name))
        {
            Save = save, Seats = seats, Early = _early, TurnTimerSeconds = turnTimer ?? state.Setup.TurnTimerSeconds,
        };
        Revision++;
    }
}
