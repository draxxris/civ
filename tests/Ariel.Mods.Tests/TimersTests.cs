using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The base game's one store of timers, on the base modules and a probe mod that registers one kind for each
/// rule a kind can declare.</summary>
public sealed class TimersTests
{
    private static readonly List<string> Log = [];

    private sealed class LoggingKind(string id) : TimerKind(id)
    {
        public override void Started(Simulation sim, GameTimer timer) => Log.Add($"started {timer.Id} {timer.Source}");
        public override void Changed(Simulation sim, GameTimer before, GameTimer after) => Log.Add($"changed {before.Id}>{after.Id} {after.Source}");
        public override void Ended(Simulation sim, GameTimer timer, TimerEnd reason) => Log.Add($"ended {timer.Id} {timer.Source} {reason}");
    }

    private sealed class ProbeMod : IMod
    {
        public void Register(IModContext ctx)
        {
            ctx.RegisterService<ITimerKind>("replace", new LoggingKind("probe:replace"));
            ctx.RegisterService<ITimerKind>("prolong", new LoggingKind("probe:prolong") { Stacking = TimerStacking.Prolong });
            ctx.RegisterService<ITimerKind>("stack", new LoggingKind("probe:stack") { Stacking = TimerStacking.Stack });
            ctx.RegisterService<ITimerKind>("keep", new LoggingKind("probe:keep") { OnOwnerChange = TimerOwnerChange.Keep });
            ctx.RegisterService<ITimerKind>("transfer", new LoggingKind("probe:transfer") { OnOwnerChange = TimerOwnerChange.Transfer });
            ctx.RegisterService<ITimerKind>("turn_end", new LoggingKind("probe:turn_end") { Expiry = TimerExpiry.OwnerTurnEnd });
            ctx.RegisterService<ITimerKind>("chain", new ChainKind());
            ctx.Subscribe<TimerEnded>("log_ended", (_, ended) => Log.Add($"event ended {ended.Timer.Id} {ended.Reason}"));
            ctx.Subscribe<TimerStarted>("log_started", (_, started) => Log.Add($"event started {started.Timer.Id}"));
            ctx.Subscribe<TimerChanged>("log_changed", (_, changed) => Log.Add($"event changed {changed.Before.Id}>{changed.After.Id}"));
        }
    }

    /// <summary>A kind whose expiry starts a timer of another kind, as a feature chaining one timer into the next.</summary>
    private sealed class ChainKind() : TimerKind("probe:chain")
    {
        public override void Ended(Simulation sim, GameTimer timer, TimerEnd reason)
        {
            if (reason == TimerEnd.Expired)
                sim.Rules().Timers.Start(sim, new GameTimer { Kind = "probe:replace", Holder = timer.Holder, ExpiresTurn = sim.State.Turn + 5 });
        }
    }

    private sealed class DuplicateKindMod : IMod
    {
        public void Register(IModContext ctx)
        {
            ctx.RegisterService<ITimerKind>("one", new TimerKind("probe:same"));
            ctx.RegisterService<ITimerKind>("two", new TimerKind("probe:same"));
        }
    }

    private static ModSource Probe(IMod mod, string id = "probe") =>
        new() { Manifest = new() { Id = id, Version = "1.0.0", References = ["foundation", "cities"] }, Root = AppContext.BaseDirectory, Instance = mod };

