using System.Diagnostics;
using Ariel.Game;
using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// A started networked game: the simulation every peer built from the same settings or save, its lockstep session,
/// the local player's <see cref="GameSession"/>, and the chat and peer names carried over from the lobby. The
/// session's pump drives lockstep; <see cref="Update"/> runs the rest once a frame.
/// <para>The host keeps the game going when people come and go. A seated peer that leaves is a lockstep member up to
/// the last batch the host received from it, and its player passes to the AI. A peer that connects later takes back a
/// seat the AI is holding: the host admits it at a tick nobody has run, then sends it a snapshot of the state before
/// that tick with every batch held for it. After a desync every peer saves its state for diagnosis, and the host
/// sends everyone the same kind of snapshot to restart from. With a turn timer, the host ends the turns of humans who
/// let it run out.</para>
/// </summary>
public sealed class MultiplayerGame : IDisposable
{
    private readonly GameCatalog _catalog;
    private readonly ITransport _transport;
    private readonly LockstepSession _lockstep;
    private readonly LockstepChannel _channel;
    private readonly Dictionary<int, string> _names;
    private readonly Dictionary<int, int> _seats;                 // lockstep peer -> seat
    private readonly List<(int Seat, string Name)> _vacated = []; // seats the AI holds for someone who left
    private readonly int _turnTimerMs;
    private readonly int _hostSeat;
    private bool _resyncing;
    private (int Turn, int Segment) _timerFor = (-1, -1);
    private long _deadline;
    private bool _timedOut;

    public GameSession Session { get; }
    public ChatLog Chat { get; }
    public int LocalPeer => _transport.LocalPeer;
    /// <summary>The lockstep tick about to run, for diagnostics.</summary>
    public int Tick => _lockstep.Tick;
    /// <summary>What lockstep is waiting for, for diagnostics.</summary>
    public string Describe() => _lockstep.Describe();
    public bool IsHost => LocalPeer == 0;
    /// <summary>Where each peer saves its state when a desync is found, for comparing afterwards; null to skip.</summary>
    public string? DesyncDirectory { get; set; }
    /// <summary>Seconds left before the host ends the turn of everyone still moving, or null without a timer.</summary>
    public int? TurnSecondsLeft => _turnTimerMs <= 0 || _timerFor.Turn < 0 ? null
        : (int)Math.Max(0, (_deadline - Environment.TickCount64 + 999) / 1000);
    /// <summary>Bumps when the people in the game change, for front ends listing them.</summary>
    public int Revision { get; private set; }

    private MultiplayerGame(GameCatalog catalog, ITransport transport, GameStart start, ChatLog chat, Simulation sim)
    {
        _catalog = catalog;
        _transport = transport;
        _names = new Dictionary<int, string>(start.Names);
        _seats = new Dictionary<int, int>(start.Seats);
        _turnTimerMs = Math.Max(0, start.TurnTimerSeconds) * 1000;
        _hostSeat = start.Seats.TryGetValue(0, out int hostSeat) ? hostSeat : 0;
        Chat = chat;
        var codec = new CommandCodec(sim.Content);
        _lockstep = start.ResumeTick is int tick
            ? LockstepSession.Resume(sim, codec, transport, start.Peers, transport.LocalPeer, tick)
            : new LockstepSession(sim, codec, transport, start.Peers, transport.LocalPeer);
        foreach (var batch in start.Batches) _lockstep.Deliver(new NetMessage(NetMessageKind.Commands, batch.Tick, batch.Peer, batch.Commands, 0));
        _lockstep.Desync += OnDesync;
        _channel = new LockstepChannel(_lockstep);
        // Every peer runs turns in the background, since lockstep compares states between commands.
        Session = new GameSession(sim, start.LocalSeat, _channel, backgroundTurns: true);
        foreach (var early in start.Early)
            if (PeerMessage.IsLockstep(early)) _lockstep.Deliver(early);
            else OnReceived(early);
        transport.Received += OnReceived;
        transport.PeerLeft += OnPeerLeft;
        if (IsHost && start.Save != null) MatchSeatsToLobby(sim, start);
    }

