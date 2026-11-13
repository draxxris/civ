using Ariel.Client;
using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>
/// A test mod that adds only interface: a screen whose button submits the mod's own command, a city screen row, an
/// encyclopedia category and a notification. The client is driven headless and never sees the mod's types; every
/// assertion goes through what the mod describes.
/// </summary>
public sealed class BulletinModTests
{
    private const string Board = "bulletin:board";
    private static string Root(string folder, string name) => Path.Combine(AppContext.BaseDirectory, folder, name);
    private static string Rules => Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder);

    private static readonly Lazy<ContentDb> Content = new(() =>
        GameContent.Build(Rules, ModLoader.FromDirectories([Root("test-mods", "Bulletin")])));

    private static GameSession Session(out City city)
    {
        var sim = GameFactory.Scenario(Content.Value);
        city = sim.State.Cities.FirstOrDefault(c => c.Owner == 0) ?? AddCity(sim);
        return new GameSession(sim);
    }

    private static City AddCity(Simulation sim)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(14)), Population = 1 };
        sim.State.AddCity(city);
        return city;
    }

    [Fact]
    public void The_screen_has_a_top_bar_button_and_picks_through_local_state()
    {
        var session = Session(out _);
        var entry = Assert.Single(session.Screens.Entries(), e => e.Id == Board);
        Assert.Equal("Bulletins", entry.Button!.Label);
        Assert.True(session.Screens.Open(Board));
        var view = session.Screens.View!;
        Assert.Equal("Bulletin Board", view.Title);
        Assert.Equal(480, view.Width);
        var post = ViewWalk.Find<ActionButton>(view.Body, "post")!;
        Assert.False(post.Enabled);
        Assert.Equal("Pick a bulletin first", post.Disabled);

        int version = session.Screens.Version;
        ulong hash = session.Sim.ComputeHash();
        Assert.True(session.Screens.Press(ViewWalk.ActionOf(view.Body, "bulletin:census")));
        // Local state is the player's alone: the world is untouched, and the view describes again.
        Assert.Equal(hash, session.Sim.ComputeHash());
        Assert.True(session.Screens.IsStale);
        view = session.Screens.View!;
        Assert.True(session.Screens.Version > version);
        Assert.False(session.Screens.IsStale);
        Assert.True(ViewWalk.Find<Table>(view.Body, "posted")!.Rows.Single(r => r.Id == "bulletin:census").Selected);
        Assert.Equal("bulletin:census", session.Screens.StateOf(Board).Get("bulletin"));
        // Choosing the same row again changes nothing.
        Assert.False(session.Screens.Press(ViewWalk.ActionOf(view.Body, "bulletin:census")));
    }

    [Fact]
    public void Its_button_submits_the_mods_own_command_and_every_panel_follows()
    {
        var session = Session(out var city);
        Assert.True(session.Screens.Open(Board));
        Assert.True(session.Screens.Press(ViewWalk.ActionOf(session.Screens.View!.Body, "bulletin:harvest")));
        var post = ViewWalk.Find<ActionButton>(session.Screens.View!.Body, "post")!;
        Assert.Equal("Post Harvest Festival", post.Label);
        var command = Assert.IsType<SubmitCommand>(post.Action).Command;
        Assert.Equal("bulletin:post", session.Sim.Content.CommandTypes.All.Single(c => c.Type == command.GetType()).Id);

        Assert.True(session.Screens.Press(post.Action));
        Assert.True(session.Screens.Press(ViewWalk.ActionOf(session.Screens.View!.Body, "post")));
        var body = session.Screens.View!.Body;
        Assert.Equal(["Harvest Festival", "2"], ViewWalk.Find<Table>(body, "posted")!.Rows.Single(r => r.Id == "bulletin:harvest").Cells);
        Assert.Equal("2", ViewWalk.Find<StatLine>(body, "total")!.Value);

        // The city screen's stats carry the mod's row.
        var row = Assert.IsType<Section>(Assert.Single(session.DescribeCity(city)!.Panels.Stats, s => s.Id == "bulletin:city"));
        Assert.Equal("2", ViewWalk.Find<StatLine>(row.Items, "posted")!.Value);

        // Each post is a notification that goes to the poster's first city.
        var notes = session.Notifications.Where(n => n.Kind == "bulletin").ToList();
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => { Assert.Equal("Posted: Harvest Festival", n.Text); Assert.Equal(city.Id, n.City); Assert.Equal(Tone.Good, n.Tone); });

        // The mod's state is the game's: it survives a save and load.
        var loaded = new GameSession(SaveGame.Load(session.Sim.Content, SaveGame.Save(session.Sim)));
        Assert.Equal("2", ViewWalk.Find<StatLine>(ScreenProbe.Body(loaded, Board), "total")!.Value);
        Assert.Null(loaded.Screens.StateOf(Board).Get("bulletin"));
    }

    [Fact]
    public void An_order_for_another_player_is_refused_and_a_closed_screen_takes_no_state()
    {
        var session = Session(out _);
        Assert.True(session.Screens.Open(Board));
        Assert.True(session.Screens.Press(ViewWalk.ActionOf(session.Screens.View!.Body, "bulletin:census")));
        var post = (SubmitCommand)ViewWalk.ActionOf(session.Screens.View!.Body, "post")!;
        var forged = new SubmitCommand(CommandFor(session.Sim, post.Command, player: 1));
        ulong hash = session.Sim.ComputeHash();
        Assert.False(session.Screens.Press(forged));
        Assert.Equal(hash, session.Sim.ComputeHash());

        var select = ViewWalk.ActionOf(session.Screens.View!.Body, "bulletin:harvest");
        session.Screens.Close();
        Assert.Null(session.Screens.View);
        Assert.False(session.Screens.Press(select));
        Assert.Equal("bulletin:census", session.Screens.StateOf(Board).Get("bulletin"));
    }

    [Fact]
    public void The_encyclopedia_lists_the_mods_category()
    {
        var session = Session(out _);
        Assert.Contains("Bulletins", session.Pedia.Categories);
        Assert.Equal("Bulletins", session.Pedia.Find("bulletin:census")!.Category);
        Assert.NotEmpty(session.Pedia.Search("Bulletins", "Harvest"));
    }

    /// <summary>The same command, for another player, through the command codec: the test never names the mod's type.</summary>
    private static GameCommand CommandFor(Simulation sim, GameCommand command, int player)
    {
        var codec = new CommandCodec(sim.Content);
        var json = System.Text.Json.Nodes.JsonNode.Parse(codec.Encode(command))!;
        var data = json.AsObject().Single(p => string.Equals(p.Key, "data", StringComparison.OrdinalIgnoreCase)).Value!.AsObject();
        data[data.Single(p => string.Equals(p.Key, "player", StringComparison.OrdinalIgnoreCase)).Key] = player;
        return codec.Decode(json.ToJsonString());
    }
}
