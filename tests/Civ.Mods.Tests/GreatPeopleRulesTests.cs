using System.Collections.Immutable;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class GreatPeopleRulesTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private Simulation Scenario()
    {
        var source = new ModSource { Manifest = new() { Id = "gp_probe", Version = "1", References = ["foundation", "military"] }, Root = mods.Root, Instance = new Probe() };
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(source)));
        GameFactory.NeutralPolitics(sim);
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(39)), Culture = 1 };
        sim.State.AddCity(city);
        city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true, AvoidGrowth = true });
        return sim;
    }
    private static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    private static Unit Unit(Simulation sim, string type = "gp_probe:sage", int owner = 0)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Type = type, Position = sim.State.Cities[0].Position };
        sim.State.AddUnit(unit); return unit;
    }
    private static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);
    [Fact]
    public void Historical_pools_and_birth_rng_resume_identically_and_threshold_includes_creator_team_increase()
    {
        var sim = Scenario(); var city = sim.State.Cities[0]; city.Buildings.Add("gp_probe:academy");
        var gp = Service<IGreatPeopleService>(sim);
        long threshold = gp.Threshold(sim, 0);
        sim.AdvanceTurn();
        Assert.Equal(80, gp.Calculate(sim, city.Id).Progress);
        Assert.Equal(80, gp.Calculate(sim, city.Id).ClassPoints["gp_probe:sage"]);
        city.Buildings.Clear(); city.Buildings.Add("gp_probe:forge");
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        for (int i = 0; i < 3; i++) { sim.AdvanceTurn(); loaded.AdvanceTurn(); }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Single(sim.State.Units);
        Assert.Equal(threshold * 2, gp.Threshold(sim, 0));
        Assert.Empty(gp.Calculate(sim, city.Id).ClassPoints);
        Assert.Single(sim.Events.PreviousTurn.OfType<GreatPersonBorn>().Concat(sim.Events.Published.OfType<GreatPersonBorn>()));
    }
    [Fact]
    public void Specialist_building_and_local_modifier_share_an_additive_ledger()
    {
        var sim = Scenario(); var city = sim.State.Cities[0];
        city.Buildings.AddRange(["gp_probe:academy", "gp_probe:inspiration"]);
        city.Specialists["gp_probe:settled_sage"] = 2;
        var result = Service<IGreatPeopleService>(sim).Calculate(sim, city.Id);
        Assert.Equal(126, result.Rate);
        Assert.Equal(84, result.Ledger.Total("great_people:base_rate"));
        Assert.Equal(50, result.Ledger.Total("cities:great_people_percent"));
    }
    [Fact]
    public void Disorder_preserves_progress_and_queries_do_not_change_state_or_rng()
    {
        var sim = Scenario(); var city = sim.State.Cities[0]; city.Buildings.Add("gp_probe:academy");
        city.Extensions.Get<CityRuleState>(sim.Content)!.Disorder = true;
        var gp = Service<IGreatPeopleService>(sim); ulong hash = sim.ComputeHash();
        Assert.Equal(0, gp.Calculate(sim, city.Id).Rate); Assert.Empty(gp.SettledSpecialists(sim, city.Id));
        Assert.Empty(gp.AvailableActions(sim, 0, -1)); Assert.Equal(hash, sim.ComputeHash());
        sim.AdvanceTurn(); Assert.Equal(0, gp.Calculate(sim, city.Id).Progress);
    }
    [Fact]
    public void Settlement_consumes_owned_unit_and_adds_specialist_commerce_and_gp_after_reload()
    {
        var sim = Scenario(); var city = sim.State.Cities[0]; var unit = Unit(sim);
        var before = Service<ICityYieldService>(sim).Calculate(sim, city.Id);
        Assert.False(sim.TryApply(new JoinGreatPersonCommand { Player = 1, UnitId = unit.Id }, out _));
        Assert.False(sim.TryApply(new JoinGreatPersonCommand { Player = 0, UnitId = unit.Id, Target = "foundation:specialist_citizen" }, out _));
        Apply(sim, new JoinGreatPersonCommand { Player = 0, UnitId = unit.Id });
        Assert.Null(sim.State.FindUnit(unit.Id));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(1, Service<IGreatPeopleService>(loaded).SettledSpecialists(loaded, city.Id)["gp_probe:settled_sage"]);
        Assert.True(Service<ICityYieldService>(loaded).Calculate(loaded, city.Id).DirectCommerceHundredths.Research > before.DirectCommerceHundredths.Research);
        Assert.True(Service<IGreatPeopleService>(loaded).Calculate(loaded, city.Id).Rate > 0);
    }
    [Fact]
    public void Bulb_chooses_eligible_flavor_target_and_completes_without_changing_unrelated_research()
    {
        var sim = Scenario(); var unit = Unit(sim);
        var action = Assert.Single(Service<IGreatPeopleService>(sim).AvailableActions(sim, 0, unit.Id), a => a.Command.EndsWith("discover_tech"));
        Assert.Equal("gp_probe:insight", action.Target);
        Apply(sim, new DiscoverGreatPersonCommand { Player = 0, UnitId = unit.Id });
        Assert.Contains("gp_probe:insight", sim.TeamOf(0).Techs);
        Assert.Contains(sim.Events.Published, e => e is TechDiscovered d && d.Tech == "gp_probe:insight");
        Assert.Null(sim.State.FindUnit(unit.Id));
    }
    [Fact]
    public void Hurry_caps_at_remaining_cost_and_culture_work_updates_city_history_and_neighbor_plots()
    {
        var sim = Scenario(); var city = sim.State.Cities[0];
        Apply(sim, new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:building", ContentId = "gp_probe:monument" });
        long cost = Service<ICityProductionService>(sim).Calculate(sim, city.Id, "cities:building", "gp_probe:monument").Cost;
        Apply(sim, new HurryGreatPersonCommand { Player = 0, UnitId = Unit(sim).Id });
        Assert.Equal(cost, city.ProductionQueue[0].Progress);
        sim.AdvanceTurn(); Assert.Contains("gp_probe:monument", city.Buildings);
        long culture = city.Culture; var neighbor = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(city.Position)).First();
        long plotCulture = sim.State.Plots[neighbor.Value].Culture.GetValueOrDefault(0);
        Apply(sim, new CultureGreatPersonCommand { Player = 0, UnitId = Unit(sim).Id });
        Assert.True(city.Culture > culture); Assert.True(sim.State.Plots[neighbor.Value].Culture[0] > plotCulture);
        Assert.Equal(city.Culture * 100, city.Extensions.Get<CityRuleState>(sim.Content)!.CultureHundredths[0]);
    }
    [Fact]
    public void Golden_age_requires_distinct_classes_bonuses_expire_and_timers_survive_saves()
    {
        var sim = Scenario(); var gp = Service<IGreatPeopleService>(sim); var city = sim.State.Cities[0]; city.Buildings.Add("gp_probe:academy");
        var tile = sim.State.Grid.TileAt(city.Position); sim.State.Plots[tile.Value].Kind = PlotKind.Hills;
        var before = Service<IPlotYieldService>(sim).Calculate(sim, 0, tile).Yields;
        Apply(sim, new GoldenAgeGreatPersonCommand { Player = 0, UnitId = Unit(sim).Id });
        int turns = gp.GoldenAgeTurnsRemaining(sim, 0); Assert.True(turns > 0);
        Assert.Equal(160, gp.Calculate(sim, city.Id).Rate);
        Assert.Equal(before.Production + 1, Service<IPlotYieldService>(sim).Calculate(sim, 0, tile).Yields.Production);
        var a = Unit(sim); var b = Unit(sim);
        Assert.False(sim.TryApply(new GoldenAgeGreatPersonCommand { Player = 0, UnitId = a.Id, Units = [a.Id, b.Id] }, out _));
        var c = Unit(sim, "gp_probe:engineer");
        Apply(sim, new GoldenAgeGreatPersonCommand { Player = 0, UnitId = a.Id, Units = [a.Id, c.Id] });
        Assert.NotNull(sim.State.FindUnit(b.Id)); Assert.Null(sim.State.FindUnit(c.Id));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        city.Buildings.Clear(); loaded.State.Cities[0].Buildings.Clear();
        int remaining = gp.GoldenAgeTurnsRemaining(sim, 0);
        for (int i = 0; i < remaining; i++) { sim.AdvanceTurn(); loaded.AdvanceTurn(); }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash()); Assert.Equal(0, gp.GoldenAgeTurnsRemaining(sim, 0));
        Assert.False(sim.State.PlayerById(0).Extensions.Get<CityPlayerState>(sim.Content)!.GoldenAge);
    }
    [Fact]
    public void Completed_wonder_starts_full_golden_age_without_spending_units()
    {
        var sim = Scenario(); var city = sim.State.Cities[0];
        Apply(sim, new SetCityProductionCommand { Player = 0, CityId = city.Id, Kind = "cities:building", ContentId = "gp_probe:celebration" });
        city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = 1000 };
        sim.AdvanceTurn(); Assert.Contains("gp_probe:celebration", city.Buildings);
        Assert.True(Service<IGreatPeopleService>(sim).GoldenAgeTurnsRemaining(sim, 0) > 0);
        Assert.Equal(1, Service<IGreatPeopleService>(sim).GoldenAgeUnitsRequired(sim, 0));
    }
    [Fact]
    public void Team_threshold_increase_scales_with_all_team_births_including_the_creator()
    {
        var sim = Scenario();
        // Player 1 joins the creator's team and has already produced ten births.
        var original = sim.State.Players[1];
        sim.State.Teams[1].Members.Clear();
        sim.State.Players[1] = new Player { Id = 1, TeamId = 0, Civilization = original.Civilization };
        sim.TeamOf(0).Members.Add(1); sim.State.RebuildIndexes();
        sim.State.PlayerById(1).Extensions.Set(sim.Content, new GreatPeoplePlayerState { Births = 10 });
        var gp = Service<IGreatPeopleService>(sim);
        long teammateBefore = gp.Threshold(sim, 1);
        var pending = new GreatPeopleCityState { Progress = gp.Threshold(sim, 0) };
        pending.ClassPoints["gp_probe:sage"] = 1;
        sim.State.Cities[0].Extensions.Set(sim.Content, pending);
        sim.AdvanceTurn();
        Assert.Single(sim.State.Units, u => u.Type == "gp_probe:sage");
        // The team increase is 50 * (11 / 10 + 1) = 100 for each member, so the teammate's threshold doubles.
        Assert.Equal(teammateBefore * 2, gp.Threshold(sim, 1));
    }

    [Fact]
    public void Attaching_a_general_splits_stack_experience_without_raising_levels_and_survives_reload()
    {
        var sim = Scenario();
        var general = Unit(sim, "gp_probe:general");
        var first = Unit(sim, "military:warrior");
        var second = Unit(sim, "military:warrior");
        Service<IUnitService>(sim).Initialize(sim, first.Id); Service<IUnitService>(sim).Initialize(sim, second.Id);
        first.Experience = 2;
        Assert.NotNull(Service<IUnitService>(sim).Validate(sim, 0, first.Id, UnitAction.Promote, "gp_probe:leader_skill"));
        first.Experience = 0;
        Apply(sim, new LeadGreatPersonCommand { Player = 0, UnitId = general.Id, Target = first.Id.ToString() });
        Assert.Null(sim.State.FindUnit(general.Id));
        Assert.Equal(11, first.Experience + second.Experience);
        Assert.Equal(first.Experience, second.Experience + 1);
        Assert.Equal(1, first.Extensions.Get<MilitaryUnitState>(sim.Content)!.Level);
        Assert.Contains("military:leader", first.Promotions);
        Assert.Null(Service<IUnitService>(sim).Validate(sim, 0, first.Id, UnitAction.Promote, "gp_probe:leader_skill"));
        Assert.NotNull(Service<IUnitService>(sim).Validate(sim, 0, second.Id, UnitAction.Promote, "gp_probe:leader_skill"));
        Apply(sim, new PromoteMilitaryUnitCommand { Player = 0, Unit = first.Id, Promotion = "gp_probe:leader_skill" });
        Assert.Contains("gp_probe:leader_skill", first.Promotions);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal("gp_probe:general", loaded.State.UnitById(first.Id).Extensions.Get<MilitaryUnitState>(loaded.Content)!.AttachedLeaderUnitType);
        Assert.DoesNotContain(Service<IGreatPeopleService>(loaded).AvailableActions(loaded, 0, Unit(loaded, "gp_probe:general").Id),
            a => a.Command == "great_people:lead_unit" && a.Target == first.Id.ToString());
    }
    [Fact]
    public void Special_construction_and_foreign_trade_consume_the_person_and_apply_the_preview()
    {
        var sim = Scenario(); var city = sim.State.Cities[0];
        var builder = Unit(sim, "gp_probe:builder");
        Apply(sim, new ConstructGreatPersonCommand { Player = 0, UnitId = builder.Id, Target = "gp_probe:monument" });
        Assert.Contains("gp_probe:monument", city.Buildings);
        Assert.Null(sim.State.FindUnit(builder.Id));
        var foreign = new City { Id = sim.State.NextCityId++, Owner = 1, Position = sim.State.Grid.HexOf(new TileId(40)) };
        sim.State.AddCity(foreign);
        var merchant = Unit(sim, "gp_probe:merchant");
        Assert.DoesNotContain(Service<IGreatPeopleService>(sim).AvailableActions(sim, 0, merchant.Id), a => a.Command == "great_people:trade_mission");
        sim.State.MoveUnit(merchant.Id, foreign.Position);
        long before = sim.State.PlayerById(0).Treasury;
        Apply(sim, new TradeGreatPersonCommand { Player = 0, UnitId = merchant.Id });
        Assert.Equal(before + 100 * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).UnitTradePercent / 100, sim.State.PlayerById(0).Treasury);
        Assert.Null(sim.State.FindUnit(merchant.Id));
    }

    [Fact]
    public void Combat_earns_generals_and_repeated_births_raise_personal_and_team_thresholds()
    {
        var sim = Scenario(); var best = sim.State.Cities[0]; best.Population = 100;
        var poor = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(70)), Population = 1 };
        sim.State.AddCity(poor);
        // Same team, but separate ownership: the teammate must share the threshold increase, not the unit.
        var original = sim.State.Players[1]; sim.State.Teams[1].Members.Clear();
        sim.State.Players[1] = new Player { Id = 1, TeamId = 0, Civilization = original.Civilization };
        sim.TeamOf(0).Members.Add(1); sim.State.RebuildIndexes();
        var enemy = new Player { Id = 2, TeamId = 2, Civilization = original.Civilization };
        sim.State.Players.Add(enemy); sim.State.Teams.Add(new Team { Id = 2, Members = [2] }); sim.State.RebuildIndexes();
        sim.State.SetWar(0, 2, true);
        var gp = Service<IGreatPeopleService>(sim); long firstThreshold = gp.GeneralThreshold(sim, 0);
        long teammateThreshold = gp.GeneralThreshold(sim, 1);
        var playerState = new MilitaryPlayerState(); sim.State.PlayerById(0).Extensions.Set(sim.Content, playerState);
        for (int birth = 1; birth <= 2; birth++)
        {
            long threshold = gp.GeneralThreshold(sim, 0);
            playerState.GeneralPoints = threshold - 1;
            var attacker = Unit(sim, "military:javelineer");
            Service<IUnitService>(sim).Initialize(sim, attacker.Id);
            var defender = Unit(sim, "military:warrior", 2);
            sim.State.MoveUnit(defender.Id, best.Position.Neighbor(0)); defender.Health = 1;
            Service<IVisibilityService>(sim).Recompute(sim);
            var preview = Service<ICombatService>(sim).Preview(sim, attacker.Id, defender.Id);
            while (true)
            {
                var (w, x, y, z) = sim.Rng.State;
                if (DeterministicRng.Restore(w, x, y, z).Next(preview.DieSides) >= preview.DefenderRoundOdds) break;
                sim.Rng.NextUInt64();
            }
            var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
            Service<ICombatService>(sim).Resolve(sim, attacker.Id, defender.Id);
            Service<ICombatService>(loaded).Resolve(loaded, attacker.Id, defender.Id);
            var born = sim.Events.Published.OfType<GreatGeneralBorn>().Last();
            Assert.Equal(0, born.Player); Assert.Equal(best.Id, born.City);
            Assert.Equal(best.Position, sim.State.UnitById(born.Unit).Position);
            Assert.True(sim.Content.Get<S.UnitDef>(born.UnitType).GreatGeneral);
            Assert.Equal(birth, playerState.GreatGeneralsCreated);
            Assert.True(gp.GeneralProgress(sim, 0) < gp.GeneralThreshold(sim, 0));
            Assert.True(gp.GeneralThreshold(sim, 0) > threshold);
            Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        }
        Assert.Equal(2, sim.Events.Published.OfType<GreatGeneralBorn>().Count());
        Assert.True(gp.GeneralThreshold(sim, 0) > firstThreshold); Assert.True(gp.GeneralThreshold(sim, 1) > teammateThreshold);
    }

    [Theory]
    [InlineData("military:scientist", "cities:academy")]
    [InlineData("military:great_general", "cities:military_academy")]
    public void Special_buildings_are_constructed_by_their_real_great_person(string type, string building)
    {
        var sim = Scenario(); sim.TeamOf(0).Techs.Add("research:military_tradition");
        var person = Unit(sim, type); var city = sim.State.Cities[0];
        Apply(sim, new ConstructGreatPersonCommand { Player = 0, UnitId = person.Id, Target = building });
        Assert.Contains(building, city.Buildings); Assert.Null(sim.State.FindUnit(person.Id));
        Assert.Contains(sim.Events.Published.OfType<BuildingCompleted>(), e => e.City == city.Id && e.Building == building);
    }

    [Fact]
    public void Merchant_crosses_closed_peaceful_borders_and_earns_distance_trade_profit()
    {
        var sim = Scenario(); sim.State.Cities[0].Buildings.Add("cities:palace");
        var foreign = new City { Id = sim.State.NextCityId++, Owner = 1, Position = sim.State.Cities[0].Position.Neighbor(0), Population = 12 };
        sim.State.AddCity(foreign); sim.State.PlotAt(foreign.Position).Owner = 1;
        var merchant = Unit(sim, "military:merchant"); Service<IUnitService>(sim).Initialize(sim, merchant.Id);
        var soldier = Unit(sim, "military:warrior"); Service<IUnitService>(sim).Initialize(sim, soldier.Id);
        Assert.Empty(sim.Content.Services.Get<IDiplomacyService>()); // No agreement service: ordinary units still need border permission.
        var move = new MoveUnitPathCommand { Player = 0, Unit = merchant.Id, Path = [foreign.Position] };
        Assert.False(sim.TryApply(new MoveUnitPathCommand { Player = 0, Unit = soldier.Id, Path = [foreign.Position] }, out _));
        Apply(sim, move);
        var action = Assert.Single(Service<IGreatPeopleService>(sim).AvailableActions(sim, 0, merchant.Id), a => a.Command == "great_people:trade_mission");
        Assert.True(action.Amount > sim.Content.Get<S.UnitDef>(merchant.Type).BaseTrade);
        long before = sim.State.PlayerById(0).Treasury;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        var trade = new TradeGreatPersonCommand { Player = 0, UnitId = merchant.Id };
        Apply(sim, trade); Apply(loaded, trade);
        Assert.Equal(before + action.Amount, sim.State.PlayerById(0).Treasury); Assert.Null(sim.State.FindUnit(merchant.Id));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    private sealed class Probe : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.UnitDef>().Register(new() { Id = "gp_probe:sage", DefaultUnit = "gp_probe:sage", GoldenAge = true, BaseDiscover = 10000, BaseHurry = 10000, GreatWorkCulture = 1000,
                GreatPeoples = ImmutableDictionary<string, bool>.Empty.Add("gp_probe:settled_sage", true), Flavors = ImmutableDictionary<string, int>.Empty.Add("probe", 100) });
            context.Content<S.SpecialistDef>().Register(new() { Id = "gp_probe:settled_sage", Commerces = new(0, 6, 0, 0), GreatPeopleRateChange = 2, GreatPersonUnit = "gp_probe:sage" });
            context.Content<S.PromotionDef>().Register(new() { Id = "gp_probe:leader_skill", Leader = true,
                CombatTypes = ImmutableDictionary<string, bool>.Empty.Add("military:unitcombat_melee", true) });
            context.Content<S.UnitDef>().Register(new() { Id = "gp_probe:general", DefaultUnit = "gp_probe:general", GreatGeneral = true, LeaderExperience = 11, LeaderPromotion = "military:leader" });
            context.Content<S.UnitDef>().Register(new() { Id = "gp_probe:builder", DefaultUnit = "gp_probe:builder", ForceBuildings = ImmutableDictionary<string, bool>.Empty.Add("gp_probe:monument", true) });
            context.Content<S.UnitDef>().Register(new() { Id = "gp_probe:merchant", DefaultUnit = "gp_probe:merchant", BaseTrade = 100 });
            context.Content<S.UnitDef>().Register(new() { Id = "gp_probe:engineer", DefaultUnit = "gp_probe:engineer", GoldenAge = true });
            context.Content<S.TechDef>().Register(new() { Id = "gp_probe:insight", Cost = 10, Flavors = ImmutableDictionary<string, int>.Empty.Add("probe", 100) });
            context.Content<S.BuildingDef>().Register(new() { Id = "gp_probe:academy", DefaultBuilding = "gp_probe:academy", GreatPersonUnit = "gp_probe:sage", GreatPeopleRateChange = 80 });
            context.Content<S.BuildingDef>().Register(new() { Id = "gp_probe:forge", DefaultBuilding = "gp_probe:forge", GreatPersonUnit = "gp_probe:engineer", GreatPeopleRateChange = 80 });
            context.Content<S.BuildingDef>().Register(new() { Id = "gp_probe:inspiration", DefaultBuilding = "gp_probe:inspiration", GreatPeopleRateModifier = 50 });
            context.Content<S.BuildingDef>().Register(new() { Id = "gp_probe:monument", DefaultBuilding = "gp_probe:monument", Cost = 20 });
            context.Content<S.BuildingDef>().Register(new() { Id = "gp_probe:celebration", DefaultBuilding = "gp_probe:celebration", Cost = 1, GoldenAge = true });
        }
    }
}
