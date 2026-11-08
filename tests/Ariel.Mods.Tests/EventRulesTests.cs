using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>Loads src/Mods/Events directly from its own build output.</summary>
public sealed class EventRulesTests
{
    private static readonly Lazy<ModSource> Mine = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ariel.slnx"))) dir = dir.Parent;
        var root = Path.Combine(dir!.FullName, "src", "Mods", "Events", "bin");
        var built = Directory.GetFiles(root, "mod.json", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).First();
        return ModLoader.FromDirectory(Path.GetDirectoryName(built)!);
    });
    private static readonly Lazy<ContentDb> Content = new(() =>
        ModLoader.Build(new LoadedMods().Sources.Append(Mine.Value)));
    private static readonly Lazy<ContentDb> EmpireContent = new(() =>
    {
        var loaded = new LoadedMods();
        var probe = new ModSource
        {
            Manifest = new() { Id = "empire_probe", Version = "1", References = ["events"] }, Root = loaded.Root, Instance = new EmpireProbe(),
        };
        return ModLoader.Build(loaded.Sources.Append(Mine.Value).Append(probe));
    });
    private static IEventService Events(Simulation sim) => sim.Content.Services.Get<IEventService>().Single();
    private static void Turn(Simulation sim) => sim.Content.TurnSystems.Get("events:turn").Run(sim);

    private static Simulation Scenario(ContentDb? content = null)
    {
        var sim = GameFactory.Scenario(content ?? Content.Value);
        GameFactory.NeutralPolitics(sim);
        sim.State.Players[0].Treasury = 100;
        return sim;
    }
    private static City AddCity(Simulation sim, int tile, int population = 3, params string[] buildings)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = population, Buildings = buildings.ToList() };
        sim.State.AddCity(city);
        return city;
    }
    private static PendingEvent Wait(Simulation sim, string trigger, int player = 0)
    {
        for (int i = 0; i < 3000; i++)
        {
            if (Events(sim).Pending(sim, player).FirstOrDefault(p => p.Trigger == trigger) is { } found) return found;
            Turn(sim);
        }
        throw new Xunit.Sdk.XunitException("event never happened: " + trigger);
    }
    private static void Choose(Simulation sim, PendingEvent e, string option) =>
        Assert.True(sim.TryApply(new ChooseEventOptionCommand { Player = e.Player, EventId = e.Id, Option = option }, out var error), error);

    [Fact]
    public void Content_ships_neutral_events_each_with_one_default_option()
    {
        var sim = Scenario();
        var triggers = sim.Content.Get<S.EventTriggerDef>().All;
        Assert.True(triggers.Count() >= 10);
        foreach (var t in triggers)
        {
            var options = sim.Content.Get<S.EventOptionDef>().All.Where(o => o.Trigger == t.Id).ToArray();
            Assert.True(options.Length >= 2, t.Id);
            Assert.Single(options, o => o.IsDefault);
        }
    }

    [Fact]
    public void Nothing_happens_without_the_required_tech_or_city()
    {
        var sim = Scenario();
        for (int i = 0; i < 300; i++) Turn(sim);
        Assert.Empty(Events(sim).Pending(sim, 0));
        sim.TeamOf(0).Techs.Add("research:agriculture");
        for (int i = 0; i < 300; i++) Turn(sim);
        Assert.Empty(Events(sim).Pending(sim, 0)); // Herbalists also needs a city of population 3
    }

    [Fact]
    public void Choosing_an_option_pays_the_cost_keeps_the_effect_and_publishes_events()
    {
        var sim = Scenario();
        var city = AddCity(sim, 14);
        sim.TeamOf(0).Techs.Add("research:agriculture");
        var e = Wait(sim, "events:herbalists");
        Assert.Equal(city.Id, e.City);
        Assert.Contains(sim.Events.Published, m => m is EventTriggered t && t.EventId == e.Id);
        Assert.Equal(["events:herbalists_fund", "events:herbalists_ignore"], Events(sim).Options(sim, e.Id));
        Assert.NotNull(Events(sim).ValidateChoice(sim, 1, e.Id, "events:herbalists_fund"));
        Assert.NotNull(Events(sim).ValidateChoice(sim, 0, e.Id, "events:bandits_pay"));
        Choose(sim, e, "events:herbalists_fund");
        Assert.Equal(70, sim.State.Players[0].Treasury);
        Assert.Empty(Events(sim).Pending(sim, 0));
        Assert.Contains(sim.Events.Published, m => m is EventResolved r && r.Option == "events:herbalists_fund" && !r.Automatic);
        var ledger = ModifierSink.Collect(new(sim, ModifierScope.City, 0, city)).Ledger;
        Assert.Equal(2, ledger.Total("cities:health"));
    }

    [Fact]
    public void Unaffordable_options_are_rejected()
    {
        var sim = Scenario();
        AddCity(sim, 14);
        sim.TeamOf(0).Techs.Add("research:agriculture");
        sim.State.Players[0].Treasury = 10;
        var e = Wait(sim, "events:herbalists");
        Assert.DoesNotContain("events:herbalists_fund", Events(sim).Options(sim, e.Id));
        Assert.False(sim.TryApply(new ChooseEventOptionCommand { Player = 0, EventId = e.Id, Option = "events:herbalists_fund" }, out _));
    }

    [Fact]
    public void Unresolved_events_use_the_default_option_at_turn_end_and_do_not_repeat_at_once()
    {
        var sim = Scenario();
        AddCity(sim, 14, 3, "cities:granary");
        var e = Wait(sim, "events:surplus");
        long before = sim.State.Players[0].Treasury;
        Turn(sim);
        Assert.Empty(Events(sim).Pending(sim, 0));
        Assert.Equal(before + 35, sim.State.Players[0].Treasury);
        Assert.Contains(sim.Events.Published, m => m is EventResolved r && r.EventId == e.Id && r.Automatic);
        for (int i = 0; i < 100; i++) Turn(sim);
        Assert.DoesNotContain(Events(sim).Pending(sim, 0), p => p.Trigger == "events:surplus"); // same turn, cooldown not over
    }

    [Fact]
    public void Plot_yield_effects_are_temporary_and_expire()
    {
        var sim = Scenario();
        AddCity(sim, 14);
        sim.State.Plots[15].Feature = "world:forest";
        var e = Wait(sim, "events:forest_fire");
        Assert.Equal(15, e.Plot);
        Choose(sim, e, "events:forest_fire_burn");
        YieldVector Yield() => ModifierSink.Collect(new(sim, ModifierScope.Plot, 0, Plot: new TileId(15))).FlatYield;
        Assert.Equal(-1, Yield().Food);
        sim.State.Turn += 15;
        Turn(sim);
        Assert.Equal(0, Yield().Food);
    }

    [Fact]
    public void Research_and_happiness_effects_apply_to_the_player_and_city()
    {
        var sim = Scenario();
        var city = AddCity(sim, 14, 3, "cities:library");
        Events(sim); // service registered
        var research = sim.Content.Services.Get<IResearchService>().Single();
        research.SetQueue(sim, 0, ["research:agriculture"]);
        var e = Wait(sim, "events:manuscript");
        // Other events can spend gold while waiting; isolate this choice's budget.
        sim.State.Players[0].Treasury = 100;
        Choose(sim, e, "events:manuscript_buy");
        Assert.Equal(50, sim.State.Players[0].Treasury);
        Assert.True(sim.TeamOf(0).Research.GetValueOrDefault("research:agriculture") > 0 || sim.TeamOf(0).Techs.Contains("research:agriculture"));
        // Enable bandits only now, so an earlier auto-resolution cannot put them on cooldown.
        sim.TeamOf(0).Techs.Add("research:bronze_working");
        sim.State.Players[0].Treasury = 100;
        var bandits = Wait(sim, "events:bandits");
        sim.State.Players[0].Treasury = 100;
        Choose(sim, bandits, "events:bandits_guards");
        Assert.Equal(1, ModifierSink.Collect(new(sim, ModifierScope.City, 0, city)).Ledger.Total("cities:happiness"));
    }

    /// <summary>Test-only empire-wide trigger. Every shipped trigger needs a city, so only this reaches the empire-wide path.</summary>
    private sealed class EmpireProbe : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.EventTriggerDef>().Register(new() { Id = "empire_probe:decree", Name = "Decree", ChancePercent = 100 });
            context.Content<S.EventOptionDef>().Register(new()
            {
                Id = "empire_probe:decree_accept", Name = "Accept", Trigger = "empire_probe:decree", IsDefault = true, HappinessDelta = 1, HealthDelta = 1,
            });
        }
    }
    private static Simulation EmpireScenario() => Scenario(EmpireContent.Value);
    private static long Happiness(Simulation sim, City city) => ModifierSink.Collect(new(sim, ModifierScope.City, city.Owner, city)).Ledger.Total("cities:happiness");
    private static long Health(Simulation sim, City city) => ModifierSink.Collect(new(sim, ModifierScope.City, city.Owner, city)).Ledger.Total("cities:health");

    [Fact]
    public void Empire_wide_effects_reach_every_city_the_player_owns_and_stop_at_capture()
    {
        var sim = EmpireScenario();
        var home = AddCity(sim, 14); var second = AddCity(sim, 20);
        var rival = new City { Id = sim.State.NextCityId++, Owner = 1, Position = sim.State.Grid.HexOf(new TileId(57)), Population = 3 };
        sim.State.AddCity(rival);
        var e = Wait(sim, "empire_probe:decree");
        Assert.Equal(-1, e.City);
        Choose(sim, e, "empire_probe:decree_accept");
        Assert.Equal(1, Happiness(sim, home)); Assert.Equal(1, Happiness(sim, second)); Assert.Equal(0, Happiness(sim, rival));
        Assert.Equal(1, Health(sim, home)); Assert.Equal(1, Health(sim, second)); Assert.Equal(0, Health(sim, rival));
        sim.State.SetWar(0, 1, true);
        sim.Content.Services.Get<ICityCaptureService>().Single().Capture(sim, second.Id, 1);
        var captured = sim.State.CityById(second.Id);
        Assert.Equal(0, Happiness(sim, captured)); Assert.Equal(0, Health(sim, captured));
        // Capital relocation can add building happiness; the decree must still contribute independently.
        var ledger = ModifierSink.Collect(new(sim, ModifierScope.City, home.Owner, home)).Ledger;
        Assert.Equal(1, Assert.Single(ledger.Lines,
            line => line.Source == "empire_probe:decree_accept" && line.Key == "cities:happiness").Amount);
    }

    [Fact]
    public void Answering_a_timed_event_on_a_later_turn_guarantees_full_duration_turns()
    {
        var sim = Scenario();
        var city = AddCity(sim, 14);
        sim.TeamOf(0).Techs.Add("research:bronze_working");
        var e = Wait(sim, "events:bandits");
        sim.State.Turn = e.Turn + 3; // The human answers three turns after the trigger.
        Choose(sim, e, "events:bandits_ignore"); // Health -1 for 10 turns.
        Assert.Equal(-1, Health(sim, city));
        sim.State.Turn = e.Turn + 3 + 9; Turn(sim);
        Assert.Equal(-1, Health(sim, city)); // still active on its 9th turn
        sim.State.Turn = e.Turn + 3 + 10; // Expiration turn reached at resolution turn + duration.
        Turn(sim);
        Assert.Equal(0, Health(sim, city));
    }

    [Fact]
    public void Applied_event_effects_record_event_id_and_trigger_provenance()
    {
        var sim = Scenario();
        var city = AddCity(sim, 14);
        sim.TeamOf(0).Techs.Add("research:agriculture");
        var e = Wait(sim, "events:herbalists");
        Choose(sim, e, "events:herbalists_fund");
        var state = sim.State.Extensions.Get<EventState>(sim.Content)!;
        var effect = Assert.Single(state.Effects);
        Assert.Equal(e.Id, effect.EventId);
        Assert.Equal("events:herbalists", effect.Trigger);
        Assert.Equal(0, effect.Player);
        Assert.Equal(city.Id, effect.City);
        Assert.Equal("events:herbalists_fund", effect.Option);
        Assert.True(effect.IsCityScope);
        Assert.False(effect.IsEmpireScope);
        Assert.True(effect.IsPermanent);
    }

    [Fact]
    public void City_capture_prunes_active_city_event_effects()
    {
        var sim = Scenario();
        var city = AddCity(sim, 14);
        sim.TeamOf(0).Techs.Add("research:agriculture");
        var e = Wait(sim, "events:herbalists");
        Choose(sim, e, "events:herbalists_fund");
        var state = sim.State.Extensions.Get<EventState>(sim.Content)!;
        Assert.Contains(state.Effects, eff => eff.City == city.Id);
        Assert.Equal(2, Health(sim, city));

        sim.State.SetWar(0, 1, true);
        sim.Content.Services.Get<ICityCaptureService>().Single().Capture(sim, city.Id, 1);

        Assert.DoesNotContain(state.Effects, eff => eff.City == city.Id);
        var capturedCity = sim.State.CityById(city.Id);
        Assert.Equal(0, Health(sim, capturedCity));
    }

    [Fact]
    public void Auto_resolve_publishes_event_resolved_with_expired_option_when_options_are_unaffordable()
    {
        var sim = Scenario();
        var state = new EventState();
        sim.State.Extensions.Set(sim.Content, state);
        var pending = new PendingEvent(state.NextEventId++, 0, "events:nonexistent_trigger", -1, -1, sim.State.Turn);
        state.AddPending(pending);
        Assert.NotNull(state.FindPending(pending.Id));

        Turn(sim);

        Assert.Null(state.FindPending(pending.Id));
        Assert.Contains(sim.Events.Published, m => m is EventResolved r
            && r.Player == 0
            && r.EventId == pending.Id
            && r.Trigger == "events:nonexistent_trigger"
            && r.Option == EventResolved.Expired
            && r.Automatic);
    }

    [Fact]
    public void EventState_lifecycle_and_transition_methods()
    {
        var sim = Scenario();
        var state = new EventState();
        Assert.False(state.HasPending);
        Assert.Equal(0, state.PendingCount);
        Assert.Equal(0, state.ActiveEffectsCount);

        // AddPending and FindPending
        var p1 = new PendingEvent(1, 0, "test:trig1", 10, -1, 5);
        state.AddPending(p1);
        Assert.True(state.HasPending);
        Assert.Equal(1, state.PendingCount);
        Assert.Equal(p1, state.FindPending(1));
        Assert.Null(state.FindPending(999));

        // AddPending replaces duplicate id
        var p1Updated = new PendingEvent(1, 0, "test:trig1_mod", 10, -1, 6);
        state.AddPending(p1Updated);
        Assert.Equal(1, state.PendingCount);
        Assert.Equal("test:trig1_mod", state.FindPending(1)!.Trigger);

        // RemovePending
        Assert.True(state.RemovePending(1));
        Assert.False(state.RemovePending(1));
        Assert.Null(state.FindPending(1));

        // AddEffect and ActiveEffectsCount
        var eff1 = new ActiveEventEffect { EventId = 1, Trigger = "t1", Player = 0, City = 10, ExpiresTurn = 15, Happiness = 2 };
        var eff2 = new ActiveEventEffect { EventId = 2, Trigger = "t2", Player = 0, City = 20, ExpiresTurn = 0, Health = 1 }; // permanent
        var eff3 = new ActiveEventEffect { EventId = 3, Trigger = "t3", Player = 0, City = -1, ExpiresTurn = 25, PlotFood = 1 }; // empire-wide
        state.AddEffect(eff1);
        state.AddEffect(eff2);
        state.AddEffect(eff3);
        Assert.Equal(3, state.ActiveEffectsCount);

        // PruneExpiredEffects
        Assert.Equal(0, state.PruneExpiredEffects(10)); // turn 10 < 15
        Assert.Equal(3, state.ActiveEffectsCount);
        Assert.Equal(1, state.PruneExpiredEffects(15)); // turn 15 >= 15 prunes eff1
        Assert.Equal(2, state.ActiveEffectsCount);
        Assert.DoesNotContain(eff1, state.Effects);
        Assert.Contains(eff2, state.Effects); // permanent remains
        Assert.Contains(eff3, state.Effects); // ExpiresTurn 25 remains

        // PruneCityEffects
        Assert.Equal(1, state.PruneCityEffects(20)); // prunes eff2 (City == 20)
        Assert.Equal(1, state.ActiveEffectsCount);
        Assert.Contains(eff3, state.Effects); // City == -1 untouched

        // PruneInvalidEffects
        var city = AddCity(sim, 14);
        var cityEff = new ActiveEventEffect { EventId = 4, Trigger = "t4", Player = 0, City = city.Id, Happiness = 1 };
        var deadCityEff = new ActiveEventEffect { EventId = 5, Trigger = "t5", Player = 0, City = 9999, Happiness = 1 };
        var wrongOwnerEff = new ActiveEventEffect { EventId = 6, Trigger = "t6", Player = 1, City = city.Id, Happiness = 1 }; // city owner is 0
        state.AddEffect(cityEff);
        state.AddEffect(deadCityEff);
        state.AddEffect(wrongOwnerEff);
        Assert.Equal(4, state.ActiveEffectsCount);

        int pruned = state.PruneInvalidEffects(sim);
        Assert.Equal(2, pruned); // deadCityEff and wrongOwnerEff pruned
        Assert.Contains(cityEff, state.Effects);
        Assert.Contains(eff3, state.Effects);

        // RecordFired and GetLastFired
        Assert.Null(state.GetLastFired(0, "events:herbalists"));
        state.RecordFired(0, "events:herbalists", 42);
        Assert.Equal(42, state.GetLastFired(0, "events:herbalists"));
        Assert.Null(state.GetLastFired(1, "events:herbalists"));
    }

    [Fact]
    public void Events_are_deterministic_for_a_seed()
    {
        string Run()
        {
            var sim = Scenario();
            AddCity(sim, 14, 3, "cities:granary");
            sim.TeamOf(0).Techs.Add("research:priesthood");
            for (int i = 0; i < 400; i++) { sim.State.Turn++; Turn(sim); }
            return string.Join(",", sim.Events.Published.OfType<EventTriggered>().Select(t => t.Trigger)) + sim.ComputeHash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void EventCooldownKey_provides_strongly_typed_keys_and_canonical_formatting()
    {
        var key1 = EventCooldownKey.Of(0, "events:herbalists");
        var key2 = EventCooldownKey.Of(1, "events:forest_fire");
        var parsed = EventCooldownKey.Parse("0:events:herbalists");

        Assert.Equal("0:events:herbalists", key1.Key);
        Assert.Equal("0:events:herbalists", key1.ToString());
        Assert.Equal(key1, parsed);
        Assert.True(key1.CompareTo(key2) < 0);

        string implicitString = key1;
        Assert.Equal("0:events:herbalists", implicitString);

        var state = new EventState();
        Assert.Null(state.GetLastFired(key1));
        state.RecordFired(key1, 10);
        Assert.Equal(10, state.GetLastFired(key1));
        Assert.Equal(10, state.GetLastFired(0, "events:herbalists"));
    }

    [Fact]
    public void Active_event_effects_have_canonical_ids_and_can_be_queried_and_cancelled_by_id()
    {
        var sim = Scenario();
        var service = Events(sim);
        var state = new EventState();
        sim.State.Extensions.Set(sim.Content, state);

        var eff1 = new ActiveEventEffect { Trigger = "t1", Player = 0, Happiness = 1 };
        var eff2 = new ActiveEventEffect { Trigger = "t2", Player = 1, Happiness = 2 };
        state.AddEffect(eff1);
        state.AddEffect(eff2);

        Assert.Equal(1, eff1.Id);
        Assert.Equal(2, eff2.Id);
        Assert.Equal(3, state.NextEffectId);

        var all = service.ActiveEffects(sim);
        Assert.Equal(2, all.Count);
        Assert.Equal([1, 2], all.Select(e => e.Id));

        var player0Effects = service.ActiveEffects(sim, 0);
        Assert.Single(player0Effects);
        Assert.Equal(1, player0Effects[0].Id);

        Assert.Same(eff1, service.FindEffect(sim, 1));
        Assert.Same(eff2, service.FindEffect(sim, 2));
        Assert.Null(service.FindEffect(sim, 999));

        Assert.True(service.CancelEffect(sim, 1));
        Assert.False(service.CancelEffect(sim, 1));
        Assert.Null(service.FindEffect(sim, 1));
        Assert.Single(service.ActiveEffects(sim));
    }
}
