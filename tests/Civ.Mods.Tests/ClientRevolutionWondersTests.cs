using Civ.Client;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class ClientRevolutionWondersTests(P7ModFixture mods) : IClassFixture<P7ModFixture>
{
    private static GameSession Session(Civ.Content.ContentDb content, out City city)
    {
        var sim = P7ModFixture.Scenario(content);
        city = P7ModFixture.City(sim);
        sim.State.PlayerById(0).Treasury = 1000;
        return new GameSession(sim);
    }

    [Fact]
    public void Stability_is_absent_without_the_mod()
    {
        var session = Session(mods.Wonders, out var city);
        Assert.Null(session.DescribeCity(city)!.Stability);
        Assert.Empty(session.DescribeStability());
    }

    [Fact]
    public void Stability_reports_status_and_suppression_reduces_it()
    {
        var session = Session(mods.Revolution, out var city);
        city.Extensions.Set(session.Sim.Content, new RevolutionCityState { InstabilityIndex = 800 });
        var report = session.DescribeCity(city)!.Stability!;
        Assert.Equal(CityStabilityStatus.Mutiny, report.Status);
        Assert.Equal(800, report.Index);
        Assert.True(report.CanSuppress);
        var summary = Assert.Single(session.DescribeStability());
        Assert.Equal(city.Id, summary.City);
        long gold = session.Me.Treasury;
        Assert.True(session.SuppressRebellion(city.Id));
        Assert.True(session.Me.Treasury < gold);
        Assert.True(session.DescribeCity(city)!.Stability!.Index < 800);
        Assert.Contains(session.Notifications, n => n.Kind == "rebellion");
    }

    [Fact]
    public void Rebellion_triggers_a_notification()
    {
        var session = Session(mods.Revolution, out var city);
        city.Extensions.Set(session.Sim.Content, new RevolutionCityState { InstabilityIndex = 1000 });
        session.Sim.Content.TurnSystems.Get("revolution:rebellions").Run(session.Sim);
        session.Sim.State.Turn++; // any later command collects events
        session.SuppressRebellion(city.Id);
        Assert.Contains(session.Notifications, n => n.Kind == "rebellion" && n.Text.Contains("rebelled"));
    }

    [Fact]
    public void Wonder_scripts_show_countdowns_and_spawn_notifications()
    {
        var session = Session(mods.Wonders, out var city);
        var sim = session.Sim;
        city.Buildings.Add("cities:crusade");
        sim.Events.Publish(new BuildingCompleted(city.Id, "cities:crusade"));
        var script = Assert.Single(session.DescribeCity(city)!.WonderScripts);
        Assert.True(script.Active);
        Assert.Equal(script.SpawnInterval - script.ElapsedTurns, script.TurnsToSpawn);
        sim.Events.Publish(new ScriptedWonderEffectApplied(city.Id, "cities:crusade", "culture", 5));
        session.SetMessage("");
        session.Submit(new EndTurnCommand { Player = 0 });
        Assert.Contains(session.Notifications, n => n.Kind == "wonder");
    }
}
