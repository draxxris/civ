namespace Ariel.Net;

/// <summary>
/// Reliable, ordered delivery between the peers of one game. Peer 0 is the host. Messages from one sender arrive in the
/// order sent. Nothing is delivered behind the caller's back: received messages and peer changes queue until
/// <see cref="Poll"/>, which raises the events on the calling thread, so game code needs no locks.
/// Implementations: <see cref="InProcessHub"/> (tests, local), <see cref="TcpTransport"/> (LAN and direct IP).
/// </summary>
public interface ITransport : IDisposable
{
    /// <summary>This peer's id.</summary>
    int LocalPeer { get; }
    /// <summary>Send to every other peer.</summary>
    void Broadcast(NetMessage message);
    /// <summary>Send to one peer.</summary>
    void SendTo(int peer, NetMessage message);
    /// <summary>Raise the events for everything that arrived since the last call.</summary>
    void Poll();
    /// <summary>A message from another peer, raised by <see cref="Poll"/>.</summary>
    event Action<NetMessage>? Received;
    /// <summary>A peer connected, raised by <see cref="Poll"/>.</summary>
    event Action<int>? PeerJoined;
    /// <summary>A peer disconnected or stopped answering, raised by <see cref="Poll"/>. On a client, the host leaving
    /// (peer 0) means the game is over.</summary>
    event Action<int>? PeerLeft;
}
