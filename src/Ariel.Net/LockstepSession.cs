using Ariel.Game;

namespace Ariel.Net;

/// <summary>Whether <paramref name="peer"/>, playing <paramref name="seat"/> (null when it plays none) at the tick a
/// command applies, may issue that command. It must depend only on its arguments, so every peer decides alike.</summary>
public delegate bool CommandAuthority(int peer, int? seat, GameCommand command);

/// <summary>
/// Deterministic lockstep. Every peer submits a (possibly empty) command batch for each tick, <c>InputDelay</c> ticks ahead.
/// A tick executes only once every member's batch for it is present; batches apply in ascending peer order, each command
/// through <see cref="Simulation.TrySubmit"/>, so a command out of its player's turn is refused on every peer alike.
/// After each tick that applied commands every peer reports its state hash; a mismatch raises <see cref="Desync"/>.
/// Empty ticks cannot change the state, so they are not hashed: hashing serializes the whole game.
/// <para>Membership can change at agreed ticks, which the host chooses and announces: a peer that left is a member up
/// to the last tick it sent (<see cref="RemovePeer"/>), and a peer that joins is a member from a tick nobody has run
/// yet (<see cref="AddPeer"/>), starting from a snapshot of the state before that tick (<see cref="Resume"/>,
/// <see cref="SnapshotBefore"/>). A desynchronized peer restarts from such a snapshot (<see cref="Restart"/>).</para>
/// </summary>
public sealed class LockstepSession : IDisposable
{
    private Simulation _sim;
    private readonly CommandCodec _codec;
    private readonly ITransport _transport;
    private readonly int _local;
    private readonly int _delay;

    private readonly List<int> _peers = new();                                     // every member ever, ascending
    private readonly Dictionary<int, int> _joinedAt = new();                       // peer -> first tick it is a member
    private readonly Dictionary<int, int> _leftAfter = new();                      // peer -> last tick it is a member
    private readonly Dictionary<int, int> _lastTick = new();                       // peer -> highest tick of a batch received
    private readonly Dictionary<int, List<(int From, int Seat)>> _seats = new();   // peer -> the player it plays, from a tick
    private readonly CommandAuthority? _authority;
    private readonly HashSet<int> _awaitingHandshake = new();
    private readonly Dictionary<int, Dictionary<int, string[]>> _batches = new();  // tick -> peer -> encoded commands
    private readonly Dictionary<int, Dictionary<int, ulong>> _hashes = new();      // tick -> peer -> hash
    private readonly Dictionary<int, Action> _snapshots = new();                   // tick -> taken before it runs
    private readonly List<string> _pending = new();
    private int _nextSubmit;
    private string? _handshakeError;
    private bool _announced;

    /// <summary>How far past the current tick a peer's batch or hash may be. A peer runs a tick only once it holds
    /// everyone's batch, so no honest peer gets more than twice the input delay ahead; the rest is slack for a peer
    /// resuming or restarting. Anything further out could only be stored forever, or keep its sender a member of
    /// ticks nobody will reach.</summary>
    private int MaxLead => 2 * _delay + 64;

    public int Tick { get; private set; }
    /// <summary>The next tick this peer will submit a batch for. No peer can have run it, so a membership change the
    /// host announces at this tick reaches everyone in time.</summary>
    public int NextSubmit => _nextSubmit;
    /// <summary>Commands applied so far, from every peer, accepted or not; it moves only when a tick had input.</summary>
    public long Applied { get; private set; }
    public Simulation Simulation => _sim;
    public event Action<int>? Desync;
    /// <summary>The latest input a peer sent that broke the protocol and was ignored, such as a second, different
    /// batch for a tick it already sent. Diagnostics only.</summary>
    public string? LastFault { get; private set; }

    /// <summary>Start a new game: every peer is a member from tick 0, and ticks [0, delay) carry no input by agreement.</summary>
    /// <param name="seats">The player each peer plays, for <paramref name="authority"/>.</param>
    /// <param name="authority">Which commands a peer may issue; without it every member may issue any command.</param>
    public LockstepSession(Simulation sim, CommandCodec codec, ITransport transport, IEnumerable<int> peers, int localPeer, int inputDelay = 2,
        IReadOnlyDictionary<int, int>? seats = null, CommandAuthority? authority = null)
        : this(sim, codec, transport, localPeer, inputDelay, startTick: 0, authority)
    {
        foreach (var p in peers.Distinct().Order())
        {
            AddMember(p, 0, seats?.TryGetValue(p, out int seat) == true ? seat : null);
            if (p != _local) _awaitingHandshake.Add(p);
        }
        if (!_peers.Contains(_local)) throw new ArgumentException("Lockstep requires the local peer among the peers");
        for (; _nextSubmit < _delay; _nextSubmit++)
            foreach (var p in _peers) Store(_nextSubmit, p, []);
    }

