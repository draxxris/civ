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
        public IReadOnlyList<string>? UnreadDomains { get; init; } = ["probe:culture"];
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
        var treasury = new TreasurySource { UnreadDomains = treasuryUnread ?? ["probe:culture"] };
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

    private sealed class UndeclaredWrite : GameCommand
    {
        public override string? Validate(Simulation sim) => null;
        public override void Apply(Simulation sim) => sim.State.PlayerById(Player).Culture++;
    }

    [Fact]
    public void Verification_catches_a_writer_outside_its_domains()
    {
        var (sim, _, _) = Game();
        sim.VerifyWrites = true;
        Assert.True(sim.TryApply(new SetCulture { Player = 0, Value = 3 }, out _));
        var error = Assert.Throws<InvalidOperationException>(() => sim.TryApply(new Sneaky { Player = 0 }, out _));
        Assert.Contains("Sneaky", error.Message);
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
