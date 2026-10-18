using Civ.Client;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

/// <summary>P7 payloads are built separately, never copied into the shared test mod directory.</summary>
public sealed class P7ModFixture
{
    public ContentDb Wonders { get; }
    public ContentDb Revolution { get; }
    public ContentDb Combined { get; }
    public P7ModFixture()
    {
        var sources = new LoadedMods().Sources;
        var wonders = Output("ScriptedWonders");
        var revolution = Output("Revolution");
        Wonders = ModLoader.Build(sources.Append(wonders));
        Revolution = ModLoader.Build(sources.Append(revolution));
        Combined = ModLoader.Build(sources.Append(wonders).Append(revolution));
    }
    private static ModSource Output(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string configuration = dir.Parent!.Name;
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Civ.slnx"))) dir = dir.Parent;
        return ModLoader.FromDirectory(Path.Combine(dir!.FullName, "src", "Mods", name, "bin", configuration, "net10.0"));
    }
    public static Simulation Scenario(ContentDb content)
    {
        var sim = GameFactory.Scenario(content);
        GameFactory.NeutralPolitics(sim);
        return sim;
    }
    public static City City(Simulation sim, int tile = 40, int owner = 0, int population = 3)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner,
            Position = sim.State.Grid.HexOf(new TileId(tile)), Population = population };
        sim.State.AddCity(city);
        return city;
    }
    public static Unit Unit(Simulation sim, City city, string type = "military:warrior", int? owner = null)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner ?? city.Owner,
            Position = city.Position, Type = type };
        sim.State.AddUnit(unit);
        sim.Events.Publish(new UnitCreated(unit.Id, unit.Owner, type, unit.Position));
        return unit;
    }
}

public sealed class ScriptedWonderTests(P7ModFixture mods) : IClassFixture<P7ModFixture>
{
    private static IScriptedWonderService Service(Simulation sim) => sim.Content.Services.Get<IScriptedWonderService>().Single();
    private static void Complete(Simulation sim, City city, string building)
    {
        city.Buildings.Add(building);
        sim.Events.Publish(new BuildingCompleted(city.Id, building));
    }
    private static void Tick(Simulation sim) => sim.Content.TurnSystems.Get("scripted_wonders:city_turns").Run(sim);

    [Theory]
    [InlineData("foundation:gamespeed_normal", 19)]
    [InlineData("foundation:gamespeed_quick", 14)]
    [InlineData("foundation:gamespeed_marathon", 39)]
    public void Crusade_musters_on_completion_then_only_after_full_speed_scaled_intervals(string speed, int interval)
    {
        var original = P7ModFixture.Scenario(mods.Wonders);
        var settings = original.Settings with { GameSpeed = speed };
        // Settings are immutable; create a normal game at the selected speed, then reuse the controlled map.
        var sim = new Simulation(mods.Wonders, settings);
        sim.State.Map = original.State.Map; sim.State.Plots = original.State.Plots;
        sim.State.Units.Clear(); sim.State.RebuildIndexes();
        var city = P7ModFixture.City(sim);
        Complete(sim, city, "cities:crusade");
        Assert.Single(sim.State.Units);
        Tick(sim); Tick(sim); // Completion turn and duplicate processing cannot advance the timer.
        Assert.Equal(0, Service(sim).DescribeCity(sim, city.Id).Single().ElapsedTurns);
        for (int i = 1; i < interval; i++) { sim.State.Turn++; Tick(sim); }
        Assert.Single(sim.State.Units);
        sim.State.Turn++; Tick(sim);
        Assert.Equal(2, sim.State.Units.Count);
        Assert.All(sim.State.Units, u =>
        {
            Assert.Equal("military:unit_unit_crusader", u.Type);
            Assert.Equal(city.Position, u.Position); Assert.Equal(city.Owner, u.Owner);
            Assert.True(u.MovesLeft > 0);
        });
        Assert.Equal(0, Service(sim).DescribeCity(sim, city.Id).Single().ElapsedTurns);
        sim.TeamOf(0).Techs.Add("research:liberalism");
        for (int i = 0; i < interval * 2; i++) { sim.State.Turn++; Tick(sim); }
        Assert.Equal(2, sim.State.Units.Count);
        Assert.False(Service(sim).IsActive(sim, city.Id, "cities:crusade"));
    }

