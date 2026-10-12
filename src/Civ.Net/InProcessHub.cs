namespace Civ.Net;

/// <summary>Connects any number of transports in memory. Delivery is immediate and ordered; used by tests and local hot-seat.</summary>
public sealed class InProcessHub
{
    private readonly List<Endpoint> _endpoints = new();

    public ITransport CreateEndpoint()
    {
        var e = new Endpoint(this);
        _endpoints.Add(e);
        return e;
    }

    private sealed class Endpoint(InProcessHub hub) : ITransport
    {
        public event Action<NetMessage>? Received;
        public void Broadcast(NetMessage m)
        {
            foreach (var other in hub._endpoints)
                if (!ReferenceEquals(other, this)) other.Received?.Invoke(m);
        }
    }
}
