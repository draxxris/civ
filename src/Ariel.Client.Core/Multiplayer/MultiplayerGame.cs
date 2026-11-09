using Ariel.Game;
using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// A started networked game: the simulation every peer built from the same settings, its lockstep session, the local
/// player's <see cref="GameSession"/>, and the chat and peer names carried over from the lobby. The session's pump drives
/// everything, so the front end treats it like any other game.
/// </summary>
public sealed class MultiplayerGame : IDisposable
{
    private readonly ITransport _transport;
    private readonly LockstepSession _lockstep;
    private readonly LockstepChannel _channel;
    private readonly IReadOnlyDictionary<int, string> _names;
    private readonly int[] _peers;

    public GameSession Session { get; }
    public ChatLog Chat { get; }
    public int LocalPeer => _transport.LocalPeer;

    private MultiplayerGame(ITransport transport, GameStart start, ChatLog chat, Simulation sim)
    {
        _transport = transport;
        _names = start.Names;
        _peers = start.Peers;
        Chat = chat;
        _lockstep = new LockstepSession(sim, new CommandCodec(sim.Content), transport, start.Peers, transport.LocalPeer);
        _channel = new LockstepChannel(_lockstep);
        Session = new GameSession(sim, start.LocalSeat, _channel);
        transport.Received += OnReceived;
        transport.PeerLeft += OnPeerLeft;
    }

    /// <summary>Build the agreed world and join lockstep. Generating the world takes a while, so call it off the game
    /// thread; input from faster peers waits in the transport until then.</summary>
    public static MultiplayerGame Create(GameCatalog catalog, ITransport transport, GameStart start, ChatLog chat) =>
        new(transport, start, chat, new Simulation(catalog.Content, start.Settings));

    public string NameOf(int peer) => _names.TryGetValue(peer, out var name) ? name : $"Player {peer}";

    public void Say(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        _transport.Broadcast(NetMessage.OfText(NetMessageKind.Chat, LocalPeer, text));
        Chat.Add(new ChatLine(LocalPeer, NameOf(LocalPeer), text));
    }

    public void Dispose()
    {
        _transport.Received -= OnReceived;
        _transport.PeerLeft -= OnPeerLeft;
        _lockstep.Dispose();
        _transport.Dispose();
    }

    private void OnReceived(NetMessage m)
    {
        if (m.Kind == NetMessageKind.Chat && m.Text is { } text) Chat.Add(new ChatLine(m.Peer, NameOf(m.Peer), text));
    }

    private void OnPeerLeft(int peer)
    {
        if (Array.IndexOf(_peers, peer) < 0) return; // turned away in the lobby, never part of the game
        Chat.Notice($"{NameOf(peer)} left the game.");
        _channel.Fail(peer == 0 ? "The host left the game." : $"{NameOf(peer)} left the game.");
    }
}
