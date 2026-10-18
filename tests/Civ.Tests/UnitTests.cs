using Civ.BaseGame;
using Civ.Game;
using Xunit;

namespace Civ.Tests;

public class UnitTests
{
    private static (Simulation sim, Hex a, Hex b) TwoAdjacentLandHexes()
    {
        var sim = TestGame.Sim(7);
        var a = sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf).OrderBy(h => h.Q).ThenBy(h => h.R)
            .First(h => { var t = sim.TerrainAt(h); return !t.IsWater && t.Passable && h.Neighbors().Any(n => sim.State.Grid.Contains(n) && sim.TerrainAt(n) is { IsWater: false, Passable: true }); });
        var b = a.Neighbors().First(n => sim.State.Grid.Contains(n) && sim.TerrainAt(n) is { IsWater: false, Passable: true });
        return (sim, a, b);
    }

    [Fact]
    public void UnitDataLoadsFromTheBaseMod()
    {
        var db = TestGame.Content();
        Assert.True(db.Units.Get("base:settler").CanFoundCity);
        Assert.Equal(5, db.ExperienceLevels.Count);
        Assert.Contains("base:shock", db.Promotions.All.Select(p => p.Id));
    }

    [Fact]
    public void CityTrainsUnitsAndBarracksGrantExperience()
    {
        var sim = TestGame.Sim();
        sim.TryApply(new FoundCityCommand { Player = 0, Position = TestGame.FindLand(sim) }, out _);
        sim.State.Cities[0].Buildings.Add("base:barracks");
        Assert.True(sim.TryApply(new StartTrainingCommand { Player = 0, CityId = 1, Unit = "base:warrior" }, out _));
        Assert.False(sim.TryApply(new StartTrainingCommand { Player = 0, CityId = 1, Unit = "base:horseman" }, out _)); // needs tech
        for (int i = 0; i < 20 && sim.State.Units.Count == 0; i++)
        {
            sim.TryApply(new EndTurnCommand { Player = 0 }, out _);
            sim.TryApply(new EndTurnCommand { Player = 1 }, out _);
        }
        var unit = Assert.Single(sim.State.Units);
        Assert.Equal(3, unit.Experience);
        Assert.Equal(1, UnitRules.Level(sim, unit)); // 3 xp passes the first threshold (2)
        Assert.Equal(1, UnitRules.UnspentPromotions(sim, unit));
    }

    [Fact]
    public void PromotionsAreGatedByLevelClassAndPrerequisite()
    {
        var sim = TestGame.Sim();
        var hex = TestGame.FindLand(sim);
        var warrior = UnitRules.Spawn(sim, 0, "base:warrior", hex);
        var cmd = (string id) => new PromoteUnitCommand { Player = 0, UnitId = warrior.Id, Promotion = id };

        Assert.False(sim.TryApply(cmd("base:combat1"), out var err)); // no experience yet
        Assert.Equal("no promotion available", err);

        warrior.Experience = 5; // level 2 -> two promotions
        Assert.False(sim.TryApply(cmd("base:combat2"), out _));  // needs combat1
        Assert.False(sim.TryApply(cmd("base:flanking"), out _)); // mounted only
        Assert.True(sim.TryApply(cmd("base:combat1"), out _));
        Assert.True(sim.TryApply(cmd("base:shock"), out _));     // melee, requires combat1
        Assert.False(sim.TryApply(cmd("base:combat2"), out _));  // both promotions spent
        Assert.Equal(0, UnitRules.UnspentPromotions(sim, warrior));
    }

    [Fact]
    public void PromotionsChangeStrengthAndMovement()
    {
        var sim = TestGame.Sim();
        var hex = TestGame.FindLand(sim);
        var horse = UnitRules.Spawn(sim, 0, "base:horseman", hex);
        int before = UnitRules.Strength(sim, horse, true);
        horse.Promotions.Add("base:combat1");
        horse.Promotions.Add("base:flanking");
        Assert.Equal(before * 110 / 100, UnitRules.Strength(sim, horse, true));
        Assert.Equal(3, UnitRules.MaxMoves(sim, horse));
    }

    [Fact]
    public void UnitsMoveAndCannotEnterEnemyTiles()
    {
        var (sim, a, b) = TwoAdjacentLandHexes();
        var mine = UnitRules.Spawn(sim, 0, "base:warrior", a);
        UnitRules.Spawn(sim, 1, "base:warrior", b);
        Assert.False(sim.TryApply(new MoveUnitCommand { Player = 0, UnitId = mine.Id, To = b }, out var err));
        Assert.Contains("attack", err);
        Assert.False(sim.TryApply(new MoveUnitCommand { Player = 1, UnitId = mine.Id, To = b }, out _)); // not theirs
    }

    [Fact]
    public void CombatIsDeterministicAndWinnersGainExperience()
    {
        (int, int, int) Run()
        {
            var (sim, a, b) = TwoAdjacentLandHexes();
            var att = UnitRules.Spawn(sim, 0, "base:warrior", a);
            UnitRules.Spawn(sim, 1, "base:warrior", b);
            Assert.True(sim.TryApply(new AttackCommand { Player = 0, UnitId = att.Id, Target = b }, out _));
            Assert.Single(sim.State.Units);
            var survivor = sim.State.Units[0];
            return (survivor.Owner, survivor.Experience, survivor.Health);
        }
        var r = Run();
        Assert.Equal(r, Run());
        Assert.Equal(r.Item1 == 0 ? 2 : 1, r.Item2);
    }

    [Fact]
    public void RestingHealsAndRefillsMoves()
    {
        var sim = TestGame.Sim();
        var u = UnitRules.Spawn(sim, 0, "base:warrior", TestGame.FindLand(sim));
        u.Health = 50;
        sim.TryApply(new EndTurnCommand { Player = 0 }, out _);
        sim.TryApply(new EndTurnCommand { Player = 1 }, out _);
        Assert.Equal(60, u.Health);

        u.Promotions.Add("base:medic1");
        u.MovesLeft = 0; // moved this turn: no healing, but moves come back
        sim.TryApply(new EndTurnCommand { Player = 0 }, out _);
        sim.TryApply(new EndTurnCommand { Player = 1 }, out _);
        Assert.Equal(60, u.Health);
        Assert.Equal(1, u.MovesLeft);
    }

    [Fact]
    public void SettlerFoundsCityAndIsConsumed()
    {
        var sim = TestGame.Sim();
        var hex = TestGame.FindLand(sim);
        var settler = UnitRules.Spawn(sim, 0, "base:settler", hex);
        var warrior = UnitRules.Spawn(sim, 0, "base:warrior", hex);
        Assert.False(sim.TryApply(new FoundCityCommand { Player = 0, Position = hex, UnitId = warrior.Id }, out _));
        Assert.True(sim.TryApply(new FoundCityCommand { Player = 0, Position = hex, UnitId = settler.Id }, out _));
        Assert.Single(sim.State.Cities);
        Assert.DoesNotContain(sim.State.Units, u => u.Id == settler.Id);
    }

    [Fact]
    public void UnitCommandsRoundTripThroughCodec()
    {
        var sim = TestGame.Sim();
        var codec = new CommandCodec(sim.Content);
        Assert.IsType<AttackCommand>(codec.Decode(codec.Encode(new AttackCommand { Player = 0, UnitId = 4, Target = new Hex(1, 1) })));
        Assert.IsType<PromoteUnitCommand>(codec.Decode(codec.Encode(new PromoteUnitCommand { Player = 0, UnitId = 4, Promotion = "base:shock" })));
    }

    [Fact]
    public void AiPromotesItsVeterans()
    {
        var sim = new Simulation(TestGame.Content(), new SimulationSettings
        {
            Seed = 11, Map = new(17, 17), PlayerCount = 1, Civilizations = ["base:builders"], PlayerAis = ["base:basic"],
        });
        for (int i = 0; i < 80; i++)
        {
            foreach (var u in sim.State.Units) u.Experience = Math.Max(u.Experience, 5); // fast-forward combat experience
            sim.AdvanceTurn();
        }
        Assert.NotEmpty(sim.State.Units);
        Assert.All(sim.State.Units, u => Assert.NotEmpty(u.Promotions));
    }
}
