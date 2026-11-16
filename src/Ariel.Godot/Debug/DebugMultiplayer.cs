using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// Plays one side of a multiplayer game with nobody at the keyboard, for two-process smoke checks on one machine:
/// hosts or joins on localhost, takes a seat, starts once both humans sit, then ends turns until a target turn.
/// Run one process with <c>--mp host</c> and another with <c>--mp join</c>.
/// <para>Each side takes its state hash the first time it reaches the target turn, says it in chat, and finishes only
/// once it has heard the other side's report. A peer that left any earlier would change the game for the one still
/// running: the host hands a departed seat to the AI, and a guest stops when the host goes.</para>
/// </summary>
internal sealed class DebugMultiplayer(GameCatalog catalog, DebugOptions options)
{
    private Lobby? _lobby;
    private Task<TcpTransport>? _joining;
    private int _attempts;
    private bool _handedOver;

    /// <summary>The chat line that reports reaching the target turn, so the other side can recognize it.</summary>
    private const string ReportPrefix = "multiplayer smoke: ";

    /// <summary>This side's report, set once it reaches the target turn.</summary>
    public string? Report { get; private set; }

    private bool IsHost => options.Multiplayer == "host";

    /// <summary>Advance the lobby. Returns it once the game is starting, exactly once.</summary>
    public Lobby? Tick()
    {
        if (_handedOver) return null;
        if (_lobby == null) Connect();
        if (_lobby == null) return null;
        _lobby.Poll();
        if (_lobby.Closed is { } closed) throw new InvalidOperationException("Lobby closed: " + closed);
        if (!IsHost && _lobby.State.Seats.Count > 1 && _lobby.State.SeatOf(_lobby.LocalPeer) == null) _lobby.TakeSeat(1);
        if (IsHost && _lobby.Starting == null && _lobby.State.StartProblem() == null && _lobby.Start() is { } problem)
            throw new InvalidOperationException(problem);
        if (_lobby.Starting == null) return null;
        _handedOver = true;
        return _lobby;
    }

    /// <summary>End the local player's turn whenever it can, until the target turn; then report it and wait for the
    /// other side's report. True once both sides have reported.</summary>
    public bool Drive(MultiplayerGame game)
    {
        var session = game.Session;
        if (Report == null && session.Sim.State.Turn > options.Turns)
        {
            Report = $"{ReportPrefix}turn {session.Sim.State.Turn}, hash {session.Sim.ComputeHash():x16}";
            game.Say(Report);
        }
        // Once this side has its hash, the other side leaving cannot change it: it reported first or never will.
        bool otherGone = session.ConnectionProblem != null || !game.Players().Any(p => p.Peer != game.LocalPeer);
        if (Report != null)
            return otherGone || game.Chat.Lines.Any(l => l.Peer >= 0 && l.Peer != game.LocalPeer && l.Text.StartsWith(ReportPrefix, StringComparison.Ordinal));
        if (otherGone) throw new InvalidOperationException(session.ConnectionProblem ?? "The other player left before the target turn.");
        if (session.IsMyMove && !session.Me.EndedTurn) session.EndTurn();
        return false;
    }

    private void Connect()
    {
        if (IsHost)
        {
            var setup = LobbySetup.Default(catalog) with { Seed = options.Seed };
            if (options.World.Length > 0)
                setup = setup with { WorldSize = catalog.WorldSizes.First(w => w.Id == options.World || w.Name.Equals(options.World, StringComparison.OrdinalIgnoreCase)).Id };
            _lobby = Lobby.Host(catalog, TcpTransport.Host(options.MultiplayerPort), "Host", setup, seats: 2);
            for (int i = 0; i < options.Opponents; i++) _lobby.AddSeat(SeatKind.Ai);
            return;
        }
        if (_joining == null)
        {
            _joining = TcpTransport.JoinAsync("localhost", options.MultiplayerPort, connectTimeout: TimeSpan.FromSeconds(5));
            return;
        }
        if (!_joining.IsCompleted) return;
        if (_joining.IsCompletedSuccessfully) _lobby = Lobby.Join(catalog, _joining.Result, "Guest");
        else if (++_attempts > 300) throw new InvalidOperationException("Could not reach the host: " + _joining.Exception?.GetBaseException().Message);
        _joining = null;
    }
}
