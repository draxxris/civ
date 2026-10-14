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
        long generated = economy.Calculate(sim, 0).CommerceHundredths.Espionage;
        Assert.True(generated > 0);
        sim.AdvanceTurn();
        Assert.Equal(generated / 100, Spies(sim).Points(sim, 0, 1)); Assert.Equal(0, Spies(sim).Points(sim, 0, 2));
        P6RulesScenario.Service<IDiplomacyService>(sim).EstablishContact(sim, 0, 2);
        P6RulesScenario.Apply(sim, new SetEspionageWeightCommand { Player = 0, TargetTeam = 2, Weight = 3 });
        long previous = Spies(sim).Points(sim, 0, 1);
        generated = economy.Calculate(sim, 0).CommerceHundredths.Espionage + sim.State.PlayerById(0).Extensions.Get<EspionagePlayerState>(sim.Content)!.RemainderHundredths;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
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
        city.ProductionQueue.Add(new("military:unit", "military:warrior", 100));
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
    [Fact]
    public void Detection_consumes_the_spy_and_spends_points_without_applying_the_mission()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[2]; Fund(sim);
        for (int i = 0; i < 6; i++) P6RulesScenario.AddUnit(sim, 1, city, "military:spy");
        bool caught = false;
        for (int attempt = 0; attempt < 32 && !caught; attempt++)
        {
            var spy = Spy(sim); var command = Mission(spy, city, EspionageMissionKind.StealTreasury);
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
    }
}
