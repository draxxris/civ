using Ariel.Client;
using Ariel.Game;
using Ariel.Net;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The pre-game lobby over the in-memory hub, through to a started game on every peer.</summary>
public sealed class LobbyTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private GameCatalog Catalog() => mods.Catalog;

    private static void Settle(params Lobby[] lobbies)
    {
        for (int i = 0; i < 10; i++) foreach (var l in lobbies) l.Poll();
    }

    private static void AssertSame(LobbyState a, LobbyState b) =>
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a), System.Text.Json.JsonSerializer.Serialize(b));

    private (Lobby Host, Lobby Guest) Pair(GameCatalog catalog)
    {
        var hub = new InProcessHub();
        var setup = LobbySetup.Default(catalog) with { WorldSize = "foundation:worldsize_duel", Seed = 9 };
        var host = Lobby.Host(catalog, hub.CreateEndpoint(), "Ada", setup);
        var guest = Lobby.Join(catalog, hub.CreateEndpoint(), "Bo");
        Settle(host, guest);
        return (host, guest);
    }

    [Fact]
    public void A_guest_sees_the_hosts_state_and_can_take_an_open_seat()
    {
        var (host, guest) = Pair(Catalog());

        AssertSame(host.State, guest.State);
        Assert.Contains(guest.State.Peers, p => p.Name == "Bo" && p.ContentMatches);
        guest.TakeSeat(0); // taken by the host
        guest.TakeSeat(1);
        Settle(host, guest);

        Assert.Equal(0, host.State.Seats[0].Peer);
        Assert.Equal(1, host.State.Seats[1].Peer);
        AssertSame(host.State, guest.State);
        Assert.Null(host.State.StartProblem());
    }

    [Fact]
    public void Only_the_host_changes_the_setup_and_starting_needs_every_human_seat_filled()
    {
        var (host, guest) = Pair(Catalog());
        guest.SetSeatKind(1, SeatKind.Ai); // ignored: not the host
        Settle(host, guest);
        Assert.Equal(SeatKind.Human, host.State.Seats[1].Kind);

        Assert.NotNull(host.Start());
        host.AddSeat();
        Settle(host, guest);
        Assert.Equal(3, guest.State.Seats.Count);
        Assert.Equal(SeatKind.Ai, guest.State.Seats[2].Kind);
    }

    [Fact]
    public void A_departing_guest_frees_its_seat()
    {
        var hub = new InProcessHub();
        var catalog = Catalog();
        var host = Lobby.Host(catalog, hub.CreateEndpoint(), "Ada", LobbySetup.Default(catalog));
        var link = hub.CreateEndpoint();
        var guest = Lobby.Join(catalog, link, "Bo");
        Settle(host, guest);
        guest.TakeSeat(1);
        Settle(host, guest);

        link.Dispose();
        Settle(host);

        Assert.Null(host.State.Seats[1].Peer);
        Assert.DoesNotContain(host.State.Peers, p => p.Peer == 1);
        Assert.Contains(host.Chat.Lines, l => l.Text == "Bo left.");
    }

    [Fact]
    public void Chat_reaches_every_peer_with_the_senders_name()
    {
        var (host, guest) = Pair(Catalog());

        guest.Say("hello");
        Settle(host, guest);

        Assert.Equal(new ChatLine(1, "Bo", "hello"), host.Chat.Lines.Last());
        Assert.Equal(new ChatLine(1, "Bo", "hello"), guest.Chat.Lines.Last());
    }

    [Fact]
    public void Starting_gives_every_peer_the_same_world_and_a_seat_to_play()
    {
        var catalog = Catalog();
        var (host, guest) = Pair(catalog);
        host.SetSeatKind(1, SeatKind.Human);
        host.AddSeat(SeatKind.Ai);
        guest.TakeSeat(1);
        guest.ChooseLeader("peoples:leader_julius_caesar");
        Settle(host, guest);

        Assert.Null(host.Start());
        Settle(host, guest);
        Assert.NotNull(host.Starting);
        Assert.NotNull(guest.Starting);
        Assert.Equal(SaveGame.SaveSettings(host.Starting!.Settings), SaveGame.SaveSettings(guest.Starting!.Settings));
        Assert.Equal([0, 1], guest.Starting.Peers);
        Assert.Equal(1, guest.Starting.LocalSeat);

        using var a = MultiplayerGame.Create(catalog, host.Release(), host.Starting, host.Chat);
        using var b = MultiplayerGame.Create(catalog, guest.Release(), guest.Starting, guest.Chat);
        Assert.Equal(a.Session.Sim.ComputeHash(), b.Session.Sim.ComputeHash());
        Assert.Equal("peoples:leader_julius_caesar", b.Session.Me.Leader);
        Assert.NotNull(b.Session.Sim.State.PlayerById(2).Ai);
        Assert.Equal(TurnMode.Dynamic, b.Session.Sim.Settings.TurnMode);

        a.Session.EndTurn();
        b.Session.EndTurn();
        for (int i = 0; i < 400 && (a.Session.Sim.State.Turn < 2 || b.Session.Sim.State.Turn < 2); i++)
        {
            a.Session.Pump();
            b.Session.Pump();
        }
        Assert.Equal(2, b.Session.Sim.State.Turn);
        Assert.Null(a.Session.ConnectionProblem);
        Assert.Equal(a.Session.Sim.ComputeHash(), b.Session.Sim.ComputeHash());
    }

    [Fact]
    public void A_peer_leaving_mid_game_is_reported_to_the_others()
    {
        var catalog = Catalog();
        var (host, guest) = Pair(catalog);
        guest.TakeSeat(1);
        Settle(host, guest);
        Assert.Null(host.Start());
        Settle(host, guest);
        using var a = MultiplayerGame.Create(catalog, host.Release(), host.Starting!, host.Chat);
        var b = MultiplayerGame.Create(catalog, guest.Release(), guest.Starting!, guest.Chat);

        b.Dispose();
        a.Session.Pump();

        Assert.Equal("Bo left the game.", a.Session.ConnectionProblem);
    }
}
