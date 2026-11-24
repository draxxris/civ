using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Mods.Espionage;
using Ariel.Modding;
using Xunit;
using Xunit.Abstractions;

namespace Ariel.Mods.Tests;

/// <summary>A spy mission in a played game, end to end: the espionage the Espionage mod adds to the palace is commerce
/// the economy splits, the mod's allocation turns it into points against the rival the AI planner weights, and a spy
/// standing in the rival's capital spends them on a mission that resolves through the base mission command, with the
/// simulation's dice. Nothing in the test writes points, weights or slider rates.</summary>
public sealed class EspionageEndToEndTests(ITestOutputHelper output)
{
    private const string Spy = "espionage:spy";
    private const int MaxTurns = 150;

    private static readonly Lazy<ContentDb> WithEspionage = new(() => ModLoader.Build(new LoadedMods().Sources
        .Append(ModLoader.FromDirectory(Path.Combine(AppContext.BaseDirectory, "optional-mods", "Espionage")))));

    private static IEspionageService Spies(Simulation sim) => sim.Service<IEspionageService>()!;

    [Fact]
    [Trait("Category", "E2E")]
    public void A_spy_spends_points_the_palace_earned_on_a_mission_that_resolves_and_replays_from_a_save()
    {
        var content = WithEspionage.Value;
        Assert.Equal(4, content.Get<BuildingDef>("cities:palace").CommerceChanges.Espionage);

        var sim = GameFactory.CreateAi(content, seed: 7, players: 2);
        sim.Rules().Relations.EstablishContact(sim, 0, 1);
        int rival = sim.State.PlayerById(1).TeamId;
        City? target = null;
        long cost = long.MaxValue;
        while (sim.State.Turn < MaxTurns)
        {
            sim.AdvanceTurn();
            target ??= sim.State.Cities.Where(c => c.Owner == 1).OrderBy(c => c.Id).FirstOrDefault();
            if (target is null) continue;
            cost = Spies(sim).MissionCost(sim, 0, target.Id, EspionageMissionKind.StealTreasury);
            if (Spies(sim).Points(sim, sim.State.PlayerById(0).TeamId, rival) >= cost) break;
        }
        Assert.NotNull(target);
        int team = sim.State.PlayerById(0).TeamId;
        long points = Spies(sim).Points(sim, team, rival);
        Assert.True(points >= cost, $"by turn {sim.State.Turn} the palace earned {points} points, short of {cost}");
        Assert.True(EspionageCommerce.Hundredths(sim, 0) > 0);

        var spy = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Position = target.Position, Type = Spy, MovesLeft = 60 };
        sim.State.AddUnit(spy);
        var resumed = SaveGame.Load(content, SaveGame.Save(sim));
        foreach (var game in new[] { sim, resumed })
        {
            var city = game.State.CityById(target.Id);
            long victim = game.State.PlayerById(1).Treasury, own = game.State.PlayerById(0).Treasury;
            int caught = game.Rules().Relations.Memories(game, 1, 0).GetValueOrDefault("espionage:spy_caught");
            int before = game.Events.Published.Count;
            Assert.True(game.TryApply(EspionageMissions.Order(0, spy.Id, EspionageMissionKind.StealTreasury), out var error), error);

            var published = game.Events.Published.Skip(before).ToArray();
            var result = Assert.Single(published.OfType<EspionageMissionResult>());
            output.WriteLine($"turn {game.State.Turn}: {points} points, theft costs {cost}, success {result.Success}");
            Assert.Equal((city.Id, EspionageMissionKind.StealTreasury, cost, 0), (result.City, result.Mission, result.Cost, result.Player));
            Assert.Contains(published.OfType<MissionResolved>(),
                m => m.Mission == EspionageMissions.Of(EspionageMissionKind.StealTreasury) && m.Unit == spy.Id && m.Success == result.Success);
            Assert.Equal(points - cost, Spies(game).Points(game, team, rival));
            if (result.Success)
            {
                Assert.Equal(own + victim / 4, game.State.PlayerById(0).Treasury);
                Assert.Equal(victim - victim / 4, game.State.PlayerById(1).Treasury);
            }
            else
            {
                Assert.Null(game.State.FindUnit(spy.Id));
                Assert.Equal(victim, game.State.PlayerById(1).Treasury);
                Assert.Equal(caught + 1, game.Rules().Relations.Memories(game, 1, 0).GetValueOrDefault("espionage:spy_caught"));
            }
        }
        Assert.Equal(sim.ComputeHash(), resumed.ComputeHash());
        sim.AdvanceTurn(); resumed.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), resumed.ComputeHash());
    }
}
