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
    private sealed class Probe : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.UnitDef>().Register(new() { Id = "gp_probe:sage", DefaultUnit = "gp_probe:sage", GoldenAge = true, BaseDiscover = 10000, BaseHurry = 10000, GreatWorkCulture = 1000,
                GreatPeoples = ImmutableDictionary<string, bool>.Empty.Add("gp_probe:settled_sage", true), Flavors = ImmutableDictionary<string, int>.Empty.Add("probe", 100) });
            context.Content<S.SpecialistDef>().Register(new() { Id = "gp_probe:settled_sage", Commerces = new(0, 6, 0, 0), GreatPeopleRateChange = 2, GreatPersonUnit = "gp_probe:sage" });
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