    /// <summary>Build the agreed world, or load the agreed save, and join lockstep. Generating the world takes a while,
    /// so call it off the game thread; input from faster peers waits in the transport until then.</summary>
    public static MultiplayerGame Create(GameCatalog catalog, ITransport transport, GameStart start, ChatLog chat)
    {
        var sim = start.Save is { } save ? SaveGame.Load(catalog.Content, save)
            : new Simulation(catalog.Content, start.Settings ?? throw new ArgumentException("A new game needs settings"));
        return new MultiplayerGame(catalog, transport, start, chat, sim);
    }

    public string NameOf(int peer) => _names.TryGetValue(peer, out var name) ? name : $"Player {peer}";

    /// <summary>The peers seated in the game, with their seats.</summary>
    public IEnumerable<(int Peer, int Seat, string Name)> Players() => _seats.OrderBy(s => s.Value).Select(s => (s.Key, s.Value, NameOf(s.Key)));

    public void Say(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _transport.Broadcast(NetMessage.OfText(NetMessageKind.Chat, LocalPeer, text));
        Chat.Add(new ChatLine(LocalPeer, NameOf(LocalPeer), text));
    }

    /// <summary>Once a frame: the turn timer. Returns at once when there is none.</summary>
    public void Update()
    {
        if (_turnTimerMs <= 0) return;
        var state = Session.Sim.State;
        var now = (state.Turn, state.TurnOrder.Active);
        if (now != _timerFor)
        {
            _timerFor = now;
            _deadline = Environment.TickCount64 + _turnTimerMs;
            _timedOut = false;
            return;
        }
        if (!IsHost || _timedOut || Environment.TickCount64 < _deadline) return;
        _timedOut = true;
        foreach (var player in state.Players.Where(p => p.Ai == null && !p.EndedTurn && Session.Sim.MayAct(p.Id)).ToList())
            _lockstep.Enqueue(new EndTurnCommand { Player = player.Id });
        Chat.Notice("Time is up: the turn ends.");
    }

    public void Dispose()
    {
        _transport.Received -= OnReceived;
        _transport.PeerLeft -= OnPeerLeft;
        _lockstep.Dispose();
        _transport.Dispose();
    }

    // --- people coming and going -------------------------------------------------------------

    private void OnPeerLeft(int peer)
    {
        if (!_seats.ContainsKey(peer)) return; // never part of the game
        if (peer == 0)
        {
            Chat.Notice("The host left the game.");
            _channel.Fail("The host left the game.");
            return;
        }
        if (!IsHost) return; // the host announces when the peer's input ends
        int last = _lockstep.LastBatchTick(peer);
        int seat = _seats[peer];
        string name = NameOf(peer);
        Depart(peer, last);
        _transport.Broadcast(new PeerMessage(PeerMessageType.Departed) { Peer = peer, Tick = last }.Encode(LocalPeer, NetMessageKind.Control));
        _vacated.Add((seat, name));
        if (_catalog.OpponentAi is { } ai)
            _lockstep.Enqueue(new SetPlayerControlCommand { Player = _hostSeat, Target = seat, Ai = ai });
        Chat.Notice($"{name} left the game; the AI plays for them until they return.");
    }

    private void Depart(int peer, int lastTick)
    {
        _lockstep.RemovePeer(peer, lastTick);
        _seats.Remove(peer);
        Revision++;
    }

    /// <summary>A peer connected while the game runs: give it back a seat the AI is holding, or turn it away.</summary>
    private void Admit(int peer, PeerMessage hello)
    {
        void Refuse(string reason) => _transport.SendTo(peer, new PeerMessage(PeerMessageType.Refuse) { Reason = reason }.Encode(LocalPeer));
        if (hello.ContentHash != _catalog.Content.ContentHash) { Refuse("Your mods differ from the host's."); return; }
        int index = _vacated.FindIndex(v => v.Name == hello.Name);
        if (index < 0) index = _vacated.Count > 0 ? 0 : -1;
        if (index < 0) { Refuse("The game is running and has no free seat."); return; }
        var (seat, _) = _vacated[index];
        _vacated.RemoveAt(index);
        string name = hello.Name ?? $"Player {peer}";

        int tick = _lockstep.NextSubmit;
        _lockstep.AddPeer(peer, tick);
        _seats[peer] = seat;
        _names[peer] = name;
        Revision++;
        _transport.Broadcast(new PeerMessage(PeerMessageType.Joined) { Peer = peer, Tick = tick, Seat = seat, Name = name }
            .Encode(LocalPeer, NetMessageKind.Control));
        _lockstep.Enqueue(new SetPlayerControlCommand { Player = _hostSeat, Target = seat, Ai = null });
        _lockstep.SnapshotBefore(tick, () => _transport.SendTo(peer, (Snapshot(PeerMessageType.Resume, tick) with { Seat = seat })
            .Encode(LocalPeer, NetMessageKind.Snapshot)));
        Chat.Notice($"{name} rejoined.");
    }

