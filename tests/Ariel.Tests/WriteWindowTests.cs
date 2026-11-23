using System.Text.Json.Nodes;
using Ariel.BaseGame;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Tests;

/// <summary>Write windows, state domains and the per-source modifier cache that relies on them.</summary>
public sealed class WriteWindowTests
{
    private sealed class TestMod(Action<IModContext> register) : IMod
    {
        public void Register(IModContext ctx) => register(ctx);
    }
    private static ModSource Source(string id, Action<IModContext> register) => new()
    {
        Manifest = new ModManifest { Id = id, Version = "1", Dependencies = ["base"] },
        Instance = new TestMod(register), Root = AppContext.BaseDirectory,
    };
    private static ContentDb Content(params ModSource[] extra) => ModLoader.Build(extra.Prepend(
        ModLoader.FromAssembly(new ModManifest { Id = "base", Version = "1" }, typeof(TestRulesetMod).Assembly, AppContext.BaseDirectory)));

    private sealed record Poked(int Player) : DomainEvent;

    /// <summary>Reads a player's treasury and never its culture.</summary>
    private sealed class TreasurySource : IModifierSource
    {
        public int Calls;
        public IReadOnlyList<string>? Unread { get; init; } = ["probe:culture"];
        public IReadOnlyList<string>? UnreadDomains(ContentDb content) => Unread;
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope != ModifierScope.Player) return;
            Interlocked.Increment(ref Calls);
            sink.Add("treasury", "probe:treasury", context.Sim.State.PlayerById(context.Player).Treasury);
            sink.AddYield("treasury", new(context.Sim.State.PlayerById(context.Player).Treasury, 0, 0));
        }
    }

    /// <summary>Declares nothing, so any write makes it compute again.</summary>
    private sealed class CultureSource : IModifierSource
    {
        public int Calls;
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope != ModifierScope.Player) return;
            Interlocked.Increment(ref Calls);
            sink.Add("culture", "probe:culture", context.Sim.State.PlayerById(context.Player).Culture);
        }
    }

    private sealed class Script(Action<Simulation> body) : GameCommand
    {
        public override IReadOnlyList<string>? WriteDomains => [];
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim) => body(sim);
    }
    private sealed class SetCulture : GameCommand
    {
        public long Value { get; init; }
        public bool Poke { get; init; }
        public override IReadOnlyList<string>? WriteDomains => ["probe:culture"];
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim)
        {
            sim.State.PlayerById(Player).Culture = Value;
            if (Poke) sim.Events.Publish(new Poked(Player));
        }
    }
    private sealed class SetTreasury : GameCommand
    {
        public long Value { get; init; }
        public override IReadOnlyList<string>? WriteDomains => ["probe:treasury"];
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim) => sim.State.PlayerById(Player).Treasury = Value;
    }
    /// <summary>Declares culture but writes the treasury.</summary>
    private sealed class Sneaky : GameCommand
    {
        public override IReadOnlyList<string>? WriteDomains => ["probe:culture"];
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim) => sim.State.PlayerById(Player).Treasury += 7;
    }

    private static (Simulation Sim, TreasurySource Treasury, CultureSource Culture) Game(bool subscribe = false, IReadOnlyList<string>? treasuryUnread = null)
    {
        var treasury = new TreasurySource { Unread = treasuryUnread ?? ["probe:culture"] };
        var culture = new CultureSource();
        var mod = Source("probe", ctx =>
        {
            ctx.RegisterStateDomain("culture", "Players/*/Culture");
            ctx.RegisterStateDomain("treasury", "Players/*/Treasury");
            ctx.RegisterService<IModifierSource>("treasury", treasury);
            ctx.RegisterService<IModifierSource>("culture", culture);
            if (subscribe) ctx.Subscribe<Poked>("poked", (_, _) => { });
        });
        var sim = new Simulation(Content(mod), new SimulationSettings { PlayerCount = 1, Civilizations = ["base:builders"] });
        return (sim, treasury, culture);
    }

    private static ModifierSink Player(Simulation sim) => ModifierSink.Collect(new(sim, ModifierScope.Player, 0));

    [Fact]
    public void A_contribution_is_reused_across_a_write_to_a_domain_its_source_never_reads()
    {
        var (sim, treasury, culture) = Game();
        sim.State.PlayerById(0).Treasury = 40;
        int t = 0, c = 0;
        long seenTreasury = 0, seenCulture = 0;
        Assert.True(sim.TryApply(new Script(s =>
        {
            Player(s);
            (t, c) = (treasury.Calls, culture.Calls);
            Assert.True(s.TryApply(new SetCulture { Player = 0, Value = 9 }, out _));
            var after = Player(s);
            Assert.Equal(t, treasury.Calls); // culture is declared unread: reused
            Assert.Equal(c + 1, culture.Calls); // undeclared: computed again
            Assert.Equal(9, after.Total("probe:culture"));
            Assert.Equal(new YieldVector(40, 0, 0), after.FlatYield); // reused lines rebuild the typed vectors
            Assert.True(s.TryApply(new SetTreasury { Player = 0, Value = 55 }, out _));
            var third = Player(s);
            Assert.Equal(t + 1, treasury.Calls);
            seenTreasury = third.Total("probe:treasury");
            seenCulture = third.Total("probe:culture");
        }) { Player = 0 }, out _));
        Assert.Equal(55, seenTreasury);
        Assert.Equal(9, seenCulture);
    }

    [Fact]
    public void Nothing_is_reused_outside_kernel_work_where_a_host_may_edit_the_state_directly()
    {
        var (sim, treasury, _) = Game();
        Player(sim);
        sim.State.PlayerById(0).Treasury = 12;
        Assert.Equal(12, Player(sim).Total("probe:treasury"));
        Assert.Equal(2, treasury.Calls);
        // A command run between two direct edits starts from nothing remembered.
        long inside = 0;
        Assert.True(sim.TryApply(new Script(s => inside = Player(s).Total("probe:treasury")) { Player = 0 }, out _));
        sim.State.PlayerById(0).Treasury = 13;
        Assert.True(sim.TryApply(new Script(s => inside = Player(s).Total("probe:treasury")) { Player = 0 }, out _));
        Assert.Equal(13, inside);
    }

    [Fact]
    public void An_undeclared_writer_and_a_handled_event_count_as_writing_everything()
    {
        var (sim, treasury, _) = Game(subscribe: true);
        Assert.True(sim.TryApply(new Script(s =>
        {
            Player(s);
            int before = treasury.Calls;
            Assert.True(s.TryApply(new SetCulture { Player = 0, Value = 1, Poke = true }, out _));
            Player(s);
            Assert.Equal(before + 1, treasury.Calls); // the subscriber may have written anything
            Assert.True(s.TryApply(new UndeclaredWrite { Player = 0 }, out _));
            Player(s);
            Assert.Equal(before + 2, treasury.Calls);
        }) { Player = 0 }, out _));
    }

    /// <summary>Declares culture, and widens before a rare write to the treasury.</summary>
    private sealed class CultureThenTreasury : GameCommand
    {
        public bool Widen { get; init; }
        public long Seen { get; private set; }
        public override IReadOnlyList<string>? WriteDomains => ["probe:culture"];
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim)
        {
            sim.State.PlayerById(Player).Culture++;
            if (Widen) sim.WidenWriteWindow();
            sim.State.PlayerById(Player).Treasury += 5;
            Seen = ModifierSink.Collect(new(sim, ModifierScope.Player, Player)).Total("probe:treasury");
        }
    }

    [Fact]
    public void A_widened_window_counts_as_writing_everything_from_then_on()
    {
        var (sim, treasury, _) = Game();
        sim.VerifyWrites = true;
        var widened = new CultureThenTreasury { Player = 0, Widen = true };
        Assert.True(sim.TryApply(new Script(s =>
        {
            Player(s);
            int before = treasury.Calls;
            Assert.True(s.TryApply(widened, out _));
            Assert.Equal(before + 1, treasury.Calls); // inside the widened window the treasury is read afresh
            Assert.Equal(5, widened.Seen);
            Player(s);
            Assert.Equal(before + 2, treasury.Calls); // and closing it stamped everything
        }) { Player = 0 }, out _));
        Assert.Equal(5, Player(sim).Total("probe:treasury"));

        // Without the widening the same write is outside the declaration, and verification says so.
        var (strict, _, _) = Game();
        strict.VerifyWrites = true;
        var error = Assert.Throws<InvalidOperationException>(() => strict.TryApply(new CultureThenTreasury { Player = 0 }, out _));
        Assert.Contains("CultureThenTreasury", error.Message);
        // Outside any window it does nothing.
        sim.WidenWriteWindow();
    }

    private sealed class UndeclaredWrite : GameCommand
    {
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim) => sim.State.PlayerById(Player).Culture++;
    }

    [Fact]
    public void Verification_catches_a_writer_outside_its_domains_and_a_source_that_reads_what_it_declares_unread()
    {
        var (sim, _, _) = Game();
        sim.VerifyWrites = true;
        Assert.True(sim.TryApply(new SetCulture { Player = 0, Value = 3 }, out _));
        var error = Assert.Throws<InvalidOperationException>(() => sim.TryApply(new Sneaky { Player = 0 }, out _));
        Assert.Contains("Sneaky", error.Message);

        // A source claiming not to read the treasury keeps a stale line across a treasury write.
        var (liar, _, _) = Game(treasuryUnread: ["probe:treasury"]);
        liar.VerifyWrites = true;
        var stale = Assert.Throws<InvalidOperationException>(() => liar.TryApply(new Script(s =>
        {
            Player(s);
            Assert.True(s.TryApply(new SetTreasury { Player = 0, Value = 99 }, out _));
            Player(s);
        }) { Player = 0 }, out _));
        Assert.Contains("TreasurySource", stale.Message);
    }

    [Fact]
    public void Verified_turns_run_with_every_system_inside_its_window()
    {
        var (sim, _, _) = Game();
        sim.VerifyWrites = true;
        var plain = Game().Sim;
        for (int i = 0; i < 3; i++) { sim.AdvanceTurn(); plain.AdvanceTurn(); }
        Assert.Equal(plain.ComputeHash(), sim.ComputeHash());
    }

    [Fact]
    public void A_declared_window_stamps_only_its_domains_and_an_undeclared_one_stamps_everything()
    {
        var (sim, _, _) = Game(subscribe: true);
        ulong culture = sim.Content.StateDomains.Mask(["probe:culture"], out _);
        ulong treasury = sim.Content.StateDomains.Mask(["probe:treasury"], out _);
        Assert.True(sim.TryApply(new Script(s =>
        {
            long stamp = s.WriteStamp;
            using (s.OpenWriteWindow(["probe:culture"], "culture"))
            {
                Assert.True(s.WritingNow(0));
                Assert.False(s.WritingNow(culture));
            }
            Assert.True(s.WrittenSince(0, stamp));
            Assert.False(s.WrittenSince(culture, stamp));
            stamp = s.WriteStamp;
            using (s.OpenWriteWindow(["probe:culture"], "outer"))
            using (s.OpenWriteWindow(["probe:treasury"], "inner")) Assert.True(s.WritingNow(culture));
            Assert.True(s.WrittenSince(culture, stamp)); // the nested window wrote the treasury
            Assert.False(s.WrittenSince(culture | treasury, stamp));
            stamp = s.WriteStamp;
            using (s.OpenWriteWindow(null, "anything")) Assert.True(s.WritingNow(culture | treasury));
            Assert.True(s.WrittenSince(culture | treasury, stamp));
            stamp = s.WriteStamp;
            using (s.OpenWriteWindow(["probe:culture"], "poke"))
            {
                Assert.False(s.WritingNow(culture));
                s.Events.Publish(new Poked(0)); // a subscriber handles it, so the window may now write anything
                Assert.True(s.WritingNow(culture));
            }
            Assert.True(s.WrittenSince(culture | treasury, stamp));
        }) { Player = 0 }, out _));
    }

    [Fact]
    public void Every_reader_counts_a_private_domain_unread_and_a_reader_through_providers_keeps_only_what_all_declare()
    {
        var content = Content(Source("a", ctx =>
        {
            ctx.RegisterStateDomain("work", "Cities/*/Worked");
            ctx.RegisterStateDomain("food", "Cities/*/FoodStored");
            ctx.RegisterPrivateStateDomain("plans", "Players/*/Extensions/a:plans");
        }));
        var domains = content.StateDomains;
        ulong plans = domains.Mask(["a:plans"], out _), work = domains.Mask(["a:work"], out _);
        Assert.Equal(plans, domains.PrivateMask);
        Assert.Equal(plans, domains.UnreadMask(new CultureSource(), content)); // declares nothing, still never reads it
        Assert.Equal(plans | work, domains.UnreadMask(new TreasurySource { Unread = ["a:work"] }, content));

        IReadOnlyList<string> own = ["a:work", "a:food"];
        Assert.Equal(["a:work"], StateDomainRegistry.UnreadByAll(own, content, [new TreasurySource { Unread = ["a:work", "a:plans"] }]));
        Assert.Empty(StateDomainRegistry.UnreadByAll(own, content, [new TreasurySource { Unread = own }, new CultureSource()]));
        Assert.Empty(StateDomainRegistry.UnreadByAll(own, content, ["not a reader"]));
        Assert.Equal(own, StateDomainRegistry.UnreadByAll(own, content, []));
    }

    [Fact]
    public void An_event_published_outside_any_window_dispatches_inside_one()
    {
        var (sim, _, _) = Game(subscribe: true);
        ulong both = sim.Content.StateDomains.AllMask;
        long stamp = sim.WriteStamp;
        sim.Events.Publish(new Poked(0));
        Assert.True(sim.WrittenSince(both, stamp)); // the subscriber may have written anything
    }

    [Fact]
    public void Domains_belong_to_their_module_and_never_overlap()
    {
        Assert.Throws<ArgumentException>(() => Content(Source("a", ctx => ctx.RegisterStateDomain("x", "Cities/*/Worked")),
            Source("b", ctx => ctx.RegisterStateDomain("y", "Cities/*"))));
        Assert.Throws<ArgumentException>(() => Content(Source("a", ctx => ctx.RegisterStateDomain("x"))));
        Assert.Throws<ArgumentException>(() => Content(Source("a", ctx => ctx.RegisterStateDomain("x", "Cities//Worked"))));
        var content = Content(Source("a", ctx =>
        {
            ctx.RegisterStateDomain("work", "Cities/*/Worked", "Cities/*/Extensions/a:state/Manual");
            ctx.RegisterStateDomain("food", "Cities/*/FoodStored");
        }));
        Assert.True(content.StateDomains.Contains("a:work"));
        ulong mask = content.StateDomains.Mask(["a:food", "missing:domain"], out bool unknown);
        Assert.True(unknown);
        Assert.Equal(content.StateDomains.Mask(["a:food"], out _), mask);

        var node = JsonNode.Parse("""{"Cities":[{"Worked":[1],"FoodStored":3,"Extensions":{"a:state":{"Manual":true,"Other":1}}}]}""")!;
        content.StateDomains.Strip(node, content.StateDomains.Mask(["a:work"], out _));
        Assert.Equal("""{"Cities":[{"FoodStored":3,"Extensions":{"a:state":{"Other":1}}}]}""", node.ToJsonString());
    }
}
