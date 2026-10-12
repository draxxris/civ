namespace Civ.Net;

public enum NetMessageKind { Commands, Hash }

/// <summary>Commands: a peer's encoded commands for a lockstep tick. Hash: that peer's state hash after executing the tick.</summary>
public sealed record NetMessage(NetMessageKind Kind, int Tick, int Peer, string[] Commands, ulong Hash);

/// <summary>Reliable, ordered broadcast to all other peers. Implementations: in-process (tests), TCP/relay (todo).</summary>
public interface ITransport
{
    void Broadcast(NetMessage message);
    event Action<NetMessage>? Received;
}