    /// <summary>The state before <paramref name="tick"/> and every batch held for it and later, with the roster.</summary>
    private PeerMessage Snapshot(PeerMessageType type, int tick) => new(type)
    {
        Tick = tick,
        Save = SaveGame.Save(_lockstep.Simulation),
        Batches = _lockstep.BatchesFrom(tick).Select(b => new CarriedBatch(b.Tick, b.Peer, b.Commands)).ToList(),
        Peers = _seats.Keys.Order().ToArray(),
        Seats = new Dictionary<int, int>(_seats),
        Names = new Dictionary<int, string>(_names),
        TurnTimerSeconds = _turnTimerMs / 1000,
    };

    /// <summary>A loaded save may seat humans where it had AI, or the reverse; switch them at the first ticks.</summary>
    private void MatchSeatsToLobby(Simulation sim, GameStart start)
    {
        var humans = start.Seats.Values.ToHashSet();
        foreach (var player in sim.State.Players.Where(p => p.Id < sim.Settings.PlayerCount))
        {
            bool human = humans.Contains(player.Id);
            if (human && player.Ai != null)
                _lockstep.Enqueue(new SetPlayerControlCommand { Player = _hostSeat, Target = player.Id, Ai = null });
            else if (!human && player.Ai == null && _catalog.OpponentAi is { } ai)
                _lockstep.Enqueue(new SetPlayerControlCommand { Player = _hostSeat, Target = player.Id, Ai = ai });
        }
    }

    // --- desync ------------------------------------------------------------------------------

    private void OnDesync(int tick)
    {
        if (DesyncDirectory is { } dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"desync-tick{tick}-peer{LocalPeer}.json"), SaveGame.Save(_lockstep.Simulation));
            }
            catch (IOException) { }
        }
        if (_resyncing) return;
        _resyncing = true;
        Chat.Notice($"The game went out of sync at step {tick}; resynchronizing from the host.");
        if (!IsHost) return;
        int at = _lockstep.NextSubmit;
        _lockstep.SnapshotBefore(at, () =>
        {
            _transport.Broadcast(Snapshot(PeerMessageType.Resync, at).Encode(LocalPeer, NetMessageKind.Snapshot));
            _resyncing = false;
        });
    }

    private void Restart(PeerMessage m)
    {
        var sim = SaveGame.Load(_catalog.Content, m.Save!);
        _lockstep.Restart(sim, m.Tick);
        foreach (var batch in m.Batches ?? []) _lockstep.Deliver(new NetMessage(NetMessageKind.Commands, batch.Tick, batch.Peer, batch.Commands, 0));
        Session.ReplaceSimulation(sim);
        _resyncing = false;
        Chat.Notice("Resynchronized with the host.");
    }

    private void OnReceived(NetMessage m)
    {
        if (m.Kind == NetMessageKind.Chat && m.Text is { } text)
        {
            Chat.Add(new ChatLine(m.Peer, NameOf(m.Peer), text));
            return;
        }
        if (PeerMessage.Decode(m) is not { } message) return;
        if (IsHost)
        {
            if (message.Type == PeerMessageType.Hello && !_seats.ContainsKey(m.Peer)) Admit(m.Peer, message);
            return;
        }
        if (m.Peer != 0) return;
        switch (message.Type)
        {
            case PeerMessageType.Departed:
                string name = NameOf(message.Peer);
                Depart(message.Peer, message.Tick);
                Chat.Notice($"{name} left the game; the AI plays for them until they return.");
                break;
            case PeerMessageType.Joined:
                _lockstep.AddPeer(message.Peer, message.Tick);
                _seats[message.Peer] = message.Seat;
                _names[message.Peer] = message.Name ?? $"Player {message.Peer}";
                Revision++;
                Chat.Notice($"{NameOf(message.Peer)} rejoined.");
                break;
            case PeerMessageType.Resync when message.Save != null:
                Restart(message);
                break;
        }
    }
}