    private LockstepSession(Simulation sim, CommandCodec codec, ITransport transport, int localPeer, int inputDelay, int startTick,
        CommandAuthority? authority)
    {
        if (inputDelay < 0) throw new ArgumentException("Lockstep requires a nonnegative input delay");
        _sim = sim; _codec = codec; _transport = transport; _local = localPeer; _delay = inputDelay; _authority = authority;
        Tick = startTick;
        _nextSubmit = startTick;
        _transport.Received += OnReceived;
    }

    /// <summary>Join a game in progress at <paramref name="startTick"/>, from a snapshot of the state before it. The
    /// other members are already running; this peer's batches for [startTick, startTick + delay) are empty by agreement,
    /// as the host announced with <see cref="AddPeer"/>. Content was checked when the peer was admitted.</summary>
    public static LockstepSession Resume(Simulation sim, CommandCodec codec, ITransport transport, IEnumerable<int> peers, int localPeer,
        int startTick, int inputDelay = 2, IReadOnlyDictionary<int, int>? seats = null, CommandAuthority? authority = null)
    {
        var session = new LockstepSession(sim, codec, transport, localPeer, inputDelay, startTick, authority) { _announced = true };
        foreach (var p in peers.Distinct().Order())
            session.AddMember(p, p == localPeer ? startTick : int.MinValue, seats?.TryGetValue(p, out int seat) == true ? seat : null);
        if (!session._peers.Contains(localPeer)) throw new ArgumentException("Lockstep requires the local peer among the peers");
        for (; session._nextSubmit < startTick + inputDelay; session._nextSubmit++) session.Store(session._nextSubmit, localPeer, []);
        return session;
    }

    /// <summary>Queue a local command; it goes out with the next submitted batch.</summary>
    public void Enqueue(GameCommand cmd) => _pending.Add(_codec.Encode(cmd));

    /// <summary>Make <paramref name="peer"/> a member from <paramref name="fromTick"/>, which no member may have run,
    /// playing <paramref name="seat"/>; its first batches, up to the input delay, are empty by agreement.</summary>
    public void AddPeer(int peer, int fromTick, int? seat = null)
    {
        if (fromTick < Tick) throw new InvalidOperationException($"Tick {fromTick} has already run");
        _leftAfter.Remove(peer);
        AddMember(peer, fromTick, seat);
        for (int t = fromTick; t < fromTick + _delay; t++) Store(t, peer, []);
    }

    /// <summary>End <paramref name="peer"/>'s membership after <paramref name="lastTick"/>, the last tick whose batch the
    /// host received from it. Every member holds those batches already, relayed before the announcement.</summary>
    public void RemovePeer(int peer, int lastTick)
    {
        _leftAfter[peer] = lastTick;
        _awaitingHandshake.Remove(peer);
    }

    /// <summary>The highest tick of a batch received from <paramref name="peer"/>, or one before the tick it joined.</summary>
    public int LastBatchTick(int peer) =>
        _lastTick.TryGetValue(peer, out int t) ? t : _joinedAt.TryGetValue(peer, out int j) ? j - 1 : -1;

    /// <summary>Run <paramref name="take"/> when every tick before <paramref name="tick"/> has run and that tick has not,
    /// so it sees exactly the state a peer resuming at that tick must start from.</summary>
    public void SnapshotBefore(int tick, Action take)
    {
        if (tick < Tick) throw new InvalidOperationException($"Tick {tick} has already run");
        _snapshots[tick] = take;
    }

    /// <summary>Replace the simulation with a snapshot of the state before <paramref name="tick"/>, after a desync.
    /// Batches already received for that tick and later stay, so the game carries on from there.</summary>
    public void Restart(Simulation sim, int tick)
    {
        _sim = sim;
        Tick = tick;
        _nextSubmit = Math.Max(_nextSubmit, tick);
        foreach (int t in _batches.Keys.Where(t => t < tick).ToList()) _batches.Remove(t);
        // Every tick from here runs again from the snapshot, so hashes reported before the restart describe states
        // nobody will reach; a hash is written once, so they must go rather than be overwritten later.
        _hashes.Clear();
    }

