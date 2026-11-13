using Ariel.Client;
using Ariel.Game;
using Ariel.Mods.Revolution;
using Ariel.Mods.ScriptedWonders;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The Revolution and ScriptedWonders mods' city panels and notices, driven through the client's host.</summary>
public sealed class ClientRevolutionWondersTests(P7ModFixture mods) : IClassFixture<P7ModFixture>
{
    private static GameSession Session(Ariel.Content.ContentDb content, out City city)
    {
        var sim = P7ModFixture.Scenario(content);
        city = P7ModFixture.City(sim);
        sim.State.PlayerById(0).Treasury = 1000;
        return new GameSession(sim);
    }

    private static IReadOnlyList<ViewElement>? Panel(GameSession session, City city, string id) =>
        session.DescribeCity(city)!.Panels.Status.FirstOrDefault(s => s.Id == id) is Section section ? section.Items : null;

    private static IReadOnlyList<ViewElement> Stability(GameSession session, City city) =>
        Panel(session, city, "revolution:stability") ?? throw new Xunit.Sdk.XunitException("no stability panel");

    private static int Index(IReadOnlyList<ViewElement> panel)
    {
        var heading = ViewWalk.Find<Heading>(panel, "status")!.Text;
        return int.Parse(heading[(heading.LastIndexOf('(') + 1)..^1]);
    }

    private static bool Press(GameSession session, IReadOnlyList<ViewElement> panel, string control) =>
        session.Screens.Press(ViewWalk.ActionOf(panel, control));

    [Fact]
    public void Stability_is_absent_without_the_mod()
    {
        var session = Session(mods.Wonders, out var city);
        Assert.Null(Panel(session, city, "revolution:stability"));
        Assert.Empty(session.SummaryItems());
    }

    [Fact]
    public void Stability_reports_status_and_suppression_reduces_it()
    {
        var session = Session(mods.Revolution, out var city);
        city.Extensions.Set(session.Sim.Content, new RevolutionCityState { InstabilityIndex = 800 });
        var panel = Stability(session, city);
        Assert.Equal("Stability: Mutiny (800)", ViewWalk.Find<Heading>(panel, "status")!.Text);
        Assert.Equal(Tone.Warn, ViewWalk.Find<Paragraph>(panel, "change")!.Tone);
        Assert.Contains(session.SummaryItems(), i => i.Id == "unrest");
        long gold = session.Me.Treasury;
        Assert.True(Press(session, panel, "suppress_gold"));
        Assert.True(session.Me.Treasury < gold);
        Assert.True(Index(Stability(session, city)) < 800);
        Assert.Contains(session.Notifications, n => n.Kind == "rebellion");
    }

    [Fact]
    public void Rebellion_triggers_a_notification()
    {
        var session = Session(mods.Revolution, out var city);
        city.Extensions.Set(session.Sim.Content, new RevolutionCityState { InstabilityIndex = 1000 });
        session.Sim.Content.TurnSystems.Get("revolution:rebellions").Run(session.Sim);
        session.Sim.State.Turn++; // any later command collects events
        Press(session, Stability(session, city), "suppress_gold");
        Assert.Contains(session.Notifications, n => n.Kind == "rebellion" && n.Text.Contains("rebelled") && n.Tone == Tone.Warn);
    }

    [Fact]
    public void A_garrison_suppresses_when_gold_cannot_pay()
    {
        var session = Session(mods.Revolution, out var city);
        session.Sim.State.PlayerById(0).Treasury = 0;
        city.Extensions.Set(session.Sim.Content, new RevolutionCityState { InstabilityIndex = 800 });
        var garrison = P7ModFixture.Unit(session.Sim, city);
        garrison.MovesLeft = 1;
        var panel = Stability(session, city);
        Assert.Null(ViewWalk.Find<ActionButton>(panel, "suppress_gold"));
        Assert.NotNull(ViewWalk.Find<ActionButton>(panel, "suppress_garrison"));
        Assert.Null(ViewWalk.Find<Paragraph>(panel, "blocked"));
        Assert.True(Press(session, panel, "suppress_garrison"));
        Assert.Equal(0, garrison.MovesLeft);
        Assert.Equal(0, session.Me.Treasury);
        Assert.True(Index(Stability(session, city)) < 800);
        // The button drawn before is stale: the garrison has no moves left to spend.
        Assert.False(Press(session, panel, "suppress_garrison"));
    }

    [Fact]
    public void Suppression_without_gold_or_a_garrison_reports_the_gold_block()
    {
        var session = Session(mods.Revolution, out var city);
        session.Sim.State.PlayerById(0).Treasury = 0;
        city.Extensions.Set(session.Sim.Content, new RevolutionCityState { InstabilityIndex = 800 });
        var panel = Stability(session, city);
        Assert.Null(ViewWalk.Find<ActionButton>(panel, "suppress_gold"));
        Assert.Null(ViewWalk.Find<ActionButton>(panel, "suppress_garrison"));
        Assert.Equal("Insufficient gold", ViewWalk.Find<Paragraph>(panel, "blocked")!.Text);
        Assert.False(session.Screens.Press(new SubmitCommand(new SuppressRebellionCommand { Player = 0, CityId = city.Id })));
    }

    [Fact]
    public void Wonder_scripts_show_countdowns_and_spawn_notifications()
    {
        var session = Session(mods.Wonders, out var city);
        var sim = session.Sim;
        city.Buildings.Add("cities:crusade");
        sim.Events.Publish(new BuildingCompleted(city.Id, "cities:crusade"));
        var panel = Panel(session, city, "scripted_wonders:city")!;
        var script = Assert.Single(ViewWalk.All(panel).OfType<Paragraph>());
        Assert.Equal("cities:crusade", script.Id);
        Assert.Matches(@": next .+ in \d+ turns$", script.Text);
        sim.Events.Publish(new ScriptedWonderEffectApplied(city.Id, "cities:crusade", "culture", 5));
        session.SetMessage("");
        session.Submit(new EndTurnCommand { Player = 0 });
        Assert.Contains(session.Notifications, n => n.Kind == "wonder");
    }
}