    private static readonly Lazy<ContentDb> Content = new(() =>
        GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), [Probe(new ProbeMod())]));

    private static Simulation Scenario()
    {
        var sim = GameFactory.Scenario(Content.Value);
        GameFactory.NeutralPolitics(sim);
        Log.Clear();
        return sim;
    }
    private static City AddCity(Simulation sim, int owner, int tile)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = 3 };
        sim.State.AddCity(city);
        return city;
    }
    private static ModifierEffect Happiness(long amount) => new() { Scope = ModifierScope.City, Key = "cities:happiness", Amount = amount };
    private static Ledger CityLedger(Simulation sim, City city) => ModifierSink.Collect(new(sim, ModifierScope.City, city.Owner, city)).Ledger;
    private static GameTimer Timer(string kind, TimerHolder holder, int expires = 0, string key = "", int value = 0) =>
        new() { Kind = kind, Holder = holder, ExpiresTurn = expires, Key = key, Value = value };

    [Fact]
    public void Modifiers_apply_to_their_holder_by_scope_until_the_expiry_turn()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        var home = AddCity(sim, 0, 14); var other = AddCity(sim, 0, 20); var rival = AddCity(sim, 1, 57);
        int turn = sim.State.Turn;
        var held = timers.Start(sim, new GameTimer { Kind = "probe:stack", Key = "probe:city", Holder = TimerHolder.OfCity(home.Id), ExpiresTurn = turn + 2, Effects = [Happiness(2)] });
        timers.Start(sim, new GameTimer { Kind = "probe:stack", Key = "probe:empire", Holder = TimerHolder.OfPlayer(0), Effects = [Happiness(1)] });
        timers.Start(sim, new GameTimer
        {
            Kind = "probe:stack", Key = "probe:plot", Holder = TimerHolder.OfPlot(15), ExpiresTurn = turn + 1,
            Effects = [new() { Scope = ModifierScope.Plot, Key = "yield.flat.food", Amount = -1 }, Happiness(5)],
        });

        Assert.Equal(1, held.Id);
        Assert.Equal(3, CityLedger(sim, home).Total("cities:happiness"));
        Assert.Equal(2, Assert.Single(CityLedger(sim, home).Lines, l => l.Source == "probe:city").Amount);
        Assert.Equal(1, CityLedger(sim, other).Total("cities:happiness"));
        Assert.Equal(0, CityLedger(sim, rival).Total("cities:happiness"));
        Assert.Equal(-1, ModifierSink.Collect(new(sim, ModifierScope.Plot, 0, Plot: new TileId(15))).FlatYield.Food);
        Assert.Equal(2, held.TurnsRemaining(turn));

        sim.State.Turn = turn + 1;
        Assert.Equal(0, ModifierSink.Collect(new(sim, ModifierScope.Plot, 0, Plot: new TileId(15))).FlatYield.Food);
        Assert.Equal(3, CityLedger(sim, home).Total("cities:happiness"));
        sim.State.Turn = turn + 2;
        Assert.Equal(1, CityLedger(sim, home).Total("cities:happiness"));
        Assert.Equal(["probe:empire"], timers.Active(sim).Select(e => e.Source));
        Assert.Null(timers.Find(sim, "probe:stack", TimerHolder.OfCity(home.Id), "probe:city"));
        Assert.NotNull(timers.Find(sim, held.Id)); // inactive, but kept until the expiry system runs

        sim.Content.TurnSystems.Get("foundation:timers").Run(sim);
        Assert.Null(timers.Find(sim, held.Id));
        Assert.Single(sim.State.Extensions.Get<TimerState>(sim.Content)!.Timers);
    }

    [Fact]
    public void A_unit_timer_modifies_its_unit_and_ends_when_the_unit_is_gone()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = sim.State.Grid.HexOf(new TileId(14)) };
        sim.State.AddUnit(unit);
        timers.Start(sim, new GameTimer
        {
            Kind = "probe:replace", Holder = TimerHolder.OfUnit(unit.Id), ExpiresTurn = sim.State.Turn + 9,
            Effects = [new() { Scope = ModifierScope.Unit, Key = "military:strength_percent", Amount = 25 }],
        });
        Assert.Equal(25, ModifierSink.Collect(new(sim, ModifierScope.Unit, 0, Unit: unit)).Ledger.Total("military:strength_percent"));
        sim.State.RemoveUnit(unit.Id);
        sim.Content.TurnSystems.Get("foundation:timers").Run(sim);
        Assert.Empty(timers.Active(sim));
        Assert.Equal(["started 1 probe:replace", "event started 1", "ended 1 probe:replace HolderLost", "event ended 1 HolderLost"], Log);
    }

    [Fact]
    public void Replace_restarts_the_timer_and_prolong_never_shortens_it()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        var city = TimerHolder.OfCity(AddCity(sim, 0, 14).Id);
        var first = timers.Start(sim, Timer("probe:replace", city, 5, value: 1));
        var other = timers.Start(sim, Timer("probe:replace", TimerHolder.OfPlayer(0), 5));
        var shorter = timers.Start(sim, Timer("probe:replace", city, 3, value: 2));
        Assert.Equal(3, shorter.Id);
        Assert.Equal([other.Id, shorter.Id], timers.Active(sim).Select(t => t.Id));
        Assert.Equal((3, 2), (timers.Find(sim, "probe:replace", city)!.ExpiresTurn, timers.Find(sim, "probe:replace", city)!.Value));
        Assert.Null(timers.Find(sim, first.Id));

        timers.Start(sim, Timer("probe:prolong", city, 8));
        Assert.Equal(8, timers.Start(sim, Timer("probe:prolong", city, 6)).ExpiresTurn);
        Assert.Equal(12, timers.Start(sim, Timer("probe:prolong", city, 12)).ExpiresTurn);
        Assert.Equal(0, timers.Start(sim, Timer("probe:prolong", city, 0)).ExpiresTurn);
        Assert.Equal(0, timers.Start(sim, Timer("probe:prolong", city, 9)).ExpiresTurn);
        Assert.Single(timers.Active(sim, "probe:prolong"));
        Assert.Equal(["started 1 probe:replace", "event started 1", "started 2 probe:replace", "event started 2",
            "changed 1>3 probe:replace", "event changed 1>3", "started 4 probe:prolong", "event started 4",
            "changed 4>5 probe:prolong", "event changed 4>5", "changed 5>6 probe:prolong", "event changed 5>6",
            "changed 6>7 probe:prolong", "event changed 6>7", "changed 7>8 probe:prolong", "event changed 7>8"], Log);
    }

    [Fact]
    public void Stack_keeps_every_timer_with_its_own_expiry()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        var player = TimerHolder.OfPlayer(0);
        timers.Start(sim, Timer("probe:stack", player, sim.State.Turn + 2, "a"));
        timers.Start(sim, Timer("probe:stack", player, sim.State.Turn + 4, "a"));
        timers.Start(sim, Timer("probe:stack", player, sim.State.Turn + 1, "b"));
        Assert.Equal(3, timers.Active(sim, "probe:stack").Count);
        Assert.Equal(1, timers.Find(sim, "probe:stack", player, "a")!.Id); // the oldest of two
        sim.State.Turn += 2;
        sim.Content.TurnSystems.Get("foundation:timers").Run(sim);
        Assert.Equal([2], timers.Active(sim).Select(t => t.Id));
    }

    [Fact]
    public void Expiry_ends_timers_in_start_order_and_calls_the_hooks_after_removing_them()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        int turn = sim.State.Turn;
        var city = TimerHolder.OfCity(AddCity(sim, 0, 14).Id);
        timers.Start(sim, Timer("probe:stack", city, turn + 3, "late"));
        timers.Start(sim, Timer("probe:stack", city, turn + 1, "early"));
        timers.Start(sim, Timer("probe:stack", city, 0, "never"));
        timers.Start(sim, Timer("probe:chain", city, turn + 1));
        Log.Clear();

        sim.State.Turn = turn + 1;
        sim.Content.TurnSystems.Get("foundation:timers").Run(sim);
        // Ended in start order; the chained timer a hook started is stored afterwards and is not itself expired.
        Assert.Equal(["ended 2 early Expired", "event ended 2 Expired", "started 5 probe:replace", "event started 5",
            "event ended 4 Expired"], Log);
        Assert.Equal([1, 3, 5], timers.Active(sim).Select(t => t.Id));
        Assert.Equal(turn + 1 + 5, timers.Find(sim, 5)!.ExpiresTurn);

        sim.State.Turn = turn + 3;
        Log.Clear();
        sim.Content.TurnSystems.Get("foundation:timers").Run(sim);
        Assert.Equal(["ended 1 late Expired", "event ended 1 Expired"], Log);
    }

    [Fact]
    public void Nothing_changes_when_nothing_expires()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        timers.Start(sim, Timer("probe:replace", TimerHolder.OfPlayer(0), sim.State.Turn + 4));
        Log.Clear();
        var before = sim.ComputeHash();
        sim.Content.TurnSystems.Get("foundation:timers").Run(sim);
        sim.Content.TurnSystems.Get("foundation:timers_turn_end").Run(sim);
        Assert.Equal(before, sim.ComputeHash());
        Assert.Empty(Log);
    }

    [Fact]
    public void An_owner_turn_end_timer_still_applies_through_its_last_turn_and_is_dropped_at_that_turns_end()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        var mine = AddCity(sim, 0, 14); var theirs = AddCity(sim, 1, 57);
        int turn = sim.State.Turn;
        timers.Start(sim, Timer("probe:turn_end", TimerHolder.OfCity(mine.Id), turn + 2));
        timers.Start(sim, Timer("probe:turn_end", TimerHolder.OfCity(theirs.Id), turn + 2));
        var end = (IPlayerTurnSystem)sim.Content.TurnSystems.Get("foundation:timers_turn_end");
        Assert.Equal(TimerSystems.TurnEndOrder, end.Order);

        end.Run(sim, 0); // the end of the first turn: one more to go
        Assert.Equal(2, timers.Active(sim).Count);
        sim.State.Turn = turn + 1;
        sim.Content.TurnSystems.Get("foundation:timers").Run(sim); // the start of a turn does not drop it
        Assert.Equal(2, timers.Active(sim).Count);
        end.Run(sim, 0);
        Assert.Equal([2], timers.Active(sim).Select(t => t.Id)); // only player 0's, at player 0's turn end
        end.Run(sim, 1);
        Assert.Empty(timers.Active(sim));
    }

    [Fact]
    public void A_cities_timers_follow_their_kind_when_it_is_captured_seceded_or_razed()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        var city = AddCity(sim, 0, 14);
        var held = TimerHolder.OfCity(city.Id) with { Player = 0 };
        var ends = timers.Start(sim, Timer("probe:replace", held, 0, "ends"));
        var keeps = timers.Start(sim, Timer("probe:keep", held));
        var transfers = timers.Start(sim, Timer("probe:transfer", held));
        var empire = timers.Start(sim, Timer("probe:replace", TimerHolder.OfPlayer(0)));
        Log.Clear();

        sim.State.SetWar(0, 1, true);
        sim.Rules().CityCapture.Capture(sim, city.Id, 1);
        Assert.Null(timers.Find(sim, ends.Id));
        Assert.Equal(keeps, timers.Find(sim, keeps.Id));
        Assert.Equal(1, timers.Find(sim, transfers.Id)!.Holder.Player);
        Assert.Equal(city.Id, timers.Find(sim, transfers.Id)!.Holder.City);
        Assert.NotNull(timers.Find(sim, empire.Id));
        Assert.Equal(["ended 1 ends HolderLost", "event ended 1 HolderLost"], Log);

        // A city that is destroyed takes every timer with it, whatever its kind says.
        sim.Events.Publish(new CityRazed(city.Id, 1, city.Position));
        Assert.Equal([empire.Id], timers.Active(sim).Select(t => t.Id));
    }

    [Fact]
    public void A_pair_holder_and_its_value_are_found_by_both_teams()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        timers.Start(sim, Timer("probe:replace", TimerHolder.OfTeams(0, 1), sim.State.Turn + 4, value: 150));
        Assert.Equal(150, timers.Find(sim, "probe:replace", TimerHolder.OfTeams(0, 1))!.Value);
        Assert.False(timers.IsActive(sim, "probe:replace", TimerHolder.OfTeams(1, 0)));
        Assert.True(timers.IsActive(sim, "probe:replace", TimerHolder.OfTeams(0, 1)));
    }

    [Fact]
    public void Cancel_ends_one_timer_by_id_with_its_hooks()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        var started = timers.Start(sim, Timer("probe:replace", TimerHolder.OfPlayer(0)));
        Log.Clear();
        Assert.True(timers.Cancel(sim, started.Id));
        Assert.False(timers.Cancel(sim, started.Id));
        Assert.Equal(["ended 1 probe:replace Cancelled", "event ended 1 Cancelled"], Log);
    }

    [Fact]
    public void Pace_scales_the_duration_and_an_unregistered_kind_is_refused()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        Assert.Equal(sim.State.Turn + GamePace.Scale(sim, 10), timers.Expiry(sim, 10));
        Assert.Equal(sim.State.Turn + 1 + GamePace.Scale(sim, 10), timers.Expiry(sim, 10, lead: 1));
        Assert.Throws<ArgumentException>(() => timers.Start(sim, Timer("probe:nothing", TimerHolder.OfPlayer(0))));
        Assert.Throws<ArgumentException>(() => timers.Start(sim, Timer("", TimerHolder.OfPlayer(0))));
    }

    [Fact]
    public void Two_kinds_with_one_id_fail_to_load()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), [Probe(new DuplicateKindMod(), "probe_twice")]));
        Assert.Contains("probe:same", error.Message);
    }

    [Fact]
    public void Saved_timers_reload_with_identical_hashes_and_keep_applying()
    {
        var sim = Scenario();
        var timers = sim.Rules().Timers;
        var city = AddCity(sim, 0, 14);
        timers.Start(sim, new GameTimer
        {
            Kind = "probe:stack", Key = "probe:city", Holder = new() { Player = 0, City = city.Id, Plot = 14 }, ExpiresTurn = sim.State.Turn + 3,
            Effects = [Happiness(2), new() { Scope = ModifierScope.Plot, Key = "yield.flat.commerce", Amount = 1 }],
        });
        timers.Start(sim, new GameTimer { Kind = "probe:stack", Key = "probe:empire", Holder = TimerHolder.OfPlayer(0), Effects = [Happiness(1)] });
        timers.Start(sim, Timer("probe:replace", TimerHolder.OfTeams(0, 1), sim.State.Turn + 6, value: 130));

        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(timers.Active(sim), loaded.Rules().Timers.Active(loaded), new TimerComparer());
        Assert.Equal(3, CityLedger(loaded, loaded.State.CityById(city.Id)).Total("cities:happiness"));
        Assert.Equal(130, loaded.Rules().Timers.Find(loaded, "probe:replace", TimerHolder.OfTeams(0, 1))!.Value);
        var next = loaded.Rules().Timers.Start(loaded, new GameTimer { Kind = "probe:stack", Key = "probe:later", Holder = TimerHolder.OfPlayer(0) });
        Assert.Equal(4, next.Id);

        for (int i = 0; i < 4; i++) { sim.AdvanceTurn(); loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim)); }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(["probe:empire", "probe:replace"], loaded.Rules().Timers.Active(loaded).Select(e => e.Source));
    }

    private sealed class TimerComparer : IEqualityComparer<GameTimer>
    {
        public bool Equals(GameTimer? a, GameTimer? b) => a is not null && b is not null
            && (a with { Effects = [] }) == (b with { Effects = [] })
            && a.Effects.Select(e => (e.Scope, e.Key, e.Target, e.Amount)).SequenceEqual(b.Effects.Select(e => (e.Scope, e.Key, e.Target, e.Amount)));
        public int GetHashCode(GameTimer timer) => timer.Id;
    }
}
