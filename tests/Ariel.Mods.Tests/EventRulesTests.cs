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
    public void A_late_answer_to_a_timed_event_expires_with_the_trigger_turn()
    {
        var sim = Scenario();
        var city = AddCity(sim, 14);
        sim.TeamOf(0).Techs.Add("research:bronze_working");
        var e = Wait(sim, "events:bandits");
        sim.State.Turn = e.Turn + 1; // The human answers one turn after the trigger.
        Choose(sim, e, "events:bandits_ignore"); // Health -1 for 10 turns.
        Assert.Equal(-1, Health(sim, city));
        sim.State.Turn = e.Turn + 9; Turn(sim);
        Assert.Equal(-1, Health(sim, city)); // still active on its last turn
        sim.State.Turn = e.Turn + 10; // The duration counts from the trigger turn, as it does for automatic answers.
        Turn(sim);
        Assert.Equal(0, Health(sim, city));
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
}