    /// <summary>Every batch held for <paramref name="fromTick"/> and later, for a peer resuming at that tick.</summary>
    public IEnumerable<(int Tick, int Peer, string[] Commands)> BatchesFrom(int fromTick)
    {
        foreach (var (tick, byPeer) in _batches.OrderBy(b => b.Key))
            if (tick >= fromTick)
                foreach (var (peer, commands) in byPeer.OrderBy(b => b.Key)) yield return (tick, peer, commands);
    }

    /// <summary>What the next tick waits for, for diagnostics.</summary>
    public string Describe()
    {
        var held = _batches.TryGetValue(Tick, out var d) ? d.Keys.Order().ToArray() : [];
        var members = _peers.Where(p => IsMember(p, Tick)).ToArray();
        return $"tick {Tick}: members [{string.Join(",", members)}], held [{string.Join(",", held)}], "
            + $"next submit {_nextSubmit}, last batch per peer [{string.Join(",", _lastTick.OrderBy(t => t.Key).Select(t => $"{t.Key}:{t.Value}"))}]"
            + (LastFault is { } fault ? $", last fault: {fault}" : "");
    }

    /// <summary>Hand over a message received before this session existed.</summary>
    public void Deliver(NetMessage message) => OnReceived(message);

    /// <summary>Receive, submit local input and run every complete tick, up to <paramref name="maxTicks"/>. Call as
    /// often as you like; returns how many ticks ran.</summary>
    public int Pump(int maxTicks = int.MaxValue)
    {
        _transport.Poll();
        // Repeat while waiting: endpoints may not have subscribed when a peer first announced itself.
        if (!_announced || _awaitingHandshake.Count > 0)
        {
            _transport.Broadcast(new NetMessage(NetMessageKind.Handshake, 0, _local, [], 0, _sim.Content.ContentHash));
            _announced = true;
        }
        if (_handshakeError != null) throw new InvalidDataException(_handshakeError);
        if (_awaitingHandshake.Count > 0) return 0;
        int ran = 0;
        while (ran < maxTicks)
        {
            while (_nextSubmit <= Tick + _delay)
            {
                string[] batch = _pending.Count == 0 ? [] : _pending.ToArray();
                _pending.Clear();
                Store(_nextSubmit, _local, batch);
                _transport.Broadcast(new NetMessage(NetMessageKind.Commands, _nextSubmit, _local, batch, 0));
                _nextSubmit++;
            }
            if (_snapshots.Remove(Tick, out var take)) take();

            if (!_batches.TryGetValue(Tick, out var tickBatches)) return ran;
            foreach (int p in _peers)
                if (IsMember(p, Tick) && !tickBatches.ContainsKey(p)) return ran;

            // The tick is consumed before anything applies: if a command throws, a later pump must never apply the
            // commands before it a second time.
            int tick = Tick;
            _batches.Remove(tick);
            Tick++;
            ran++;
            bool any = false;
            foreach (int p in _peers)
            {
                if (!IsMember(p, tick)) continue;
                foreach (var text in tickBatches[p])
                {
                    ApplyEncoded(p, tick, text);
                    any = true;
                    Applied++;
                }
            }

            if (any)
            {
                var hash = _sim.ComputeHash();
                StoreHash(tick, _local, hash);
                _transport.Broadcast(new NetMessage(NetMessageKind.Hash, tick, _local, [], hash));
            }
        }
        return ran;
    }

    public void Dispose() => _transport.Received -= OnReceived;

    /// <summary>The player <paramref name="peer"/> played at <paramref name="tick"/>, if any.</summary>
    private int? SeatOf(int peer, int tick)
    {
        int? seat = null;
        if (_seats.TryGetValue(peer, out var history))
            foreach (var (from, s) in history)
                if (from <= tick) seat = s;
        return seat;
    }

    /// <summary>Apply one received command. One that cannot be decoded, that its peer may not issue, or whose
    /// validation fails with an exception instead of an error, is refused: every peer holds the same bytes, so every peer refuses it alike.</summary>
    private void ApplyEncoded(int peer, int tick, string text)
    {
        GameCommand command;
        try
        {
            command = _codec.Decode(text);
            if (_authority != null && !_authority(peer, SeatOf(peer, tick), command)) return;
            command.Validate(_sim);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or KeyNotFoundException
            or ArgumentException or InvalidOperationException or NullReferenceException or NotSupportedException
            or FormatException or InvalidCastException)
        {
            return;
        }
        _sim.TrySubmit(command, out _);
    }

