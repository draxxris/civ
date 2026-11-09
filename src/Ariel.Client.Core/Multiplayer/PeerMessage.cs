using System.Text.Json;
using Ariel.Net;

namespace Ariel.Client;

/// <summary>What a session message asks or announces. The lobby uses the first group; a running game uses the rest.</summary>
internal enum PeerMessageType
{
    Hello, Take, Leave, Leader, State, Start, Refuse,
    /// <summary>To a peer joining a game in progress: the snapshot, the tick it starts at, and its seat.</summary>
    Resume,
    /// <summary>A peer left; it is a member up to <see cref="PeerMessage.Tick"/>.</summary>
    Departed,
    /// <summary>A peer joins at <see cref="PeerMessage.Tick"/> in <see cref="PeerMessage.Seat"/>.</summary>
    Joined,
    /// <summary>After a desync: the snapshot every peer restarts from at <see cref="PeerMessage.Tick"/>.</summary>
    Resync,
}

/// <summary>A lockstep batch carried inside a snapshot, keeping its original sender.</summary>
internal sealed record CarriedBatch(int Tick, int Peer, string[] Commands);

/// <summary>One lobby or session-control message, sent as JSON in a <see cref="NetMessageKind.Lobby"/>,
/// <see cref="NetMessageKind.Control"/> or (when it carries a save) <see cref="NetMessageKind.Snapshot"/> message. Only
/// the fields its type needs are set.</summary>
internal sealed record PeerMessage(PeerMessageType Type)
{
    public string? Name { get; init; }
    public string? ContentHash { get; init; }
    public int Seat { get; init; }
    public int Peer { get; init; }
    public int Tick { get; init; }
    public string? Leader { get; init; }
    public string? Civilization { get; init; }
    public LobbyState? State { get; init; }
    public string? Settings { get; init; }
    public string? Save { get; init; }
    public string? Reason { get; init; }
    public int[]? Peers { get; init; }
    public Dictionary<int, int>? Seats { get; init; }
    public Dictionary<int, string>? Names { get; init; }
    public List<CarriedBatch>? Batches { get; init; }
    public int TurnTimerSeconds { get; init; }

    public NetMessage Encode(int from, NetMessageKind kind = NetMessageKind.Lobby) =>
        NetMessage.OfText(kind, from, JsonSerializer.Serialize(this));

    public static PeerMessage? Decode(NetMessage m)
    {
        if (m.Kind is not (NetMessageKind.Lobby or NetMessageKind.Control or NetMessageKind.Snapshot) || m.Text is null) return null;
        try { return JsonSerializer.Deserialize<PeerMessage>(m.Text); }
        catch (JsonException) { return null; }
    }

    /// <summary>Whether a message is lockstep input, which must reach the game even when it arrives early.</summary>
    public static bool IsLockstep(NetMessage m) => m.Kind is NetMessageKind.Handshake or NetMessageKind.Commands or NetMessageKind.Hash;
}
