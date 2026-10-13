using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class MilitaryRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static Unit Add(Simulation sim, int owner, int tile, string type = "military:warrior")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = sim.State.Grid.HexOf(new(tile)) };
        sim.State.AddUnit(unit); Service<IUnitService>(sim).Initialize(sim, unit.Id); return unit;
    }
    [Fact]
    public void Equal_strength_odds_and_combat_save_replay_match()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var a = Add(sim, 0, 39); var d = Add(sim, 1, 40);
        sim.State.SetWar(0, 1, true);
        var combat = Service<ICombatService>(sim);
        var hash = sim.ComputeHash();
        var preview = combat.Preview(sim, a.Id, d.Id);
        Assert.Equal(500000, preview.SuccessOdds);
        Assert.Equal(20, preview.DamageToAttacker);
        Assert.Equal(200, preview.AttackStrength.Maximum);
        Assert.Equal(hash, sim.ComputeHash());
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        var command = new MoveUnitPathCommand { Player = 0, Unit = a.Id, Path = [d.Position] };
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.True(loaded.TryApply(command, out error), error);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Single(combat.History(sim));
        var midTurn = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), midTurn.ComputeHash());
    }
    [Fact]
    public void Movement_checks_borders_and_uses_fractional_routes()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var unit = Add(sim, 0, 39, "military:worker");
        var movement = Service<IMovementService>(sim);
        sim.State.Plots[40].Kind = PlotKind.Hills;
        Assert.Equal(240, movement.StepCost(sim, unit.Id, new(39), new(40)));
        sim.State.Plots[39].Route = "world:route_route_road"; sim.State.Plots[40].Route = "world:route_route_road";
        Assert.True(movement.StepCost(sim, unit.Id, new(39), new(40)) < 120);
        sim.State.SetRiver(new(39), 0, true);
        Assert.Equal(240, movement.StepCost(sim, unit.Id, new(39), new(40)));
        sim.State.Plots[40].Owner = 1;
        var hash = sim.ComputeHash();
        Assert.False(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = unit.Id, Path = [sim.State.Grid.HexOf(new(40))] }, out _));
        Assert.Equal(hash, sim.ComputeHash());
        sim.State.Plots[40].Owner = 0;
        Assert.NotNull(Service<IPathfindingService>(sim).FindPath(sim, unit.Id, sim.State.Grid.HexOf(new(42))));
    }
    [Fact]
    public void First_strikes_and_lethal_withdrawal_use_the_same_round_model_as_preview()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var a = Add(sim, 0, 39); var d = Add(sim, 1, 40);
        sim.State.SetWar(0, 1, true);
        a.Health = 1; d.Health = 1; a.Promotions.Add("military:drill2");
        var combat = Service<ICombatService>(sim);
        Assert.Equal(750000, combat.Preview(sim, a.Id, d.Id).SuccessOdds);
        d.Health = 100; a.Promotions.Clear();
        a.Promotions.UnionWith(["military:escape1", "military:escape2", "military:tactics"]);
        var preview = combat.Preview(sim, a.Id, d.Id);
        Assert.Equal(100, preview.WithdrawalPercent);
        // Advance the real stream until its next round draw is a lethal defender hit.
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            if (DeterministicRng.Restore(w, x, y, z).Next(1000) < preview.DefenderRoundOdds) break;
            sim.Rng.NextUInt64();
        }
        var report = combat.Resolve(sim, a.Id, d.Id);
        Assert.Equal(CombatOutcome.Withdrawn, report.Outcome);
        Assert.Equal(1, a.Health); Assert.Equal(100, d.Health);
        Assert.Equal(3, a.Experience);
    }
    [Fact]
    public void Collateral_damages_secondary_targets_but_not_immune_siege()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var a = Add(sim, 0, 39, "military:catapult");
        var d = Add(sim, 1, 40, "military:catapult");
        var secondary = Add(sim, 1, 40); var immune = Add(sim, 1, 40, "military:catapult");
        sim.State.SetWar(0, 1, true);
        Service<ICombatService>(sim).Resolve(sim, a.Id, d.Id);
        // Collateral strength 6 versus 2, factor 4: 10 * 10 / 6 = 16.
        Assert.Equal(84, secondary.Health); Assert.Equal(100, immune.Health);
    }
    [Fact]
    public void Capture_transfers_city_and_worker_and_resets_the_foreign_queue()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var a = Add(sim, 0, 39); var worker = Add(sim, 1, 40, "military:worker");
        var position = worker.Position;
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 1, Name = "Target", Position = position, Population = 3,
            ProductionQueue = [new("cities:unit", "military:warrior", 4)] });
        sim.State.SetWar(0, 1, true);
        Assert.True(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = a.Id, Path = [position] }, out var error), error);
        var city = sim.State.CityById(1);
        Assert.Equal(0, city.Owner); Assert.Equal(2, city.Population); Assert.Empty(city.ProductionQueue);
        Assert.Equal(0, sim.State.PlotAt(position).Owner);
        Assert.Contains(sim.State.UnitsAt(position), u => u.Type == "military:worker" && u.Owner == 0 && u.MovesLeft == 0);
        Assert.Null(sim.State.FindUnit(worker.Id));
        Assert.True(sim.TryApply(new RazeCityCommand { Player = 0, City = city.Id }, out error), error);
        Assert.Null(sim.State.CityAt(position));
    }
    [Fact]
    public void Healing_and_upgrade_price_follow_location_and_production_formulas()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var unit = Add(sim, 0, 39);
        var units = Service<IUnitService>(sim);
        Assert.Equal(15, units.HealRate(sim, unit.Id));
        sim.State.Plots[39].Owner = null; Assert.Equal(10, units.HealRate(sim, unit.Id));
        sim.State.SetWar(0, 1, true); sim.State.Plots[39].Owner = 1;
        Assert.Equal(5, units.HealRate(sim, unit.Id));
        sim.State.Plots[39].Owner = 0;
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = unit.Position });
        Assert.Equal(20, units.HealRate(sim, unit.Id));
        // Sequential scaling gives costs 72 and 26. Price: 20 + (72-26)*50/100 = 43.
        Assert.Equal(43, units.UpgradePrice(sim, unit.Id, "military:archer"));
    }
    [Fact]
    public void Barbarian_spawning_and_next_turn_behavior_survive_save_replay()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Turn = 100;
        foreach (var plot in sim.State.Plots) { plot.Owner = null; plot.Visible.Clear(); }
        foreach (int tile in new[] { 0, 5, 11 }) sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new(tile)) });
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Contains(sim.State.Players, p => p.Extensions.Get<MilitaryPlayerState>(sim.Content)?.Barbarian == true);
        Assert.NotEmpty(sim.State.Units);
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }
    [Fact]
    public void Earned_promotions_use_levels_and_heal_half_damage()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var unit = Add(sim, 0, 39); unit.Experience = 2; unit.Health = 40;
        Assert.True(sim.TryApply(new PromoteMilitaryUnitCommand { Player = 0, Unit = unit.Id, Promotion = "military:combat1" }, out var error), error);
        Assert.Equal(70, unit.Health);
        Assert.Equal(5, Service<IUnitService>(sim).ExperienceNeeded(sim, unit.Id));
        Assert.False(sim.TryApply(new PromoteMilitaryUnitCommand { Player = 0, Unit = unit.Id, Promotion = "military:combat1" }, out _));
    }
}
