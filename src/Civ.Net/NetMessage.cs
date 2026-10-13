namespace Civ.Net;

public enum NetMessageKind { Handshake, Commands, Hash }

/// <summary>Handshake carries the frozen content hash. Commands carry encoded input; Hash reports state after a tick.</summary>
public sealed record NetMessage(NetMessageKind Kind, int Tick, int Peer, string[] Commands, ulong Hash, string? ContentHash = null);

/// <summary>Reliable, ordered broadcast to all other peers. Implementations: in-process (tests), TCP/relay (todo).</summary>
public interface ITransport
{
    void Broadcast(NetMessage message);
    event Action<NetMessage>? Received;
}
