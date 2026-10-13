using Civ.Game;

namespace Civ.Net;

/// <summary>
/// Deterministic lockstep. Every peer submits a (possibly empty) command batch for each tick, <c>InputDelay</c> ticks ahead.
/// A tick executes only once all peers' batches for it are present; batches apply in ascending peer order.
/// After each tick every peer reports its state hash; a mismatch raises <see cref="Desync"/>.
/// </summary>
public sealed class LockstepSession
{
    private readonly Simulation _sim;
    private readonly CommandCodec _codec;
    private readonly ITransport _transport;
    private readonly int[] _peers;
    private readonly int _local;
    private readonly int _delay;

    private readonly Dictionary<int, Dictionary<int, string[]>> _batches = new();   // tick -> peer -> encoded commands
    private readonly Dictionary<int, Dictionary<int, ulong>> _hashes = new();       // tick -> peer -> hash
    private readonly List<string> _pending = new();
    private int _nextSubmit;
    private readonly HashSet<int> _handshakes = new();
    private string? _handshakeError;
    private bool _announced;

    public int Tick { get; private set; }
    public event Action<int>? Desync;

    public LockstepSession(Simulation sim, CommandCodec codec, ITransport transport, IEnumerable<int> peers, int localPeer, int inputDelay = 2)
    {
        _sim = sim; _codec = codec; _transport = transport; _local = localPeer; _delay = inputDelay;
        _peers = peers.OrderBy(p => p).ToArray();
        if (_delay < 0 || !_peers.Contains(_local) || _peers.Distinct().Count() != _peers.Length)
            throw new ArgumentException("Lockstep requires distinct peers, a local peer and a nonnegative input delay");
        _handshakes.Add(_local);
        // Ticks [0, delay) have no input by agreement, so the pipeline can start.
        for (; _nextSubmit < _delay; _nextSubmit++)
            foreach (var p in _peers) Store(_nextSubmit, p, []);
        _transport.Received += OnReceived;
    }

    /// <summary>Queue a local command; it goes out with the next submitted batch.</summary>
    public void Enqueue(GameCommand cmd) => _pending.Add(_codec.Encode(cmd));

    /// <summary>Submit local input and run every tick that is complete. Call as often as you like; returns how many ticks ran.</summary>
    public int Pump()
    {
        // Repeat while waiting: endpoints may not have subscribed when a peer first announced itself.
        if (!_announced || _handshakes.Count < _peers.Length)
        {
            _transport.Broadcast(new NetMessage(NetMessageKind.Handshake, 0, _local, [], 0, _sim.Content.ContentHash));
            _announced = true;
        }
        if (_handshakeError != null) throw new InvalidDataException(_handshakeError);
        if (_handshakes.Count < _peers.Length) return 0;
        int ran = 0;
        while (true)
        {
            while (_nextSubmit <= Tick + _delay)
            {
                var batch = _pending.ToArray();
                _pending.Clear();
                Store(_nextSubmit, _local, batch);
                _transport.Broadcast(new NetMessage(NetMessageKind.Commands, _nextSubmit, _local, batch, 0));
                _nextSubmit++;
            }

            if (!_batches.TryGetValue(Tick, out var tickBatches) || _peers.Any(p => !tickBatches.ContainsKey(p)))
                return ran;

            foreach (var p in _peers)
                foreach (var text in tickBatches[p])
                    _sim.TryApply(_codec.Decode(text), out _);

            var hash = _sim.ComputeHash();
            StoreHash(Tick, _local, hash);
            _transport.Broadcast(new NetMessage(NetMessageKind.Hash, Tick, _local, [], hash));
            _batches.Remove(Tick);
            Tick++;
            ran++;
        }
    }

    private void OnReceived(NetMessage m)
    {
        if (!_peers.Contains(m.Peer)) throw new InvalidDataException($"Unknown lockstep peer {m.Peer}");
        if (m.Kind == NetMessageKind.Handshake)
        {
            if (!string.Equals(m.ContentHash, _sim.Content.ContentHash, StringComparison.Ordinal))
                _handshakeError = $"Lockstep content mismatch with peer {m.Peer}: expected {_sim.Content.ContentHash}, got {m.ContentHash}";
            else _handshakes.Add(m.Peer);
        }
        else if (m.Kind == NetMessageKind.Commands) Store(m.Tick, m.Peer, m.Commands);
        else StoreHash(m.Tick, m.Peer, m.Hash);
    }

    private void Store(int tick, int peer, string[] commands)
    {
        if (!_batches.TryGetValue(tick, out var d)) _batches[tick] = d = new();
        d[peer] = commands;
    }

    private void StoreHash(int tick, int peer, ulong hash)
    {
        if (!_hashes.TryGetValue(tick, out var d)) _hashes[tick] = d = new();
        d[peer] = hash;
        if (_peers.All(p => d.ContainsKey(p)))
        {
            if (d.Values.Distinct().Count() > 1) Desync?.Invoke(tick);
            _hashes.Remove(tick);
        }
    }
}
