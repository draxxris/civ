using Ariel.Client;
using Ariel.Game;
using Ariel.Net;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>A running networked game keeping going while people leave, rejoin, desync, load saves or run out of time.</summary>
public sealed class MultiplayerGameTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private GameCatalog Catalog => mods.Catalog;

    private static void Settle(params Lobby[] lobbies)
    {
        for (int i = 0; i < 10; i++) foreach (var l in lobbies) l.Poll();
    }

    private static void Run(Func<bool> until, params MultiplayerGame[] games)
    {
        // Turns run on pool threads, so give them time as well as frames.
        var deadline = DateTime.UtcNow.AddSeconds(60);
        for (int i = 0; (i < 600 || DateTime.UtcNow < deadline) && !until(); i++)
        {
            foreach (var g in games) { g.Session.Pump(); g.Update(); }
            if (i >= 600 || games.Any(g => g.Session.TurnInProgress)) Thread.Sleep(1);
        }
        Assert.True(until(), "the game did not get there: " + string.Join(" | ", games.Select(g =>
            $"peer {g.LocalPeer} {g.Describe()} turn {g.Session.Sim.State.Turn} seg {g.Session.Sim.State.TurnOrder.Active}/{g.Session.Sim.State.TurnOrder.Segments.Count} " +
            $"ended [{string.Join(",", g.Session.Sim.State.Players.Select(p => $"{p.Id}:{(p.EndedTurn ? 1 : 0)}{(p.Ai == null ? "h" : "a")}"))}] " +
            $"problem {g.Session.ConnectionProblem} msg {g.Session.Message} chat [{string.Join(" / ", g.Chat.Lines.Select(l => l.Text))}]")));
    }

    /// <summary>Host and guest seated in a duel world with one AI, started, built.</summary>
    private (InProcessHub Hub, MultiplayerGame Host, MultiplayerGame Guest) Started(Func<Lobby, string?>? prepare = null, int timer = 0)
    {
        var hub = new InProcessHub();
        var setup = LobbySetup.Default(Catalog) with { WorldSize = "foundation:worldsize_duel", Seed = 4, TurnTimerSeconds = timer };
        var host = Lobby.Host(Catalog, hub.CreateEndpoint(), "Ada", setup);
        var guest = Lobby.Join(Catalog, hub.CreateEndpoint(), "Bo");
        Settle(host, guest);
        if (prepare?.Invoke(host) is { } problem) Assert.Fail(problem);
        Settle(host, guest);
        guest.TakeSeat(host.State.Seats.Select((s, i) => (s, i)).First(x => x.s.Kind == SeatKind.Human && x.s.Peer == null).i);
        Settle(host, guest);
        Assert.Null(host.Start());
        Settle(host, guest);
        var a = MultiplayerGame.Create(Catalog, host.Release(), host.Starting!, host.Chat);
        var b = MultiplayerGame.Create(Catalog, guest.Release(), guest.Starting!, guest.Chat);
        return (hub, a, b);
    }

    private static void EndTurns(params MultiplayerGame[] games)
    {
        Run(() => games.All(g => !g.Session.TurnInProgress), games); // The others may still be moving.
        int turn = games[0].Session.Sim.State.Turn;
        foreach (var g in games) g.Session.EndTurn();
        Run(() => games.All(g => g.Session.Sim.State.Turn > turn), games);
    }

    [Fact]
    public void A_player_who_leaves_is_played_by_the_AI_and_the_game_goes_on()
    {
        var (_, host, guest) = Started();
        int seat = guest.Session.LocalPlayer;

        guest.Dispose();
        Run(() => host.Session.Sim.State.PlayerById(seat).Ai != null, host);

        Assert.Null(host.Session.ConnectionProblem);
        Assert.Contains(host.Chat.Lines, l => l.Text.StartsWith("Bo left the game"));
        EndTurns(host);
        Assert.Equal(2, host.Session.Sim.State.Turn);
        host.Dispose();
    }

    [Fact]
    public void A_player_who_returns_takes_their_seat_back_in_sync()
    {
        var (hub, host, guest) = Started();
        int seat = guest.Session.LocalPlayer;
        EndTurns(host, guest);
        guest.Dispose();
        Run(() => host.Session.Sim.State.PlayerById(seat).Ai != null, host);
        EndTurns(host);

        var back = Lobby.Join(Catalog, hub.CreateEndpoint(), "Bo");
        for (int i = 0; i < 200 && back.Starting == null; i++) { back.Poll(); host.Session.Pump(); }
        Assert.NotNull(back.Starting);
        Assert.Equal(seat, back.Starting!.LocalSeat);
        using var rejoined = MultiplayerGame.Create(Catalog, back.Release(), back.Starting, back.Chat);
        Run(() => rejoined.Session.Sim.State.PlayerById(seat).Ai == null && host.Session.Sim.State.PlayerById(seat).Ai == null, host, rejoined);

        Assert.Equal(host.Session.Sim.ComputeHash(), rejoined.Session.Sim.ComputeHash());
        EndTurns(host, rejoined);
        Assert.Equal(host.Session.Sim.ComputeHash(), rejoined.Session.Sim.ComputeHash());
        Assert.Equal(host.Session.Sim.State.Turn, rejoined.Session.Sim.State.Turn);
        host.Dispose();
    }

    [Fact]
    public void A_desynchronized_peer_restarts_from_the_hosts_state()
    {
        var (_, host, guest) = Started();
        var dir = Path.Combine(Path.GetTempPath(), "ariel-desync-" + Guid.NewGuid().ToString("N"));
        guest.DesyncDirectory = dir;
        guest.Session.Me.Treasury += 77; // a bug in miniature

        EndTurns(host, guest);
        Run(() => guest.Chat.Lines.Any(l => l.Text == "Resynchronized with the host."), host, guest);
        EndTurns(host, guest);

        Assert.Equal(host.Session.Sim.ComputeHash(), guest.Session.Sim.ComputeHash());
        Assert.Null(guest.Session.ConnectionProblem);
        Assert.NotEmpty(Directory.GetFiles(dir, "desync-*.json"));
        Directory.Delete(dir, true);
        host.Dispose();
        guest.Dispose();
    }

    [Fact]
    public void A_saved_game_continues_with_its_seats_taken_by_whoever_joins()
    {
        var (_, first, firstGuest) = Started();
        EndTurns(first, firstGuest);
        string save = first.Session.SaveToJson();
        int turn = first.Session.Sim.State.Turn;
        first.Dispose();
        firstGuest.Dispose();

        var (_, host, guest) = Started(lobby => lobby.LoadSave(save));

        Assert.Equal(turn, host.Session.Sim.State.Turn);
        Assert.Equal(host.Session.Sim.ComputeHash(), guest.Session.Sim.ComputeHash());
        EndTurns(host, guest);
        Assert.Equal(turn + 1, guest.Session.Sim.State.Turn);
        Assert.Equal(host.Session.Sim.ComputeHash(), guest.Session.Sim.ComputeHash());
        host.Dispose();
        guest.Dispose();
    }

    [Fact]
    public void The_turn_timer_ends_the_turn_of_whoever_is_still_moving()
    {
        var (_, host, guest) = Started(timer: 1);
        host.Session.EndTurn();
        Run(() => host.TurnSecondsLeft != null, host, guest);

        Thread.Sleep(1100);
        Run(() => host.Session.Sim.State.Turn == 2 && guest.Session.Sim.State.Turn == 2, host, guest);

        Assert.Equal(host.Session.Sim.ComputeHash(), guest.Session.Sim.ComputeHash());
        host.Dispose();
        guest.Dispose();
    }

    [Fact]
    public void A_networked_session_refuses_to_load_a_save_on_its_own()
    {
        var (_, host, guest) = Started();

        Assert.False(guest.Session.LoadFromJson(host.Session.SaveToJson()));

        host.Dispose();
        guest.Dispose();
    }
}