    private void AddMember(int peer, int fromTick, int? seat)
    {
        _joinedAt[peer] = fromTick;
        // Kept per membership, so batches from an earlier membership still apply under the seat they were sent from.
        if (seat is int s)
        {
            if (!_seats.TryGetValue(peer, out var history)) _seats[peer] = history = new();
            history.Add((fromTick, s));
        }
        int i = _peers.BinarySearch(peer);
        if (i < 0) _peers.Insert(~i, peer);
    }

    private bool IsMember(int peer, int tick) =>
        _joinedAt.TryGetValue(peer, out int from) && from <= tick && (!_leftAfter.TryGetValue(peer, out int last) || tick <= last);

    private void OnReceived(NetMessage m)
    {
        switch (m.Kind)
        {
            case NetMessageKind.Handshake:
                // Only a peer this session still waits on can be checked; a repeat, or a handshake from a peer that
                // was never waited on (one the host admitted mid-game after checking its content), cannot stop it.
                if (!_awaitingHandshake.Contains(m.Peer)) return;
                if (!string.Equals(m.ContentHash, _sim.Content.ContentHash, StringComparison.Ordinal))
                    _handshakeError = $"Lockstep content mismatch with peer {m.Peer}: expected {_sim.Content.ContentHash}, got {m.ContentHash}";
                else _awaitingHandshake.Remove(m.Peer);
                break;
            case NetMessageKind.Commands:
                // Batches for ticks already run (a resumed peer hears some) can never be used. This peer's own batches
                // come only from Pump: a relayed copy is the same batch, and anything else is not ours to accept.
                if (m.Tick < Tick || m.Peer == _local || !Accepts(m.Peer, m.Tick)) return;
                if (!Store(m.Tick, m.Peer, m.Commands ?? [])) return;
                if (!_lastTick.TryGetValue(m.Peer, out int last) || m.Tick > last) _lastTick[m.Peer] = m.Tick;
                break;
            case NetMessageKind.Hash:
                if (m.Peer == _local || !Accepts(m.Peer, m.Tick)) return;
                StoreHash(m.Tick, m.Peer, m.Hash);
                break;
        }
    }

    /// <summary>Whether input from <paramref name="peer"/> for <paramref name="tick"/> can be used: the tick is within
    /// reach, and the peer is not known to be outside its membership then, before it joined or after it left. A peer
    /// not yet announced is accepted, since its batches can arrive before the announcement does.</summary>
    private bool Accepts(int peer, int tick)
    {
        if (tick > Tick + MaxLead) { LastFault = $"peer {peer} sent tick {tick}, beyond {Tick + MaxLead}"; return false; }
        return !_joinedAt.ContainsKey(peer) || IsMember(peer, tick);
    }

    /// <summary>Hold a peer's batch for a tick. Each batch is written once: a repeat of the same batch changes nothing,
    /// and a different one is ignored and recorded as a fault, so a tick never applies one copy on some peers and
    /// another on the rest. Returns whether the batch is now held as given.</summary>
    private bool Store(int tick, int peer, string[] commands)
    {
        if (!_batches.TryGetValue(tick, out var d)) _batches[tick] = d = new();
        if (d.TryGetValue(peer, out var held))
        {
            if (held.AsSpan().SequenceEqual(commands)) return true;
            LastFault = $"peer {peer} sent a second, different batch for tick {tick}";
            return false;
        }
        d[peer] = commands;
        return true;
    }

    /// <summary>Compare once every member of the tick has reported. Hashes for ticks this peer never ran (before it
    /// resumed) are dropped.</summary>
    private void StoreHash(int tick, int peer, ulong hash)
    {
        if (tick < Tick && !_hashes.ContainsKey(tick) && peer != _local) return;
        if (!_hashes.TryGetValue(tick, out var d)) _hashes[tick] = d = new();
        // One hash per peer and tick: a later report cannot replace the first, so a peer cannot raise a desync by
        // revising what it said.
        if (!d.TryAdd(peer, hash)) return;
        ulong? first = null;
        bool mismatch = false;
        foreach (int p in _peers)
        {
            if (!IsMember(p, tick)) continue;
            if (!d.TryGetValue(p, out ulong h)) return;
            if (first is null) first = h;
            else if (h != first) mismatch = true;
        }
        _hashes.Remove(tick);
        if (mismatch) Desync?.Invoke(tick);
    }
}
