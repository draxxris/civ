using Ariel.Net;

namespace Ariel.Client;

/// <summary>
/// Plays one side of a multiplayer game with nobody at the keyboard, for two-process smoke checks on one machine:
/// hosts or joins on localhost, takes a seat, starts once both humans sit, then ends turns until a target turn.
/// Run one process with <c>--mp host</c> and another with <c>--mp join</c>.
/// </summary>
internal sealed class DebugMultiplayer(GameCatalog catalog, DebugOptions options)
{
    private Lobby? _lobby;
    private Task<TcpTransport>? _joining;
    private int _attempts;
    private bool _handedOver;

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

    /// <summary>End the local player's turn whenever it can, until the target turn. True once there.</summary>
    public bool Drive(GameSession session)
    {
        if (session.ConnectionProblem is { } problem) throw new InvalidOperationException(problem);
        if (session.Sim.State.Turn > options.Turns) return true;
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
            _joining = TcpTransport.JoinAsync("localhost", options.MultiplayerPort, TimeSpan.FromSeconds(5));
            return;
        }
        if (!_joining.IsCompleted) return;
        if (_joining.IsCompletedSuccessfully) _lobby = Lobby.Join(catalog, _joining.Result, "Guest");
        else if (++_attempts > 300) throw new InvalidOperationException("Could not reach the host: " + _joining.Exception?.GetBaseException().Message);
        _joining = null;
    }
}