    [Fact]
    public void Completion_grants_are_exactly_once_and_empire_buildings_skip_existing_and_foreign_cities()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        var city = P7ModFixture.City(sim);
        var owned = P7ModFixture.City(sim, 43); owned.Buildings.Add("cities:granary");
        var foreign = P7ModFixture.City(sim, 70, 1);
        Complete(sim, city, "cities:terracotta");
        Assert.Equal(3, sim.State.Units.Count);
        Assert.Equal(100, city.Culture);
        Complete(sim, city, "cities:pyramid");
        string granary = sim.Content.Get<S.BuildingDef>("cities:granary").Name;
        Assert.DoesNotContain("cities:granary", city.Buildings);
        Assert.Contains(granary, new GameSession(sim).DescribeCity(city)!.Buildings);
        Assert.Single(owned.Buildings, b => b == "cities:granary");
        Assert.Single(new GameSession(sim).DescribeCity(owned)!.Buildings, b => b == granary);
        Assert.DoesNotContain("cities:granary", foreign.Buildings);
        Assert.DoesNotContain(granary, new GameSession(sim).DescribeCity(foreign)!.Buildings);
        var later = P7ModFixture.City(sim, 80);
        Assert.Contains(granary, new GameSession(sim).DescribeCity(later)!.Buildings);
        Assert.DoesNotContain("cities:granary", later.Buildings);
        Assert.Single(sim.Events.Published.OfType<ScriptedWonderEffectApplied>(), e => e.Effect == "free_building");
        var loaded = SaveGame.Load(mods.Wonders, SaveGame.Save(sim));
        var before = loaded.ComputeHash();
        loaded.Events.Publish(new BuildingCompleted(city.Id, "cities:terracotta"));
        loaded.Events.Publish(new BuildingCompleted(city.Id, "cities:pyramid"));
        Assert.Equal(before, loaded.ComputeHash());
        Assert.Equal(before, sim.ComputeHash());
        var loadedCity = loaded.State.CityById(city.Id);
        loadedCity.Buildings.Remove("cities:pyramid");
        Assert.DoesNotContain(granary, new GameSession(loaded).DescribeCity(loadedCity)!.Buildings);
        Assert.DoesNotContain(granary, new GameSession(loaded).DescribeCity(loaded.State.CityById(later.Id))!.Buildings);
        Assert.Contains(granary, new GameSession(loaded).DescribeCity(loaded.State.CityById(owned.Id))!.Buildings);
    }

    [Fact]
    public void Leonardo_reduces_real_upgrade_prices_and_obsolescence_removes_the_discount()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        var city = P7ModFixture.City(sim);
        var unit = P7ModFixture.Unit(sim, city);
        var service = sim.Content.Services.Get<IUnitService>().Single();
        long full = service.UpgradePrice(sim, unit.Id, "military:swordsman");
        Assert.True(full > 0);
        Complete(sim, city, "cities:leonardo");
        Assert.Equal(full - full * 50 / 100, service.UpgradePrice(sim, unit.Id, "military:swordsman"));
        sim.TeamOf(0).Techs.Add("research:particle_physics");
        Assert.Equal(full, service.UpgradePrice(sim, unit.Id, "military:swordsman"));
    }

    [Fact]
    public void Great_wall_changes_real_combat_strength_only_for_barbarian_land_units_inside_protected_borders()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        var city = P7ModFixture.City(sim);
        var attacker = P7ModFixture.Unit(sim, city, owner: 1);
        sim.State.PlayerById(1).Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true });
        var defender = P7ModFixture.Unit(sim, city);
        sim.State.MoveUnit(attacker.Id, sim.State.Grid.HexOf(new TileId(41)));
        var combat = sim.Content.Services.Get<ICombatService>().Single();
        var baseline = combat.Preview(sim, attacker.Id, defender.Id).AttackStrength.Maximum;
        Complete(sim, city, "cities:great_wall");
        Assert.True(combat.Preview(sim, attacker.Id, defender.Id).AttackStrength.Maximum < baseline);
        sim.State.PlotAt(attacker.Position).Owner = 1;
        Assert.Equal(baseline, combat.Preview(sim, attacker.Id, defender.Id).AttackStrength.Maximum);
        sim.State.PlotAt(attacker.Position).Owner = 0;
        sim.TeamOf(0).Techs.Add("research:artillery");
        Assert.Equal(baseline, combat.Preview(sim, attacker.Id, defender.Id).AttackStrength.Maximum);
    }

    [Fact]
    public void Art_of_war_awards_experience_through_actual_combat_and_only_to_the_owner()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        var city = P7ModFixture.City(sim);
        var attacker = P7ModFixture.Unit(sim, city, "military:swordsman");
        var defender = P7ModFixture.Unit(sim, city, owner: 1);
        defender.Health = 1;
        sim.State.MoveUnit(attacker.Id, sim.State.Grid.HexOf(new TileId(41)));
        sim.State.SetWar(0, 1, true);
        var baseline = SaveGame.Load(mods.Wonders, SaveGame.Save(sim));
        Complete(sim, city, "cities:art_of_war");
        var outcome = sim.Content.Services.Get<ICombatService>().Single().Resolve(sim, attacker.Id, defender.Id);
        var plain = baseline.Content.Services.Get<ICombatService>().Single().Resolve(baseline, attacker.Id, defender.Id);
        Assert.Equal(CombatOutcome.AttackerWon, outcome.Outcome);
        Assert.Equal(plain.Outcome, outcome.Outcome);
        Assert.Equal(baseline.State.UnitById(attacker.Id).Experience + 2, attacker.Experience);
        Assert.Single(sim.Events.Published.OfType<ScriptedWonderEffectApplied>(), e => e.Effect == "victory_experience");
    }

    [Fact]
    public void Art_of_war_grants_no_experience_when_the_loser_survives()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        var city = P7ModFixture.City(sim);
        var attacker = P7ModFixture.Unit(sim, city, "military:swordsman");
        var defender = P7ModFixture.Unit(sim, city, owner: 1);
        Complete(sim, city, "cities:art_of_war");
        int before = attacker.Experience;
        // A damaging attack leaves the defender alive, so no victory experience is due.
        sim.Events.Publish(new CombatResolved(attacker.Id, defender.Id, attacker.Id));
        Assert.Equal(before, attacker.Experience);
        Assert.DoesNotContain(sim.Events.Published.OfType<ScriptedWonderEffectApplied>(), e => e.Effect == "victory_experience");
    }

    [Fact]
    public void Team_wonders_reach_teammates_but_not_other_teams()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        sim.State.Players.Add(new Player { Id = 2, TeamId = 0, Treasury = 0 });
        sim.State.Teams.Single(t => t.Id == 0).Members.Add(2);
        sim.State.RebuildIndexes();
        var wonderCity = P7ModFixture.City(sim, 40, owner: 2);
        Complete(sim, wonderCity, "cities:art_of_war");
        var teammate = P7ModFixture.Unit(sim, wonderCity, "military:swordsman", owner: 0);
        var fallen = P7ModFixture.Unit(sim, wonderCity, owner: 1);
        sim.State.RemoveUnit(fallen.Id);
        sim.Events.Publish(new CombatResolved(teammate.Id, fallen.Id, teammate.Id));
        Assert.Equal(2, teammate.Experience);

        var rival = P7ModFixture.Unit(sim, wonderCity, "military:swordsman", owner: 1);
        var defeated = P7ModFixture.Unit(sim, wonderCity, owner: 0);
        sim.State.RemoveUnit(defeated.Id);
        sim.Events.Publish(new CombatResolved(rival.Id, defeated.Id, rival.Id));
        Assert.Equal(0, rival.Experience);
    }

    [Fact]
    public void Wonder_finished_while_obsolete_grants_its_completion_once_it_turns_active()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        var city = P7ModFixture.City(sim);
        int units = sim.State.Units.Count;
        sim.TeamOf(0).Techs.Add("research:liberalism"); // Obsoletes King Richard's Crusade, so the wonder is inactive.
        Complete(sim, city, "cities:crusade");
        Tick(sim);
        Assert.Equal(units, sim.State.Units.Count);
        sim.TeamOf(0).Techs.Remove("research:liberalism");
        Tick(sim);
        Assert.Equal(units + 1, sim.State.Units.Count);
        Tick(sim);
        Assert.Equal(units + 1, sim.State.Units.Count);
        Assert.Single(sim.Events.Published.OfType<ScriptedWonderEffectApplied>(), e => e.Effect == "units");
    }

    [Fact]
    public void Mid_interval_save_and_full_turn_replay_preserve_hashes_and_queries_are_pure()
    {
        var sim = P7ModFixture.Scenario(mods.Combined);
        var city = P7ModFixture.City(sim);
        Complete(sim, city, "cities:crusade");
        for (int i = 0; i < 9; i++) sim.AdvanceTurn();
        var before = sim.ComputeHash();
        Assert.True(Service(sim).DescribeCity(sim, city.Id).Single().ElapsedTurns > 0);
        Assert.Equal(before, sim.ComputeHash());
        var peer = SaveGame.Load(mods.Combined, SaveGame.Save(sim));
        Assert.Equal(before, peer.ComputeHash());
        for (int i = 0; i < 25; i++)
        {
            sim.AdvanceTurn(); peer.AdvanceTurn();
            Assert.Equal(sim.ComputeHash(), peer.ComputeHash());
        }
        Assert.True(sim.State.Units.Count(u => u.Type == "military:unit_unit_crusader") >= 2);
    }

    [Fact]
    public void Art_of_war_victory_experience_respects_the_barbarian_cap()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        var city = P7ModFixture.City(sim);
        sim.State.PlayerById(1).Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true });
        Complete(sim, city, "cities:art_of_war");
        var unit = P7ModFixture.Unit(sim, city, "military:swordsman");
        unit.Experience = 40;
        var fallen = P7ModFixture.Unit(sim, city, owner: 1);
        sim.State.RemoveUnit(fallen.Id);
        sim.Events.Publish(new CombatResolved(unit.Id, fallen.Id, unit.Id, 1));
        Assert.Equal(40, unit.Experience);
        unit.Experience = 39;
        sim.Events.Publish(new CombatResolved(unit.Id, fallen.Id, unit.Id, 1));
        Assert.Equal(40, unit.Experience);
    }

    [Fact]
    public void Great_wall_blocks_barbarian_entry_and_weakens_cross_border_attackers()
    {
        var sim = P7ModFixture.Scenario(mods.Wonders);
        var city = P7ModFixture.City(sim);
        sim.State.PlotAt(city.Position).Owner = 0;
        var target = sim.State.Grid.HexOf(new TileId(41));
        sim.State.PlotAt(target).Owner = 0;
        var outside = new TileId(42);
        sim.State.PlotAt(sim.State.Grid.HexOf(outside)).Owner = null;
        var raider = P7ModFixture.Unit(sim, city, owner: 1);
        sim.State.PlayerById(1).Extensions.Set(sim.Content, new MilitaryPlayerState { Barbarian = true });
        sim.State.MoveUnit(raider.Id, sim.State.Grid.HexOf(outside));
        sim.State.SetWar(0, 1, true);
        var movement = sim.Content.Services.Get<IMovementService>().Single();
        var from = sim.State.Grid.TileAt(raider.Position);
        Assert.Null(movement.CanEnter(sim, raider.Id, from, new TileId(41)));
        var defender = P7ModFixture.Unit(sim, city);
        sim.State.MoveUnit(defender.Id, target);
        var combat = sim.Content.Services.Get<ICombatService>().Single();
        long baseline = combat.Preview(sim, raider.Id, defender.Id).AttackStrength.Maximum;
        Complete(sim, city, "cities:great_wall");
        Assert.NotNull(movement.CanEnter(sim, raider.Id, from, new TileId(41)));
        Assert.True(combat.Preview(sim, raider.Id, defender.Id).AttackStrength.Maximum < baseline);
    }
}
