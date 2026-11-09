namespace Ariel.Net;

/// <summary>Connects any number of transports in memory, in creation order from peer 0. Delivery is ordered and waits
/// for <see cref="ITransport.Poll"/>, as on a real network; used by tests and local play.</summary>
public sealed class InProcessHub
{
    private readonly List<Endpoint> _endpoints = new();

    public ITransport CreateEndpoint()
    {
        var e = new Endpoint(this, _endpoints.Count);
        foreach (var other in _endpoints) { other.Enqueue(() => other.RaiseJoined(e.LocalPeer)); e.Enqueue(() => e.RaiseJoined(other.LocalPeer)); }
        _endpoints.Add(e);
        return e;
    }

    private sealed class Endpoint(InProcessHub hub, int peer) : ITransport
    {
        private readonly Queue<Action> _inbox = new();
        private bool _closed;

        public int LocalPeer => peer;
        public event Action<NetMessage>? Received;
        public event Action<int>? PeerJoined;
        public event Action<int>? PeerLeft;

        public void Enqueue(Action a) { if (!_closed) _inbox.Enqueue(a); }
        public void RaiseJoined(int p) => PeerJoined?.Invoke(p);

        public void Broadcast(NetMessage m)
        {
            foreach (var other in hub._endpoints)
                if (!ReferenceEquals(other, this)) other.Enqueue(() => other.Received?.Invoke(m));
        }

        public void SendTo(int to, NetMessage m)
        {
            var other = hub._endpoints.FirstOrDefault(e => e.LocalPeer == to && !e._closed);
            other?.Enqueue(() => other.Received?.Invoke(m with { To = to }));
        }

        public void Poll()
        {
            while (_inbox.Count > 0) _inbox.Dequeue()();
        }

        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            _inbox.Clear();
            foreach (var other in hub._endpoints)
                if (!ReferenceEquals(other, this)) other.Enqueue(() => other.PeerLeft?.Invoke(peer));
        }
    }
}
