using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class MilitaryRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static Unit Add(Simulation sim, int owner, int tile, string type = "military:warrior")
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = sim.State.Grid.HexOf(new(tile)) };
        sim.State.AddUnit(unit); Service<IUnitService>(sim).Initialize(sim, unit.Id);
        Service<IVisibilityService>(sim).Recompute(sim); return unit;
    }
    [Fact]
    public void Pillaging_an_owned_improvement_is_free_and_publishes_its_step_down()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var def = mods.Content.Get<Civ.Content.Schema.UnitDef>().All.First(u => u.Pillage && u.Domain == "military:domain_land");
        var unit = Add(sim, 0, 39, def.Id);
        var improvement = mods.Content.Get<Civ.Content.Schema.ImprovementDef>().All
            .First(i => !i.Permanent && !i.Goody && i.ImprovementPillage is not null && i.PillageGold > 0);
        var plot = sim.State.PlotAt(unit.Position);
        plot.Owner = unit.Owner;
        plot.Improvement = improvement.Id;
        plot.BuildProgress["world:build_build_farm"] = 9;
        long treasury = sim.State.PlayerById(unit.Owner).Treasury;
        int movesBefore = unit.MovesLeft;
        int moveDenominator = mods.Content.Get<Civ.Content.Schema.RuleConstantsDef>().All.Single().Values.MoveDenominator;

        Assert.True(sim.TryApply(new PillageCommand { Player = unit.Owner, Unit = unit.Id }, out var error), error);

        Assert.Equal(improvement.ImprovementPillage, plot.Improvement);
        Assert.Equal(0, plot.ImprovementTurns);
        Assert.Empty(plot.BuildProgress);
        Assert.Equal(treasury, sim.State.PlayerById(unit.Owner).Treasury);
        var message = Assert.Single(sim.Events.Published.OfType<PlotPillaged>());
        Assert.Equal(improvement.Id, message.Improvement);
        Assert.Equal(improvement.ImprovementPillage, message.ReplacementImprovement);
        Assert.Equal(0, message.Gold);
        Assert.Equal(movesBefore - moveDenominator, unit.MovesLeft);
    }

    [Fact]
    public void Pillaging_rejects_loaded_units_permanent_improvements_and_owned_routes()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var units = Service<IUnitService>(sim);
        var land = mods.Content.Get<Civ.Content.Schema.UnitDef>().All.First(u => u.Pillage && u.Domain == "military:domain_land");
        var unit = Add(sim, 0, 39, land.Id);
        var plot = sim.State.PlotAt(unit.Position);
        plot.Owner = unit.Owner;
        plot.Route = "world:route_route_road";
        Assert.Equal("Cannot pillage at peace", units.Validate(sim, unit.Owner, unit.Id, UnitAction.Pillage, ""));

        plot.Route = null;
        plot.Improvement = mods.Content.Get<Civ.Content.Schema.ImprovementDef>().All.First(i => i.Permanent && !i.Goody).Id;
        unit.MovesLeft = units.MaximumMoves(sim, unit.Id);
        Assert.Equal("Cannot pillage a permanent improvement", units.Validate(sim, unit.Owner, unit.Id, UnitAction.Pillage, ""));

        plot.Improvement = null;
        sim.State.Plots[40].Terrain = "world:coast";
        var ship = Add(sim, 0, 40, "military:cog");
        var cargo = Add(sim, 0, 40, land.Id);
        Assert.True(sim.TryApply(new LoadUnitCommand { Player = 0, Unit = cargo.Id, Transport = ship.Id }, out var error), error);
        Assert.Equal("Loaded cargo cannot pillage", units.Validate(sim, cargo.Owner, cargo.Id, UnitAction.Pillage, ""));
    }

    [Fact]
    public void Pillaging_a_foreign_improvement_awards_gold_but_route_only_pillage_does_not()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var land = mods.Content.Get<Civ.Content.Schema.UnitDef>().All.First(u => u.Pillage && u.Domain == "military:domain_land");
        var unit = Add(sim, 0, 39, land.Id);
        var plot = sim.State.PlotAt(unit.Position);
        plot.Owner = 1;
        var improvement = mods.Content.Get<Civ.Content.Schema.ImprovementDef>().All
            .Where(i => !i.Permanent && !i.Goody && i.PillageGold > 0).MaxBy(i => i.PillageGold)!;
        plot.Improvement = improvement.Id;
        sim.State.SetWar(0, 1, true);
        long treasuryBefore = sim.State.PlayerById(0).Treasury;

        Assert.True(sim.TryApply(new PillageCommand { Player = 0, Unit = unit.Id }, out var error), error);
        var award = Assert.Single(sim.Events.Published.OfType<PlotPillaged>());
        Assert.Equal(improvement.Id, award.Improvement);
        Assert.True(award.Gold > 0);
        Assert.Equal(award.Gold, sim.State.PlayerById(0).Treasury - treasuryBefore);

        var routeSim = GameFactory.Scenario(mods.Content);
        var routeUnit = Add(routeSim, 0, 39, land.Id);
        var routePlot = routeSim.State.PlotAt(routeUnit.Position);
        routePlot.Owner = 1; routePlot.Route = "world:route_route_road";
        routeSim.State.SetWar(0, 1, true);
        Assert.True(routeSim.TryApply(new PillageCommand { Player = 0, Unit = routeUnit.Id }, out error), error);
        var routeEvent = Assert.Single(routeSim.Events.Published.OfType<PlotPillaged>());
        Assert.Null(routeEvent.Improvement);
        Assert.Equal("world:route_route_road", routeEvent.Route);
        Assert.Equal(0, routeEvent.Gold);
        Assert.Null(routePlot.Route);
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
    public void Pathfinding_prefers_a_cheaper_route_detour_over_the_shortest_hex_line()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var start = sim.State.Grid.HexOf(sim.State.Grid.TileAt(3, 3));
        var goal = start.Neighbor(0).Neighbor(0);
        var unit = Add(sim, 0, sim.State.Grid.TileAt(start).Value);
        var detour = new[] { start.Neighbor(1), start.Neighbor(1).Neighbor(0), goal };
        foreach (var hex in detour.Prepend(start)) sim.State.PlotAt(hex).Route = "world:route_route_gravtube";
        var path = Service<IPathfindingService>(sim).FindPath(sim, unit.Id, goal);
        Assert.NotNull(path);
        Assert.Equal(18, path.Cost); // Three route edges at six sub-moves each, not two ordinary edges at 120.
        Assert.Equal(detour, path.Steps);
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
    [Theory]
    [InlineData(1, 1, 0, true)]
    [InlineData(2, 1, 0, false)]
    [InlineData(1, 2, 0, false)]
    [InlineData(1, 1, 40, false)]
    public void Military_capture_auto_razes_only_size_one_cities_without_growth_or_cultural_expansion(int population, int highest, long culture, bool autoRazed)
    {
        var sim = GameFactory.Scenario(mods.Content);
        var attacker = Add(sim, 0, 39);
        var city = new City { Id = sim.State.NextCityId++, Owner = 1, Position = sim.State.Grid.HexOf(new(40)), Population = population, Culture = culture };
        city.Extensions.Set(sim.Content, new CityRuleState { HighestPopulation = highest });
        city.Buildings.Add("cities:palace"); sim.State.AddCity(city);
        var remaining = new City { Id = sim.State.NextCityId++, Owner = 1, Position = sim.State.Grid.HexOf(new(80)) };
        sim.State.AddCity(remaining); sim.State.SetWar(0, 1, true); sim.State.Turn = 50;
        long treasury = sim.State.PlayerById(0).Treasury;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var command = new MoveUnitPathCommand { Player = 0, Unit = attacker.Id, Path = [city.Position] };
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.True(loaded.TryApply(command, out error), error);
        var message = Assert.Single(sim.Events.Published.OfType<CityCaptured>());
        Assert.Equal(autoRazed, message.AutoRazed);
        Assert.Equal(1, message.PreviousOwner); Assert.Equal(0, message.Player);
        Assert.True(message.CaptureGold > 0);
        Assert.Equal(message.CaptureGold, sim.State.PlayerById(0).Treasury - treasury);
        Assert.Equal(autoRazed, sim.State.FindCity(city.Id) is null);
        Assert.Equal(city.Position, attacker.Position);
        Assert.Contains("cities:palace", remaining.Buildings);
        if (autoRazed)
        {
            var razed = Assert.Single(sim.Events.Published.OfType<CityRazed>());
            Assert.Equal(0, razed.Player); Assert.Equal(city.Position, razed.Position);
            var events = sim.Events.Published.Where(e => e is CityCaptured or CityRazed).ToArray();
            Assert.IsType<CityCaptured>(events[0]); Assert.IsType<CityRazed>(events[1]);
        }
        else
        {
            var captured = sim.State.CityById(city.Id);
            Assert.Equal(0, captured.Owner);
            Assert.Equal(Math.Max(highest, population), captured.Extensions.Get<CityRuleState>(sim.Content)!.HighestPopulation);
            Assert.True(captured.Extensions.Get<CityRuleState>(sim.Content)!.PendingConquestChoice);
        }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Ai_capture_resolves_immediately_without_a_pending_human_choice()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var old = sim.State.PlayerById(0);
        sim.State.Turn = 9;
        sim.State.Players[0] = new Player { Id = 0, TeamId = old.TeamId, Civilization = old.Civilization, Ai = "ai:standard" };
        sim.State.RebuildIndexes();
        var attacker = Add(sim, 0, 39);
        var city = new City { Id = sim.State.NextCityId++, Owner = 1, Position = sim.State.Grid.HexOf(new(40)), Population = 3 };
        sim.State.AddCity(city); sim.State.SetWar(0, 1, true);
        Assert.True(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = attacker.Id, Path = [city.Position] }, out var error), error);
        var captured = sim.State.CityById(city.Id);
        var state = captured.Extensions.Get<CityRuleState>(sim.Content)!;
        Assert.False(state.PendingConquestChoice); Assert.False(state.IsPuppet);
        Assert.Equal(9, state.TurnAcquired);
        Assert.True(sim.TryApply(new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:unit", ContentId = "military:warrior" }, out error), error);
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
    [Fact]
    public void First_strikes_cancel_so_equal_first_strikes_do_not_burn_dummy_rounds()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var a = Add(sim, 0, 39); var d = Add(sim, 1, 40);
        sim.State.SetWar(0, 1, true);
        a.Promotions.Add("military:drill2"); d.Promotions.Add("military:drill2");
        var combat = Service<ICombatService>(sim);
        var preview = combat.Preview(sim, a.Id, d.Id);
        var report = combat.Resolve(sim, a.Id, d.Id);
        Assert.Equal(preview.AttackerFirstStrikes, preview.DefenderFirstStrikes);
        Assert.DoesNotContain(report.Rounds, r => r.FirstStrike);
    }
    [Fact]
    public void Siege_cannot_walk_onto_a_defended_city_when_defenders_are_at_the_combat_limit()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var a = Add(sim, 0, 39, "military:catapult"); var d = Add(sim, 1, 40);
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 1, Name = "Target", Position = d.Position, Population = 3 });
        sim.State.SetWar(0, 1, true);
        d.Health = 100 - MilitaryDataLimit(sim, a);
        sim.State.PlotAt(d.Position).Owner = 1;
        sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = a.Id, Path = [d.Position] }, out _);
        Assert.Equal(1, sim.State.CityAt(d.Position)!.Owner);
        Assert.NotNull(sim.State.FindUnit(d.Id));
        Assert.NotEqual(d.Position, sim.State.UnitById(a.Id).Position);
    }
    [Fact]
    public void Lethal_defender_withdrawal_keeps_both_units_and_allows_attacker_to_advance()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var a = Add(sim, 0, 39, "military:javelineer"); var d = Add(sim, 1, 40);
        sim.State.SetWar(0, 1, true);
        d.Promotions.UnionWith(["military:escape1", "military:escape2", "military:tactics"]);
        var target = d.Position;
        var preview = Service<ICombatService>(sim).Preview(sim, a.Id, d.Id);
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            var probe = DeterministicRng.Restore(w, x, y, z);
            if (Enumerable.Range(0, 8).All(_ => probe.Next(preview.DieSides) >= preview.DefenderRoundOdds)) break;
            sim.Rng.NextUInt64();
        }
        Assert.True(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = a.Id, Path = [target] }, out var error), error);
        var report = Assert.Single(Service<ICombatService>(sim).History(sim));
        Assert.Equal(CombatOutcome.DefenderWithdrawn, report.Outcome);
        Assert.Equal(1, d.Health); Assert.NotEqual(target, d.Position); Assert.Equal(target, a.Position);
        Assert.True(report.Rounds.Count > 1); Assert.Equal(1, sim.State.Grid.Distance(target, d.Position));
        Assert.True(sim.State.Grid.Distance(a.Position, d.Position) > 0);
        Assert.DoesNotContain(sim.State.Grid.Range(sim.State.Grid.TileAt(d.Position), 1).SelectMany(t => sim.State.UnitsAt(sim.State.Grid.HexOf(t))),
            u => u.Owner == 0 && u.Id != a.Id);
        Assert.True(a.Experience > 0); Assert.True(d.Experience > 0); Assert.False(report.LoserDied);
        Assert.Equal(sim.ComputeHash(), SaveGame.Load(sim.Content, SaveGame.Save(sim)).ComputeHash());
    }

    [Fact]
    public void Bombardment_reduces_defense_and_recovery_skips_the_bombarded_turn()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var siege = Add(sim, 0, 39, "military:catapult");
        var defender = Add(sim, 1, 40);
        var city = new City { Id = sim.State.NextCityId++, Owner = 1, Position = defender.Position, Population = 3 };
        city.Buildings.Add("cities:walls"); sim.State.AddCity(city);
        sim.State.SetWar(0, 1, true);
        var combat = Service<ICombatService>(sim);
        long before = combat.Preview(sim, siege.Id, defender.Id).DefenseStrength.Ledger.Total("military:combat_percent");
        Assert.True(sim.TryApply(new BombardCityCommand { Player = 0, Unit = siege.Id, Target = city.Position }, out var error), error);
        var state = city.Extensions.Get<CityRuleState>(sim.Content)!;
        Assert.Equal(20, state.DefenseDamage); // 8 * 80 / 100 = 6 defense points; 100 * 6 / 30 = 20 damage.
        Assert.True(state.BombardedThisTurn);
        Assert.Equal(6, Assert.Single(sim.Events.Published.OfType<CityBombarded>()).DefenseReduction);
        Assert.True(combat.Preview(sim, siege.Id, defender.Id).DefenseStrength.Ledger.Total("military:combat_percent") < before);
        Assert.NotNull(combat.ValidateBombard(sim, 0, siege.Id, city.Position));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(20, state.DefenseDamage); Assert.False(state.BombardedThisTurn);
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(15, state.DefenseDamage);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ranged_fire_spends_the_attack_without_melee_retaliation_and_replays(bool hit)
    {
        var sim = GameFactory.Scenario(mods.Content);
        var siege = Add(sim, 0, 39, "military:catapult");
        var defender = Add(sim, 1, 40); var secondary = Add(sim, 1, 40);
        sim.State.SetWar(0, 1, true);
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            if ((DeterministicRng.Restore(w, x, y, z).Next(100) < 70) == hit) break;
            sim.Rng.NextUInt64();
        }
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var command = new RangedStrikeCommand { Player = 0, Unit = siege.Id, Target = defender.Position };
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.True(loaded.TryApply(command, out error), error);
        Assert.Equal(100, siege.Health); Assert.Equal(0, siege.MovesLeft);
        Assert.Equal(hit, defender.Health < 100); Assert.Equal(hit, secondary.Health < 100);
        Assert.NotNull(command.Validate(sim));
        Assert.Single(Service<ICombatService>(sim).History(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Cargo_boards_on_move_travels_hidden_and_unloads_after_save()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Plots[40].Terrain = "world:coast"; sim.State.Plots[41].Terrain = "world:coast";
        var ship = Add(sim, 0, 40, "military:cog"); var cargo = Add(sim, 0, 39);
        Add(sim, 1, 42);
        var units = Service<IUnitService>(sim);
        var hash = sim.ComputeHash();
        Assert.Null(Service<IMovementService>(sim).CanEnter(sim, cargo.Id, new(39), new(40)));
        Assert.Equal(hash, sim.ComputeHash());
        Assert.True(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = cargo.Id, Path = [ship.Position] }, out var error), error);
        Assert.Equal(ship.Id, cargo.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
        Assert.Equal(0, cargo.MovesLeft);
        Assert.False(Service<IVisibilityService>(sim).IsUnitVisible(sim, cargo.Id, 1));
        Assert.True(Service<IVisibilityService>(sim).IsUnitVisible(sim, cargo.Id, 0));
        Assert.True(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = ship.Id, Path = [sim.State.Grid.HexOf(new(41))] }, out error), error);
        Assert.Equal(ship.Position, cargo.Position);
        cargo.MovesLeft = units.MaximumMoves(sim, cargo.Id);
        Assert.NotNull(units.ValidateUnload(sim, 0, cargo.Id));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var unload = new UnloadUnitCommand { Player = 0, Unit = cargo.Id, Destination = sim.State.Grid.HexOf(new(42)) };
        Assert.True(sim.TryApply(unload, out error), error);
        Assert.True(loaded.TryApply(unload, out error), error);
        Assert.Null(cargo.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
        Assert.Equal(0, cargo.MovesLeft); Assert.Empty(units.CargoUnits(sim, ship.Id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Removing_a_transport_removes_its_cargo(bool combatLoss)
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Plots[39].Terrain = "world:coast"; sim.State.Plots[40].Terrain = "world:coast";
        var ship = Add(sim, 0, 40, "military:cog"); var cargo = Add(sim, 0, 40);
        Assert.True(sim.TryApply(new LoadUnitCommand { Player = 0, Unit = cargo.Id, Transport = ship.Id }, out var error), error);
        Assert.NotNull(Service<IUnitService>(sim).ValidateLoad(sim, 0, cargo.Id, ship.Id));
        if (combatLoss)
        {
            var attacker = Add(sim, 1, 39, "military:battleship");
            sim.State.SetWar(0, 1, true); ship.Health = 1;
            Assert.Equal(ship.Id, Service<ICombatService>(sim).BestDefender(sim, attacker.Id, ship.Position));
            Assert.True(sim.TryApply(new MoveUnitPathCommand { Player = 1, Unit = attacker.Id, Path = [ship.Position] }, out error), error);
        }
        else Assert.True(sim.TryApply(new DisbandUnitCommand { Player = 0, Unit = ship.Id }, out error), error);
        Assert.Null(sim.State.FindUnit(ship.Id)); Assert.Null(sim.State.FindUnit(cargo.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Amphibious_landings_detach_cargo_and_only_trained_units_keep_moves(bool amphibious)
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Plots[40].Terrain = "world:coast";
        var ship = Add(sim, 0, 40, "military:cog"); var cargo = Add(sim, 0, 40, "military:worker");
        if (amphibious) cargo.Promotions.Add("military:amphibious");
        Assert.True(sim.TryApply(new LoadUnitCommand { Player = 0, Unit = cargo.Id, Transport = ship.Id }, out var error), error);
        Assert.True(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = cargo.Id, Path = [sim.State.Grid.HexOf(new(41))] }, out error), error);
        Assert.Null(cargo.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
        Assert.Equal(amphibious, cargo.MovesLeft > 0);
    }

    [Fact]
    public void Cargo_capacity_promotions_allow_one_more_passenger_without_relaxing_domain_rules()
    {
        var sim = GameFactory.Scenario(mods.Content); sim.State.Plots[40].Terrain = "world:coast";
        var ship = Add(sim, 0, 40, "military:cog"); var units = Service<IUnitService>(sim);
        int capacity = units.CargoCapacity(sim, ship.Id); Assert.True(capacity > 0);
        for (int i = 0; i < capacity; i++)
        {
            var passenger = Add(sim, 0, 40);
            Assert.True(sim.TryApply(new LoadUnitCommand { Player = 0, Unit = passenger.Id, Transport = ship.Id }, out var error), error);
        }
        var extra = Add(sim, 0, 40); var load = new LoadUnitCommand { Player = 0, Unit = extra.Id, Transport = ship.Id };
        Assert.False(sim.TryApply(load, out _));
        ship.Promotions.Add("military:transport1");
        Assert.Equal(capacity + 1, units.CargoCapacity(sim, ship.Id));
        Assert.True(sim.TryApply(load, out var loadError), loadError); Assert.Equal(capacity + 1, units.CargoUnits(sim, ship.Id).Count);
        ship.Promotions.Add("military:transport2");
        var aircraft = Add(sim, 0, 40, "military:fighter");
        Assert.NotNull(units.ValidateLoad(sim, 0, aircraft.Id, ship.Id));
    }

    [Fact]
    public void Recon_sight_survives_recompute_and_save_but_expires_next_turn()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var plane = Add(sim, 0, 39, "military:bomber");
        var target = sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf)
            .First(h => sim.State.Grid.Distance(plane.Position, h) == 5);
        var sight = Service<IVisibilityService>(sim);
        Assert.DoesNotContain(0, sim.State.PlotAt(target).Visible);
        var command = new AirMissionCommand { Player = 0, Unit = plane.Id, Kind = AirMissionKind.Recon, Target = target };
        var hash = sim.ComputeHash();
        Assert.Null(command.Validate(sim)); Assert.Equal(hash, sim.ComputeHash());
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.Equal(0, plane.MovesLeft);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sight.Recompute(sim); sight.Recompute(loaded);
        Assert.Contains(0, sim.State.PlotAt(target).Visible);
        Assert.Contains(0, loaded.State.PlotAt(target).Visible);
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.DoesNotContain(0, sim.State.PlotAt(target).Visible);
        Assert.Contains(0, sim.State.PlotAt(target).Revealed);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Rebase_obeys_base_capacity_and_loads_onto_a_carrier()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var plane = Add(sim, 0, 39, "military:fighter");
        var target = sim.State.Grid.HexOf(new(41));
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = target, Population = 3 });
        var residents = Enumerable.Range(0, 8).Select(_ => Add(sim, 0, 41, "military:fighter")).ToArray();
        var command = new AirMissionCommand { Player = 0, Unit = plane.Id, Kind = AirMissionKind.Rebase, Target = target };
        var hash = sim.ComputeHash();
        Assert.NotNull(command.Validate(sim)); Assert.Equal(hash, sim.ComputeHash());
        sim.State.RemoveUnit(residents[0].Id);
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.Equal(target, plane.Position); Assert.Equal(0, plane.MovesLeft);
        Assert.NotNull(new SetUnitActivityCommand { Player = 0, Unit = plane.Id, Activity = UnitActivity.Intercept }.Validate(sim));
        Assert.Null(plane.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
        sim.AdvanceTurn();
        sim.State.Plots[42].Terrain = "world:coast";
        var carrier = Add(sim, 0, 42, "military:carrier");
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        command = new AirMissionCommand { Player = 0, Unit = plane.Id, Kind = AirMissionKind.Rebase, Target = carrier.Position };
        Assert.True(sim.TryApply(command, out error), error);
        Assert.True(loaded.TryApply(command, out error), error);
        Assert.Equal(carrier.Id, plane.Extensions.Get<MilitaryUnitState>(sim.Content)!.TransportId);
        Assert.Contains(plane.Id, Service<IUnitService>(sim).CargoUnits(sim, carrier.Id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        var units = Service<IUnitService>(sim);
        while (units.CargoUnits(sim, carrier.Id).Count < units.CargoCapacity(sim, carrier.Id))
        {
            var resident = Add(sim, 0, 42, "military:fighter");
            Assert.True(sim.TryApply(new LoadUnitCommand { Player = 0, Unit = resident.Id, Transport = carrier.Id }, out error), error);
        }
        var extra = Add(sim, 0, 41, "military:fighter");
        var full = new AirMissionCommand { Player = 0, Unit = extra.Id, Kind = AirMissionKind.Rebase, Target = carrier.Position };
        ulong before = sim.ComputeHash(); Assert.False(sim.TryApply(full, out _)); Assert.Equal(before, sim.ComputeHash());
    }

    [Fact]
    public void Aircraft_can_rebase_at_twice_their_range_but_not_one_hex_farther()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Map = new(40, 8, false);
        sim.State.Plots = Enumerable.Range(0, 320).Select(_ => new Plot { Terrain = "world:grass", Owner = 0 }).ToArray();
        sim.State.RebuildIndexes();
        var aircraft = Add(sim, 0, 0, "military:fighter");
        int range = Service<IAirMissionService>(sim).AirRange(sim, aircraft.Id);
        var boundary = sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf).First(h => sim.State.Grid.Distance(aircraft.Position, h) == range * 2);
        var tooFar = sim.State.Grid.Tiles.Select(sim.State.Grid.HexOf).First(h => sim.State.Grid.Distance(aircraft.Position, h) == range * 2 + 1);
        foreach (var hex in new[] { boundary, tooFar }) sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Position = hex });
        var rejected = new AirMissionCommand { Player = 0, Unit = aircraft.Id, Kind = AirMissionKind.Rebase, Target = tooFar };
        ulong hash = sim.ComputeHash(); Assert.False(sim.TryApply(rejected, out _)); Assert.Equal(hash, sim.ComputeHash());
        Assert.True(sim.TryApply(new AirMissionCommand { Player = 0, Unit = aircraft.Id, Kind = AirMissionKind.Rebase, Target = boundary }, out var error), error);
        Assert.Equal(boundary, aircraft.Position);
    }

    [Fact]
    public void Air_strike_stops_at_the_air_damage_limit_without_melee_retaliation()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var bomber = Add(sim, 0, 39, "military:bomber");
        var defender = Add(sim, 1, 40); defender.Health = 51;
        sim.State.SetWar(0, 1, true);
        var command = new AirMissionCommand { Player = 0, Unit = bomber.Id, Kind = AirMissionKind.AirStrike, Target = defender.Position };
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.True(loaded.TryApply(command, out error), error);
        Assert.Equal(50, defender.Health); Assert.Equal(100, bomber.Health);
        Assert.Equal(0, bomber.MovesLeft); Assert.NotNull(command.Validate(sim));
        var report = Assert.Single(sim.Events.Published.OfType<AirMissionResolved>()).Report;
        Assert.Equal(defender.Id, report.TargetUnit); Assert.Equal(1, report.TargetDamage); Assert.Null(report.Interceptor);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn();
        Assert.NotNull(command.Validate(sim)); // The target remains at the air damage limit.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Air_bomb_reduces_city_defense_or_pillages_an_improvement(bool cityTarget)
    {
        var sim = GameFactory.Scenario(mods.Content);
        var bomber = Add(sim, 0, 39, "military:bomber");
        var target = sim.State.Grid.HexOf(new(40)); var plot = sim.State.PlotAt(target); plot.Owner = 1;
        var city = new City { Id = sim.State.NextCityId++, Owner = 1, Position = target, Population = 3 };
        if (cityTarget) { city.Buildings.Add("cities:walls"); sim.State.AddCity(city); }
        else plot.Improvement = "world:improvement_improvement_woodcutter";
        sim.State.SetWar(0, 1, true);
        var command = new AirMissionCommand { Player = 0, Unit = bomber.Id, Kind = AirMissionKind.AirBomb, Target = target };
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.True(loaded.TryApply(command, out error), error);
        var report = Assert.Single(sim.Events.Published.OfType<AirMissionResolved>()).Report;
        if (cityTarget)
        {
            Assert.Equal(12, report.CityDefenseReduction); // 15 rate, reduced by the wall's 20% resistance.
            Assert.Equal(40, city.Extensions.Get<CityRuleState>(sim.Content)!.DefenseDamage);
            Assert.True(city.Extensions.Get<CityRuleState>(sim.Content)!.BombardedThisTurn);
        }
        else
        {
            Assert.Equal("world:improvement_improvement_woodcutter", report.DestroyedImprovement);
            Assert.Null(plot.Improvement); Assert.Equal(0, plot.ImprovementTurns);
        }
        Assert.Equal(0, bomber.MovesLeft); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Interception_aborts_the_mission_and_only_ground_defense_is_used_up(bool airPatrol, bool airBomb)
    {
        var sim = GameFactory.Scenario(mods.Content);
        var bomber = Add(sim, 0, 39, "military:bomber");
        var defender = Add(sim, 1, 40);
        var interceptor = Add(sim, 1, 40, airPatrol ? "military:fighter" : "military:sam_infantry");
        var plot = sim.State.PlotAt(defender.Position); plot.Owner = 1; plot.Improvement = "world:improvement_improvement_woodcutter";
        sim.State.SetWar(0, 1, true);
        if (airPatrol)
        {
            interceptor.Health = 50;
            interceptor.Promotions.UnionWith(["military:interception1", "military:interception2"]);
            Add(sim, 1, 40, "military:sam_infantry"); // The patrol has better odds than this ground defense.
            Assert.True(sim.TryApply(new SetUnitActivityCommand { Player = 1, Unit = interceptor.Id, Activity = UnitActivity.Intercept }, out var activityError), activityError);
        }
        // Pick a stream where the interception check succeeds; round outcomes remain unconstrained.
        int probability = airPatrol ? 42 : 30;
        Assert.Equal(probability, Service<IAirMissionService>(sim).InterceptionProbability(sim, interceptor.Id));
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            var rng = DeterministicRng.Restore(w, x, y, z); rng.Next(100);
            if (rng.Next(100) < probability) break;
            sim.Rng.NextUInt64();
        }
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var command = new AirMissionCommand { Player = 0, Unit = bomber.Id, Kind = airBomb ? AirMissionKind.AirBomb : AirMissionKind.AirStrike, Target = defender.Position };
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.True(loaded.TryApply(command, out error), error);
        var report = Assert.Single(sim.Events.Published.OfType<AirMissionResolved>()).Report;
        Assert.Equal(interceptor.Id, report.Interceptor); Assert.Equal(0, report.TargetDamage);
        Assert.Equal(100, defender.Health);
        Assert.Equal("world:improvement_improvement_woodcutter", plot.Improvement);
        Assert.Equal(!airPatrol, interceptor.Extensions.Get<MilitaryUnitState>(sim.Content)!.InterceptedThisTurn);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn();
        Assert.False(interceptor.Extensions.Get<MilitaryUnitState>(sim.Content)!.InterceptedThisTurn);
    }

    [Fact]
    public void Missile_evasion_bypasses_interception_and_a_successful_strike_consumes_the_missile()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var missile = Add(sim, 0, 39, "military:guided_missile");
        var defender = Add(sim, 1, 40);
        Add(sim, 1, 40, "military:sam_infantry");
        sim.State.SetWar(0, 1, true);
        var command = new AirMissionCommand { Player = 0, Unit = missile.Id, Kind = AirMissionKind.AirStrike, Target = defender.Position };
        Assert.NotNull(new AirMissionCommand { Player = 0, Unit = missile.Id, Kind = AirMissionKind.Recon, Target = defender.Position }.Validate(sim));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.True(loaded.TryApply(command, out error), error);
        var report = Assert.Single(sim.Events.Published.OfType<AirMissionResolved>()).Report;
        Assert.Null(report.Interceptor); Assert.True(report.TargetDamage > 0);
        Assert.Null(sim.State.FindUnit(missile.Id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void Submarines_do_not_block_or_wake_enemies_until_a_detector_is_in_range()
    {
        var sim = GameFactory.Scenario(mods.Content);
        foreach (var plot in sim.State.Plots) plot.Terrain = "world:coast";
        var hunter = Add(sim, 0, 39, "military:battleship"); var submarine = Add(sim, 1, 40, "military:submarine");
        sim.State.SetWar(0, 1, true);
        var sight = Service<IVisibilityService>(sim); var combat = Service<ICombatService>(sim);
        var session = new Civ.Client.GameSession(sim);
        Assert.Contains(0, sim.State.PlotAt(submarine.Position).Visible);
        Assert.False(sight.IsUnitVisible(sim, submarine.Id, 0)); Assert.True(sight.IsUnitVisible(sim, submarine.Id, 1));
        Assert.Null(combat.BestDefender(sim, hunter.Id, submarine.Position));
        Assert.DoesNotContain(session.VisibleUnits(), u => u.Id == submarine.Id);
        Assert.True(sim.TryApply(new SetUnitActivityCommand { Player = 0, Unit = hunter.Id, Activity = UnitActivity.Sentry }, out var error), error);
        sim.AdvanceTurn();
        Assert.Equal(UnitActivity.Sentry, hunter.Extensions.Get<MilitaryUnitState>(sim.Content)!.Activity);
        var detector = Add(sim, 0, 80, "military:destroyer");
        Assert.False(sight.IsUnitVisible(sim, submarine.Id, 0));
        sim.State.MoveUnit(detector.Id, sim.State.Grid.HexOf(new(41))); sight.Recompute(sim);
        Assert.True(sight.IsUnitVisible(sim, submarine.Id, 0)); Assert.Equal(submarine.Id, combat.BestDefender(sim, hunter.Id, submarine.Position));
        Assert.Contains(session.VisibleUnits(), u => u.Id == submarine.Id);
        Assert.NotNull(Service<IMovementService>(sim).CanEnter(sim, hunter.Id, new(39), new(40)));
        sim.AdvanceTurn(); Assert.Equal(UnitActivity.Awake, hunter.Extensions.Get<MilitaryUnitState>(sim.Content)!.Activity);
        sim.State.RemoveUnit(detector.Id); sight.Recompute(sim);
        var target = submarine.Position;
        Assert.True(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = hunter.Id, Path = [target] }, out error), error);
        Assert.Equal(target, hunter.Position); Assert.NotNull(sim.State.FindUnit(submarine.Id)); Assert.Empty(combat.History(sim));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Cavalry_flanks_siege_after_winning_or_withdrawing_but_not_inside_cities(bool withdraw, bool inCity)
    {
        var sim = GameFactory.Scenario(mods.Content);
        var cavalry = Add(sim, 0, 39, "military:cavalry"); var primary = Add(sim, 1, 40);
        var siege = Add(sim, 1, 40, "military:catapult"); var untouched = Add(sim, 1, 40);
        siege.Health = 1; sim.State.SetWar(0, 1, true);
        if (inCity) sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 1, Position = primary.Position });
        if (withdraw)
        {
            cavalry.Health = 1; cavalry.Promotions.UnionWith(["military:escape1", "military:escape2", "military:tactics"]);
        }
        else primary.Health = 1;
        var combat = Service<ICombatService>(sim); var preview = combat.Preview(sim, cavalry.Id, primary.Id);
        // Select the first lethal round and a successful flank; no combat outcome is mocked.
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State; var rng = DeterministicRng.Restore(w, x, y, z);
            bool defenderHits = rng.Next(preview.DieSides) < preview.DefenderRoundOdds;
            if (withdraw) rng.Next(100);
            if (defenderHits == withdraw && rng.Next(preview.DieSides) >= preview.DefenderRoundOdds * (100 - preview.WithdrawalPercent) / 100) break;
            sim.Rng.NextUInt64();
        }
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var report = combat.Resolve(sim, cavalry.Id, primary.Id); combat.Resolve(loaded, cavalry.Id, primary.Id);
        Assert.Equal(withdraw ? CombatOutcome.Withdrawn : CombatOutcome.AttackerWon, report.Outcome);
        if (inCity) { Assert.Empty(report.Flanking!); Assert.NotNull(sim.State.FindUnit(siege.Id)); }
        else { Assert.Contains(report.Flanking!, h => h.Unit == siege.Id && h.Damage > 0); Assert.Null(sim.State.FindUnit(siege.Id)); }
        Assert.Equal(100, untouched.Health); Assert.NotNull(sim.State.FindUnit(cavalry.Id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Amphibious_assault_penalty_is_removed_only_by_amphibious_training(bool trained)
    {
        var sim = GameFactory.Scenario(mods.Content); sim.State.Plots[39].Terrain = "world:coast";
        var ship = Add(sim, 0, 39, "military:cog"); var attacker = Add(sim, 0, 39); var defender = Add(sim, 1, 40);
        sim.State.SetWar(0, 1, true);
        Assert.True(sim.TryApply(new LoadUnitCommand { Player = 0, Unit = attacker.Id, Transport = ship.Id }, out var error), error);
        if (trained) attacker.Promotions.Add("military:amphibious");
        var preview = Service<ICombatService>(sim).Preview(sim, attacker.Id, defender.Id);
        Assert.Equal(trained ? 200 : 300, preview.DefenseStrength.Maximum);
        Assert.Null(Service<IMovementService>(sim).CanEnter(sim, attacker.Id, new(39), new(40), attack: true));
    }

    [Fact]
    public void Ranged_damage_stops_at_the_combat_limit()
    {
        var sim = GameFactory.Scenario(mods.Content); var siege = Add(sim, 0, 39, "military:catapult"); var defender = Add(sim, 1, 40);
        sim.State.SetWar(0, 1, true); defender.Health = 100 - MilitaryDataLimit(sim, siege) + 1;
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State;
            if (DeterministicRng.Restore(w, x, y, z).Next(100) < 70) break;
            sim.Rng.NextUInt64();
        }
        Assert.True(sim.TryApply(new RangedStrikeCommand { Player = 0, Unit = siege.Id, Target = defender.Position }, out var error), error);
        Assert.Equal(100 - MilitaryDataLimit(sim, siege), defender.Health); Assert.Equal(100, siege.Health);
    }

    [Fact]
    public void Surviving_suicide_missile_is_not_consumed_when_interception_aborts_its_strike()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var missile = Add(sim, 0, 39, "military:biological_warfare_missile");
        var defender = Add(sim, 1, 40); var secondary = Add(sim, 1, 40);
        var interceptor = Add(sim, 1, 40, "military:sam_infantry");
        sim.State.SetWar(0, 1, true);
        // Choose interception without lethal duel damage. Even five ground hits deal only 75 HP.
        while (true)
        {
            var (w, x, y, z) = sim.Rng.State; var rng = DeterministicRng.Restore(w, x, y, z);
            if (rng.Next(100) >= 65 && rng.Next(100) < 30) break;
            sim.Rng.NextUInt64();
        }
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var command = new AirMissionCommand { Player = 0, Unit = missile.Id, Kind = AirMissionKind.AirStrike, Target = defender.Position };
        Assert.True(sim.TryApply(command, out var error), error); Assert.True(loaded.TryApply(command, out error), error);
        var report = Assert.Single(sim.Events.Published.OfType<AirMissionResolved>()).Report;
        Assert.Equal(interceptor.Id, report.Interceptor); Assert.Equal(0, report.TargetDamage);
        Assert.InRange(missile.Health, 25, 100); Assert.NotNull(sim.State.FindUnit(missile.Id));
        Assert.Equal(100, defender.Health); Assert.Equal(100, secondary.Health);
        Assert.True(missile.Extensions.Get<MilitaryUnitState>(sim.Content)!.Attacked);
        Assert.True(interceptor.Extensions.Get<MilitaryUnitState>(sim.Content)!.InterceptedThisTurn);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData("cities:bunker", 80, false)]
    [InlineData("cities:flak_tower", 0, false)]
    [InlineData("cities:bunker", 80, true)]
    public void City_air_defenses_reduce_direct_and_collateral_damage_before_limits(string building, int percent, bool nearCollateralLimit)
    {
        var sim = GameFactory.Scenario(mods.Content);
        var bomber = Add(sim, 0, 39, "military:bomber");
        var defender = Add(sim, 1, 40, "military:infantry"); var secondary = Add(sim, 1, 40, "military:infantry");
        var city = new City { Id = sim.State.NextCityId++, Owner = 1, Position = defender.Position };
        sim.State.AddCity(city); sim.State.SetWar(0, 1, true);
        var protectedSim = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        protectedSim.State.CityById(city.Id).Buildings.Add(building);
        if (nearCollateralLimit) protectedSim.State.UnitById(secondary.Id).Health = 51;
        var replay = SaveGame.Load(sim.Content, SaveGame.Save(protectedSim));
        var command = new AirMissionCommand { Player = 0, Unit = bomber.Id, Kind = AirMissionKind.AirStrike, Target = defender.Position };
        Assert.True(sim.TryApply(command, out var error), error);
        Assert.True(protectedSim.TryApply(command, out error), error); Assert.True(replay.TryApply(command, out error), error);
        int direct = 100 - defender.Health, collateral = 100 - secondary.Health;
        Assert.True(direct > 0); Assert.True(collateral > 0);
        Assert.Equal(100 - direct * percent / 100, protectedSim.State.UnitById(defender.Id).Health);
        int initialHealth = nearCollateralLimit ? 51 : 100;
        int expectedCollateral = Math.Min(collateral * percent / 100, initialHealth - 50);
        Assert.Equal(initialHealth - expectedCollateral, protectedSim.State.UnitById(secondary.Id).Health);
        Assert.Equal(protectedSim.ComputeHash(), replay.ComputeHash());
    }

    private static int MilitaryDataLimit(Simulation sim, Unit unit) => sim.Content.Get<Civ.Content.Schema.UnitDef>(unit.Type).CombatLimit;
    [Fact]
    public void Defender_winning_on_its_own_plot_gets_domestic_experience_even_if_attacker_is_outside()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var a = Add(sim, 0, 39); var d = Add(sim, 1, 40);
        sim.State.SetWar(0, 1, true);
        sim.State.PlotAt(a.Position).Owner = null; sim.State.PlotAt(d.Position).Owner = 1;
        a.Health = 1;
        var baseline = d.Experience;
        var report = Service<ICombatService>(sim).Resolve(sim, a.Id, d.Id);
        Assert.Equal(CombatOutcome.DefenderWon, report.Outcome);
        Assert.True(d.Experience > baseline);
    }
    [Fact]
    public void Fortify_turns_only_accumulate_while_fortified()
    {
        var sim = GameFactory.Scenario(mods.Content);
        var idle = Add(sim, 0, 39); var fortified = Add(sim, 0, 40);
        Assert.True(sim.TryApply(new SetUnitActivityCommand { Player = 0, Unit = fortified.Id, Activity = UnitActivity.Fortify }, out var error), error);
        var enemy = Add(sim, 1, 41);
        sim.State.SetWar(0, 1, true);
        sim.AdvanceTurn(); sim.AdvanceTurn();
        var combat = Service<ICombatService>(sim);
        int idleDefense = combat.Preview(sim, enemy.Id, idle.Id).DefenseStrength.Current;
        int fortifiedDefense = combat.Preview(sim, enemy.Id, fortified.Id).DefenseStrength.Current;
        Assert.True(idleDefense > 0);
        Assert.True(fortifiedDefense > idleDefense);
    }
    [Fact]
    public void Naval_unit_on_water_upgrades_from_an_adjacent_coastal_city()
    {
        var sim = GameFactory.Scenario(mods.Content);
        sim.State.Plots[40].Terrain = "world:coast"; sim.State.Plots[41].Terrain = "world:coast";
        sim.State.Plots[40].AreaId = 5; sim.State.Plots[41].AreaId = 5;
        sim.State.AddCity(new City { Id = sim.State.NextCityId++, Owner = 0, Name = "Port", Position = sim.State.Grid.HexOf(new(39)), Population = 3 });
        var ship = Add(sim, 0, 41, "military:wargalley");
        sim.TeamOf(0).Techs.UnionWith(["research:rudder", "research:seafaring"]);
        sim.State.PlayerById(0).Treasury = 1000;
        var error = Service<IUnitService>(sim).Validate(sim, 0, ship.Id, UnitAction.Upgrade, "military:cog");
        Assert.NotEqual("No eligible nearby training city", error);
    }
}
