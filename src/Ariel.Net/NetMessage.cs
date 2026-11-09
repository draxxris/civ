namespace Ariel.Net;

/// <summary>What a message is for. Lockstep uses Handshake, Commands and Hash; the lobby, chat and session control use
/// the rest, with their data in <see cref="NetMessage.Text"/>.</summary>
public enum NetMessageKind
{
    /// <summary>Lockstep: the sender's frozen content hash, before any tick runs.</summary>
    Handshake,
    /// <summary>Lockstep: the sender's command batch for <see cref="NetMessage.Tick"/>.</summary>
    Commands,
    /// <summary>Lockstep: the sender's state hash after <see cref="NetMessage.Tick"/>.</summary>
    Hash,
    /// <summary>Lobby state or a lobby request, as JSON.</summary>
    Lobby,
    /// <summary>A chat line.</summary>
    Chat,
    /// <summary>A whole saved game, for starting, resuming or rejoining.</summary>
    Snapshot,
    /// <summary>Session control: pause, resume, a seat handed to the AI, a desync report.</summary>
    Control,
}

/// <summary>One message between peers. <see cref="To"/> is a peer id, or <see cref="NetMessage.Everyone"/> for a
/// broadcast. Plain data, serialized as JSON on the wire.</summary>
public sealed record NetMessage(NetMessageKind Kind, int Tick, int Peer, string[] Commands, ulong Hash, string? ContentHash = null)
{
    /// <summary>Recipient for a broadcast.</summary>
    public const int Everyone = -1;

    /// <summary>Recipient peer id, or <see cref="Everyone"/>.</summary>
    public int To { get; init; } = Everyone;
    /// <summary>Payload of lobby, chat, snapshot and control messages.</summary>
    public string? Text { get; init; }

    /// <summary>A message that carries only text.</summary>
    public static NetMessage OfText(NetMessageKind kind, int peer, string text, int to = Everyone) =>
        new(kind, 0, peer, [], 0) { Text = text, To = to };
}
