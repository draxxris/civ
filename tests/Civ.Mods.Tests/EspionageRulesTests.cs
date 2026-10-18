using Civ.Content;
using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class EspionageRulesTests
{
    private static IEspionageService Spies(Simulation sim) => P6RulesScenario.Service<IEspionageService>(sim);
    private static EspionageTeamState TeamState(Simulation sim, int team)
    {
        var state = sim.State.TeamById(team).Extensions.Get<EspionageTeamState>(sim.Content) ?? new();
        sim.State.TeamById(team).Extensions.Set(sim.Content, state); return state;
    }
    private static void Fund(Simulation sim, int team = 0, int target = 1, long points = 100000) => TeamState(sim, team).Points[target] = points;
    private static Unit Spy(Simulation sim) => P6RulesScenario.AddUnit(sim, 0, sim.State.Cities[2], "military:spy");
    private static ExecuteEspionageMissionCommand Mission(Unit spy, City city, EspionageMissionKind mission, string? tech = null) => new() { Player = spy.Owner, Unit = spy.Id, City = city.Id, Mission = mission, Tech = tech };

    [Fact]
    public void Commerce_is_allocated_only_to_known_teams_and_weights_conserve_points_across_save_load()
    {
        var sim = P6RulesScenario.Create();
        sim.State.Players.Add(new Player { Id = 2, TeamId = 2, Treasury = 10000 });
        sim.State.Teams.Add(new Team { Id = 2, Members = new() { 2 } }); sim.State.RebuildIndexes();
        P6RulesScenario.AddCity(sim, 2, 80);
        var economy = P6RulesScenario.Service<IPlayerEconomyService>(sim);
        economy.SetRates(sim, 0, new(0, 0, 0, 100));
        Assert.Equal(1, Spies(sim).Weight(sim, 0, 1));
        Assert.Contains("known", P6RulesScenario.Reject(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 2, Weight = 3 }));
        ulong beforeForecast = sim.ComputeHash();
        var forecast = economy.Calculate(sim, 0);
        Assert.Equal(beforeForecast, sim.ComputeHash());
        long generated = forecast.CommerceHundredths.Espionage;
        Assert.Equal(generated / 100, Assert.Single(forecast.Ledger.Lines, l => l.Source == "economy:espionage_target:1").Amount);
        Assert.DoesNotContain(forecast.Ledger.Lines, l => l.Source == "economy:espionage_target:2");
        Assert.True(generated > 0);
        sim.AdvanceTurn();
        Assert.Equal(generated / 100, Spies(sim).Points(sim, 0, 1)); Assert.Equal(0, Spies(sim).Points(sim, 0, 2));
        var settled = sim.State.PlayerById(0).Extensions.Get<EconomyPlayerState>(sim.Content)!;
        Assert.Equal(0, settled.EspionageHundredths);
        Assert.Equal(generated, settled.SettledCommerceHundredths.Espionage);
        P6RulesScenario.Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 2);
        P6RulesScenario.Apply(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 2, Weight = 3 });
        long previous = Spies(sim).Points(sim, 0, 1);
        generated = economy.Calculate(sim, 0).CommerceHundredths.Espionage + sim.State.PlayerById(0).Extensions.Get<EspionagePlayerState>(sim.Content)!.RemainderHundredths;
        var allocations = economy.Calculate(sim, 0).Ledger.Lines.Where(l => l.Key == "economy:espionage_spending").ToDictionary(l => l.Source, l => l.Amount);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(allocations["economy:espionage_target:1"], Spies(sim).Points(sim, 0, 1) - previous);
        Assert.Equal(allocations["economy:espionage_target:2"], Spies(sim).Points(sim, 0, 2));
        long points = generated / 100;
        Assert.Equal(points, Spies(sim).Points(sim, 0, 1) - previous + Spies(sim).Points(sim, 0, 2));
        // The larger fractional remainder gets the indivisible point; ties go to the lower team id.
        long target1 = points / 4 + (points % 4 is 1 or 2 ? 1 : 0);
        Assert.Equal(target1, Spies(sim).Points(sim, 0, 1) - previous); Assert.Equal(points - target1, Spies(sim).Points(sim, 0, 2));
        Assert.Equal(3, Spies(loaded).Weight(loaded, 0, 2));
        P6RulesScenario.Apply(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 1, Weight = 0 });
        P6RulesScenario.Apply(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 2, Weight = 0 });
        long before = Spies(sim).Points(sim, 0, 1) + Spies(sim).Points(sim, 0, 2);
        sim.AdvanceTurn(); Assert.Equal(before, Spies(sim).Points(sim, 0, 1) + Spies(sim).Points(sim, 0, 2));
        Assert.True(sim.State.PlayerById(0).Extensions.Get<EspionagePlayerState>(sim.Content)!.RemainderHundredths >= 100);
        P6RulesScenario.Apply(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 1, Weight = 1 });
        sim.AdvanceTurn(); Assert.True(Spies(sim).Points(sim, 0, 1) + Spies(sim).Points(sim, 0, 2) > before);
    }
    [Theory]
    [InlineData(PassiveEspionageKind.Demographics, 50)]
    [InlineData(PassiveEspionageKind.CityVisibility, 100)]
    [InlineData(PassiveEspionageKind.InspectResearch, 150)]
    [InlineData(PassiveEspionageKind.InvestigateCity, 200)]
    public void Passive_effects_use_live_balances_and_bilateral_spending_ratio(PassiveEspionageKind kind, long baseline)
    {
        var sim = P6RulesScenario.Create();
        // No investment on either side is neutral: the threshold is the baseline, and empty balances grant nothing.
        ulong hash = sim.ComputeHash();
        Assert.Equal(baseline, Spies(sim).PassiveThreshold(sim, 0, 1, kind));
        Assert.False(Spies(sim).HasPassiveEffect(sim, 0, 1, kind)); Assert.Equal(hash, sim.ComputeHash());
        // Live points count as own investment. At twice the baseline the ratio reaches its 50% floor, so half the baseline qualifies.
        Fund(sim, points: baseline * 2);
        Assert.Equal(baseline / 2, Spies(sim).PassiveThreshold(sim, 0, 1, kind));
        Assert.True(Spies(sim).HasPassiveEffect(sim, 0, 1, kind));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim)); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.True(Spies(loaded).HasPassiveEffect(loaded, 0, 1, kind));
        // Rival spending raises the threshold: team 1 spent 300 against team 0, a 175% ratio, with no points on either side.
        var rival = P6RulesScenario.Create(); TeamState(rival, 1).Spent[0] = 300;
        Assert.Equal(baseline * 175 / 100, Spies(rival).PassiveThreshold(rival, 0, 1, kind));
        Assert.False(Spies(rival).HasPassiveEffect(rival, 0, 1, kind));
    }
    [Fact]
    public void Ratio_ignores_investment_against_other_teams()
    {
        var sim = P6RulesScenario.Create();
        TeamState(sim, 0).Spent[2] = 1000; // Team 0 spends on team 2, so the 0-1 ratio stays neutral.
        Assert.Equal(50, Spies(sim).PassiveThreshold(sim, 0, 1, PassiveEspionageKind.Demographics));
    }
    [Fact]
    public void Eliminated_teams_stop_receiving_espionage_points()
    {
        var sim = P6RulesScenario.Create();
        sim.State.Players.Add(new Player { Id = 2, TeamId = 2, Treasury = 10000 });
        sim.State.Teams.Add(new Team { Id = 2, Members = new() { 2 } }); sim.State.RebuildIndexes();
        var colony = P6RulesScenario.AddCity(sim, 2, 80);
        P6RulesScenario.Service<IPlayerEconomyService>(sim).SetRates(sim, 0, new(0, 0, 0, 100));
        P6RulesScenario.Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 2);
        P6RulesScenario.Apply(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 2, Weight = 3 });
        P6RulesScenario.Apply(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 1, Weight = 1 });
        for (int turn = 0; turn < 5; turn++) sim.AdvanceTurn();
        long colonyPoints = Spies(sim).Points(sim, 0, 2), mainPoints = Spies(sim).Points(sim, 0, 1);
        Assert.True(colonyPoints > 0);
        sim.State.RemoveCity(colony.Id); // Team 2 keeps its weight but has no living member.
        for (int turn = 0; turn < 10; turn++) sim.AdvanceTurn();
        Assert.Equal(colonyPoints, Spies(sim).Points(sim, 0, 2));
        Assert.True(Spies(sim).Points(sim, 0, 1) > mainPoints);
    }

    [Fact]
    public void Mission_rejections_leave_state_and_rng_unchanged()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[2]; var spy = Spy(sim);
        var command = Mission(spy, city, EspionageMissionKind.StealTreasury);
        Assert.Contains("points", P6RulesScenario.Reject(sim, command)); Fund(sim);
        spy.MovesLeft = 0; Assert.Contains("moves", P6RulesScenario.Reject(sim, command)); spy.MovesLeft = 60;
        sim.State.MoveUnit(spy.Id, sim.State.Cities[0].Position);
        Assert.Contains("target city", P6RulesScenario.Reject(sim, command));
        Assert.Contains("rival", P6RulesScenario.Reject(sim, Mission(spy, sim.State.Cities[0], EspionageMissionKind.StealTreasury)));
        sim.State.MoveUnit(spy.Id, city.Position);
        Assert.Contains("production", P6RulesScenario.Reject(sim, Mission(spy, city, EspionageMissionKind.SabotageProduction)));
        Assert.Contains("Choose", P6RulesScenario.Reject(sim, Mission(spy, city, EspionageMissionKind.StealTech)));
        sim.TeamOf(1).Techs.Add("research:assembly_line");
        Assert.Contains("prerequisites", P6RulesScenario.Reject(sim, Mission(spy, city, EspionageMissionKind.StealTech, "research:assembly_line")));
        var warrior = P6RulesScenario.AddUnit(sim, 0, city, "military:warrior");
        Assert.Contains("spy", P6RulesScenario.Reject(sim, Mission(warrior, city, EspionageMissionKind.StealTreasury)));
        Assert.Contains("0..10000", P6RulesScenario.Reject(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 1, Weight = -1 }));
    }
    [Theory]
    [InlineData(EspionageMissionKind.StealTreasury)]
    [InlineData(EspionageMissionKind.SabotageProduction)]
    [InlineData(EspionageMissionKind.InciteRevolt)]
    [InlineData(EspionageMissionKind.FomentUnhappiness)]
    [InlineData(EspionageMissionKind.StealTech)]
    public void Missions_apply_effects_and_return_spies_and_resume_identically(EspionageMissionKind mission)
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[2]; var spy = Spy(sim); Fund(sim);
        const string tech = "research:agriculture";
        sim.TeamOf(1).Techs.Add(tech);
        city.ProductionQueue.Add(new("cities:unit", "military:warrior", 100));
        // Explicit palace selection takes precedence over the first owned city.
        sim.State.Cities[1].Buildings.Add("cities:palace");
        long ownerGold = sim.State.PlayerById(0).Treasury, victimGold = sim.State.PlayerById(1).Treasury;
        int unhappy = P6RulesScenario.Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Unhappy;
        var command = Mission(spy, city, mission, mission == EspionageMissionKind.StealTech ? tech : null);
        long cost = Spies(sim).MissionCost(sim, 0, city.Id, mission, command.Tech);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        P6RulesScenario.Apply(sim, command); P6RulesScenario.Apply(loaded, command);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.True(sim.Events.Published.OfType<EspionageMissionResult>().Single().Success);
        Assert.Equal(100000 - cost, Spies(sim).Points(sim, 0, 1));
        Assert.Equal(sim.State.Cities[1].Position, sim.State.UnitById(spy.Id).Position); Assert.Equal(0, sim.State.UnitById(spy.Id).MovesLeft);
        switch (mission)
        {
            case EspionageMissionKind.StealTreasury:
                Assert.Equal(victimGold * 3 / 4, sim.State.PlayerById(1).Treasury); Assert.Equal(ownerGold + victimGold / 4, sim.State.PlayerById(0).Treasury); break;
            case EspionageMissionKind.SabotageProduction: Assert.Equal(50, city.ProductionQueue[0].Progress); break;
            case EspionageMissionKind.InciteRevolt: Assert.True(GovernmentStatus.IsDisorder(sim, city)); Assert.Equal(0, P6RulesScenario.Service<ICityYieldService>(sim).Calculate(sim, city.Id).Yields.Commerce); break;
            case EspionageMissionKind.FomentUnhappiness: Assert.Equal(unhappy + 3, P6RulesScenario.Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Unhappy); break;
            case EspionageMissionKind.StealTech: Assert.Contains(tech, sim.TeamOf(0).Techs); Assert.Contains(sim.Events.Published.OfType<TechDiscovered>(), e => e.Team == 0 && e.Tech == tech); break;
        }
        loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim)); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        long production = city.ProductionQueue[0].Progress;
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        if (mission == EspionageMissionKind.InciteRevolt) { Assert.False(GovernmentStatus.IsDisorder(sim, city)); Assert.Equal(production, city.ProductionQueue[0].Progress); }
        if (mission == EspionageMissionKind.FomentUnhappiness)
        {
            sim.AdvanceTurn(); sim.AdvanceTurn();
            Assert.DoesNotContain(ModifierSink.Collect(new(sim, ModifierScope.City, city.Owner, city)).Ledger.Lines, l => l.Source == "espionage:foment_unhappiness");
        }
    }
    [Theory]
    [InlineData(EspionageMissionKind.StealTreasury)]
    [InlineData(EspionageMissionKind.CounterEspionage)]
    public void Detection_consumes_the_spy_and_spends_points_without_applying_the_mission(EspionageMissionKind mission)
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[2]; Fund(sim);
        for (int i = 0; i < 6; i++) P6RulesScenario.AddUnit(sim, 1, city, "military:spy");
        bool caught = false;
        for (int attempt = 0; attempt < 32 && !caught; attempt++)
        {
            var spy = Spy(sim); var command = Mission(spy, city, mission);
            long gold = sim.State.PlayerById(1).Treasury, points = Spies(sim).Points(sim, 0, 1);
            long cost = Spies(sim).MissionCost(sim, 0, city.Id, command.Mission);
            var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
            P6RulesScenario.Apply(sim, command); P6RulesScenario.Apply(loaded, command);
            Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
            Assert.Equal(points - cost, Spies(sim).Points(sim, 0, 1));
            caught = !sim.Events.Published.OfType<EspionageMissionResult>().Last().Success;
            if (caught) { Assert.Null(sim.State.FindUnit(spy.Id)); Assert.Equal(gold, sim.State.PlayerById(1).Treasury); }
        }
        Assert.True(caught);
        Assert.Null(Spies(sim).CounterEspionage(sim, 0, 1));
    }

    [Theory]
    [InlineData("foundation:gamespeed_normal", 84, 8)]
    [InlineData("foundation:gamespeed_quick", 63, 6)]
    [InlineData("foundation:gamespeed_marathon", 168, 16)]
    public void Counter_espionage_has_one_team_expiry_and_survives_save_load(string speed, long cost, int duration)
    {
        var content = P6RulesScenario.Create().Content;
        var sim = Simulation.CreateScenario(content, GameFactory.Settings() with { GameSpeed = speed }, new(12, 8, true),
            Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray());
        GameFactory.NeutralPolitics(sim);
        var home = P6RulesScenario.AddCity(sim, 0, 14);
        var city = P6RulesScenario.AddCity(sim, 1, 57);
        P6RulesScenario.Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 1);
        Fund(sim); Fund(sim, 1, 0);
        Assert.Null(Spies(sim).CounterEspionage(sim, 0, 1));
        var spy = P6RulesScenario.AddUnit(sim, 0, city, "military:spy");
        var command = Mission(spy, city, EspionageMissionKind.CounterEspionage);
        Assert.Equal(cost, Spies(sim).MissionCost(sim, 0, city.Id, command.Mission));
        int expires = sim.State.Turn + duration;
        P6RulesScenario.Apply(sim, command);
        Assert.True(sim.Events.Published.OfType<EspionageMissionResult>().Single().Success);
        Assert.Equal(home.Position, spy.Position); Assert.Equal(0, spy.MovesLeft);
        Assert.Equal(100000 - cost, Spies(sim).Points(sim, 0, 1));
        Assert.Equal(new CounterEspionageEffect(expires, 200), Spies(sim).CounterEspionage(sim, 0, 1));
        Assert.Null(Spies(sim).CounterEspionage(sim, 1, 0));
        // A second member sees the same team effect, even before that member's turn ends.
        sim.State.Players.Add(new Player { Id = 2, TeamId = 0, Treasury = 10000 });
        sim.TeamOf(0).Members.Add(2); sim.State.RebuildIndexes();
        P6RulesScenario.AddCity(sim, 2, 18);
        var teammateSpy = P6RulesScenario.AddUnit(sim, 2, city, "military:spy");
        var repeat = Mission(teammateSpy, city, command.Mission);
        Assert.Contains("already active", P6RulesScenario.Reject(sim, repeat));
        var loaded = SaveGame.Load(content, SaveGame.Save(sim));
        Assert.Equal(new CounterEspionageEffect(expires, 200), Spies(loaded).CounterEspionage(loaded, 0, 1));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        TeamState(loaded, 0).CounterEspionage[1] = new(expires + 1, 200);
        Assert.NotEqual(sim.ComputeHash(), loaded.ComputeHash());
        TeamState(loaded, 0).CounterEspionage[1] = new(expires, 201);
        Assert.NotEqual(sim.ComputeHash(), loaded.ComputeHash());
        TeamState(loaded, 0).CounterEspionage[1] = new(expires, 200);
        for (int turn = 0; turn < duration - 1; turn++)
        {
            sim.AdvanceTurn(); loaded.AdvanceTurn();
            Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        }
        Assert.Equal(expires - 1, sim.State.Turn);
        Assert.NotNull(Spies(sim).CounterEspionage(sim, 0, 1));
        Assert.Contains("already active", P6RulesScenario.Reject(sim, repeat));
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(expires, sim.State.Turn);
        Assert.Null(Spies(sim).CounterEspionage(sim, 0, 1));
        Assert.Null(repeat.Validate(sim));
        Assert.Equal(50, Spies(sim).MissionCost(sim, 1, home.Id, EspionageMissionKind.StealTreasury));
    }

    [Fact]
    public void Counter_costs_use_defenders_directed_multiplier_and_round_the_modifier_before_the_cost()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[2]; var home = sim.State.Cities[1];
        Fund(sim, points: 104000); Fund(sim, 1, 0);
        var spy = Spy(sim); spy.Promotions.Add("military:security1"); spy.Health = 40;
        int chance = Spies(sim).SuccessChance(sim, 1, home.Id);
        P6RulesScenario.Apply(sim, Mission(spy, city, EspionageMissionKind.CounterEspionage));
        Assert.True(sim.Events.Published.OfType<EspionageMissionResult>().Single().Success);
        // A land spy's +10 interception gives 200 + 5*10 = 250%, without a health penalty.
        Assert.Equal(250, Spies(sim).CounterEspionage(sim, 0, 1)!.CostPercent);
        // Reverse ratio is floor(200*104050/204100) = 101; floor(101*250/100) = 252.
        Assert.Equal(201, Spies(sim).MissionCost(sim, 1, home.Id, EspionageMissionKind.SabotageProduction));
        Assert.Equal(504, Spies(sim).PassiveThreshold(sim, 1, 0, PassiveEspionageKind.InvestigateCity));
        Assert.Equal(78, Spies(sim).MissionCost(sim, 0, city.Id, EspionageMissionKind.SabotageProduction));
        Assert.Equal(chance, Spies(sim).SuccessChance(sim, 1, home.Id));
        // The unrelated team pair has no effect.
        sim.State.Players.Add(new Player { Id = 2, TeamId = 2 });
        sim.State.Teams.Add(new Team { Id = 2, Members = new() { 2 } }); sim.State.RebuildIndexes();
        var third = P6RulesScenario.AddCity(sim, 2, 80);
        Assert.Equal(80, Spies(sim).MissionCost(sim, 0, third.Id, EspionageMissionKind.SabotageProduction));
    }

    [Fact]
    public void Passive_permissions_are_revoked_when_the_target_team_is_eliminated()
    {
        var sim = P6RulesScenario.Create(); Fund(sim);
        Assert.True(Spies(sim).HasPassiveEffect(sim, 0, 1, PassiveEspionageKind.Demographics));
        foreach (var unit in sim.State.Units.Where(u => u.Owner == 1).ToArray()) sim.State.RemoveUnit(unit.Id);
        foreach (var city in sim.State.Cities.Where(c => c.Owner == 1).ToArray()) sim.State.RemoveCity(city.Id);
        Assert.False(Spies(sim).HasPassiveEffect(sim, 0, 1, PassiveEspionageKind.Demographics));
    }

    [Fact]
    public void Legacy_team_extension_without_counter_state_loads_with_no_protection()
    {
        var sim = P6RulesScenario.Create(); Fund(sim);
        var save = System.Text.Json.Nodes.JsonNode.Parse(SaveGame.Save(sim))!;
        var teams = save["State"]!["Teams"]!.AsArray();
        var state = teams.Single(t => (int)t!["Id"]! == 0)!["Extensions"]!["espionage:team_state"]!;
        Assert.True(state.AsObject().Remove("CounterEspionage"));
        var loaded = SaveGame.Load(sim.Content, save.ToJsonString());
        Assert.Equal(100000, Spies(loaded).Points(loaded, 0, 1));
        Assert.Null(Spies(loaded).CounterEspionage(loaded, 0, 1));
        Assert.Equal(4, (int)EspionageMissionKind.StealTech);
        Assert.Equal(5, (int)EspionageMissionKind.CounterEspionage);
    }
}
