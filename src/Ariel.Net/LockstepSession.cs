using Ariel.Game;

namespace Ariel.Net;

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
    private readonly HashSet<int> _awaitingHandshake = new();
    private readonly Dictionary<int, Dictionary<int, string[]>> _batches = new();  // tick -> peer -> encoded commands
    private readonly Dictionary<int, Dictionary<int, ulong>> _hashes = new();      // tick -> peer -> hash
    private readonly Dictionary<int, Action> _snapshots = new();                   // tick -> taken before it runs
    private readonly List<string> _pending = new();
    private int _nextSubmit;
    private string? _handshakeError;
    private bool _announced;

    public int Tick { get; private set; }
    /// <summary>The next tick this peer will submit a batch for. No peer can have run it, so a membership change the
    /// host announces at this tick reaches everyone in time.</summary>
    public int NextSubmit => _nextSubmit;
    /// <summary>Commands applied so far, from every peer, accepted or not; it moves only when a tick had input.</summary>
    public long Applied { get; private set; }
    public Simulation Simulation => _sim;
    public event Action<int>? Desync;

    /// <summary>Start a new game: every peer is a member from tick 0, and ticks [0, delay) carry no input by agreement.</summary>
    public LockstepSession(Simulation sim, CommandCodec codec, ITransport transport, IEnumerable<int> peers, int localPeer, int inputDelay = 2)
        : this(sim, codec, transport, localPeer, inputDelay, startTick: 0)
    {
        foreach (var p in peers.Distinct().Order())
        {
            AddMember(p, 0);
            if (p != _local) _awaitingHandshake.Add(p);
        }
        if (!_peers.Contains(_local)) throw new ArgumentException("Lockstep requires the local peer among the peers");
        for (; _nextSubmit < _delay; _nextSubmit++)
            foreach (var p in _peers) Store(_nextSubmit, p, []);
    }

    private LockstepSession(Simulation sim, CommandCodec codec, ITransport transport, int localPeer, int inputDelay, int startTick)
    {
        if (inputDelay < 0) throw new ArgumentException("Lockstep requires a nonnegative input delay");
        _sim = sim; _codec = codec; _transport = transport; _local = localPeer; _delay = inputDelay;
        Tick = startTick;
        _nextSubmit = startTick;
        _transport.Received += OnReceived;
    }

    /// <summary>Join a game in progress at <paramref name="startTick"/>, from a snapshot of the state before it. The
    /// other members are already running; this peer's batches for [startTick, startTick + delay) are empty by agreement,
    /// as the host announced with <see cref="AddPeer"/>. Content was checked when the peer was admitted.</summary>
    public static LockstepSession Resume(Simulation sim, CommandCodec codec, ITransport transport, IEnumerable<int> peers, int localPeer,
        int startTick, int inputDelay = 2)
    {
        var session = new LockstepSession(sim, codec, transport, localPeer, inputDelay, startTick) { _announced = true };
        foreach (var p in peers.Distinct().Order()) session.AddMember(p, p == localPeer ? startTick : int.MinValue);
        if (!session._peers.Contains(localPeer)) throw new ArgumentException("Lockstep requires the local peer among the peers");
        for (; session._nextSubmit < startTick + inputDelay; session._nextSubmit++) session.Store(session._nextSubmit, localPeer, []);
        return session;
    }

    /// <summary>Queue a local command; it goes out with the next submitted batch.</summary>
    public void Enqueue(GameCommand cmd) => _pending.Add(_codec.Encode(cmd));

    /// <summary>Make <paramref name="peer"/> a member from <paramref name="fromTick"/>, which no member may have run;
    /// its first batches, up to the input delay, are empty by agreement.</summary>
    public void AddPeer(int peer, int fromTick)
    {
        if (fromTick < Tick) throw new InvalidOperationException($"Tick {fromTick} has already run");
        _leftAfter.Remove(peer);
        AddMember(peer, fromTick);
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
        foreach (int t in _hashes.Keys.Where(t => t < tick).ToList()) _hashes.Remove(t);
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
            + $"next submit {_nextSubmit}, last batch per peer [{string.Join(",", _lastTick.OrderBy(t => t.Key).Select(t => $"{t.Key}:{t.Value}"))}]";
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

            bool any = false;
            foreach (int p in _peers)
            {
                if (!IsMember(p, Tick)) continue;
                foreach (var text in tickBatches[p])
                {
                    _sim.TrySubmit(_codec.Decode(text), out _);
                    any = true;
                    Applied++;
                }
            }

            if (any)
            {
                var hash = _sim.ComputeHash();
                StoreHash(Tick, _local, hash);
                _transport.Broadcast(new NetMessage(NetMessageKind.Hash, Tick, _local, [], hash));
            }
            _batches.Remove(Tick);
            Tick++;
            ran++;
        }
        return ran;
    }

    public void Dispose() => _transport.Received -= OnReceived;

    private void AddMember(int peer, int fromTick)
    {
        _joinedAt[peer] = fromTick;
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
                if (!string.Equals(m.ContentHash, _sim.Content.ContentHash, StringComparison.Ordinal))
                    _handshakeError = $"Lockstep content mismatch with peer {m.Peer}: expected {_sim.Content.ContentHash}, got {m.ContentHash}";
                else _awaitingHandshake.Remove(m.Peer);
                break;
            case NetMessageKind.Commands:
                // Batches for ticks already run (a resumed peer hears some) can never be used.
                if (m.Tick < Tick) return;
                Store(m.Tick, m.Peer, m.Commands);
                if (!_lastTick.TryGetValue(m.Peer, out int last) || m.Tick > last) _lastTick[m.Peer] = m.Tick;
                break;
            case NetMessageKind.Hash:
                StoreHash(m.Tick, m.Peer, m.Hash);
                break;
        }
    }

    private void Store(int tick, int peer, string[] commands)
    {
        if (!_batches.TryGetValue(tick, out var d)) _batches[tick] = d = new();
        d[peer] = commands;
    }

    /// <summary>Compare once every member of the tick has reported. Hashes for ticks this peer never ran (before it
    /// resumed) are dropped.</summary>
    private void StoreHash(int tick, int peer, ulong hash)
    {
        if (tick < Tick && !_hashes.ContainsKey(tick) && peer != _local) return;
        if (!_hashes.TryGetValue(tick, out var d)) _hashes[tick] = d = new();
        d[peer] = hash;
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
