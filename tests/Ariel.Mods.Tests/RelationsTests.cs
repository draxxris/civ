using Ariel.Composition;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>Relations are the base game's: contact, war and peace, and the memory ledger work without the Diplomacy
/// mod, and any mod can add a kind of memory that fades on the base schedule and that attitudes weigh by data.</summary>
public sealed class RelationsTests
{
    private const string Insult = "memory_probe:insult";
    private static string Root(string folder, string name) => Path.Combine(AppContext.BaseDirectory, folder, name);
    private static string Rules => Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder);

    /// <summary>The base game and the memory probe, and no feature mod at all.</summary>
    private static readonly Lazy<ContentDb> BaseWithProbe = new(() =>
        GameContent.Build(Rules, ModLoader.FromDirectories([Root("test-mods", "MemoryKind")])));

    /// <summary>The same with the Diplomacy mod, which turns memories into attitude.</summary>
    private static readonly Lazy<ContentDb> DiplomacyWithProbe = new(() =>
        GameContent.Build(Rules, ModLoader.FromDirectories([Root(GameContent.OptionalModsFolder, "Diplomacy"), Root("test-mods", "MemoryKind")])));

    private static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);
    private static void Reject(Simulation sim, GameCommand command, string reason)
    {
        ulong before = sim.ComputeHash();
        Assert.False(sim.TryApply(command, out var error));
        Assert.Contains(reason, error!);
        Assert.Equal(before, sim.ComputeHash());
    }
    private static GameCommand Command(Simulation sim, string type, int player, int target) =>
        new CommandCodec(sim.Content).Decode($"{{\"type\":\"{type}\",\"data\":{{\"player\":{player},\"target\":{target}}}}}");
    /// <summary>The relations turn system: contact from current sight, then memories fade.</summary>
    private static void EndTurn(Simulation sim)
    {
        sim.State.Turn++;
        sim.Content.TurnSystems.All.Single(s => s.Id == "relations:turn").Run(sim);
    }

    [Fact]
    public void Contact_war_and_peace_work_without_the_diplomacy_mod()
    {
        var sim = GameFactory.Scenario(BaseWithProbe.Value);
        var relations = sim.Rules().Relations;
        Assert.Null(sim.Rules().TeamAgreements); Assert.Null(sim.Rules().Dispositions); Assert.Null(sim.Rules().Exchanges);
        int a = sim.TeamOf(0).Id, b = sim.TeamOf(1).Id;

        Reject(sim, new DeclareWarCommand { Player = 0, Target = 1 }, "teams have not met");
        sim.State.Plots[41].Owner = 1;
        sim.State.Plots[41].Visible.Add(a);
        relations.DiscoverContacts(sim);
        Assert.True(relations.HasMet(sim, a, b));

        // Without an agreements mod, foreign territory is closed at peace.
        var scout = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = sim.State.Grid.HexOf(new(40)) };
        sim.State.AddUnit(scout); sim.Rules().Units.Initialize(sim, scout.Id);
        Assert.False(relations.MayEnter(sim, a, b));
        Assert.Equal("Closed borders", sim.Rules().Movement.CanEnter(sim, scout.Id, new(40), new(41)));

        Apply(sim, new DeclareWarCommand { Player = 0, Target = 1 });
        Assert.Contains(b, sim.TeamOf(0).Wars); Assert.Contains(a, sim.TeamOf(1).Wars);
        Assert.Contains(sim.Events.Published, e => e is WarDeclared { AggressorTeam: var x, DefenderTeam: var y, Offensive: true } && x == a && y == b);
        Assert.Equal(1, relations.Memories(sim, 1, 0).GetValueOrDefault(BaseMemories.DeclaredWar));
        Assert.Empty(relations.Memories(sim, 0, 1));
        Assert.Equal(1, relations.Relation(sim, a, b)!.OffensiveWarsBy(a));

        Reject(sim, new MakePeaceCommand { Player = 1, Target = 0 }, "war is too recent");
        sim.State.Turn += RelationRules.MinimumWarTurns;
        Apply(sim, new MakePeaceCommand { Player = 1, Target = 0 });
        Assert.Empty(sim.TeamOf(0).Wars);
        Reject(sim, new DeclareWarCommand { Player = 0, Target = 1 }, "peace treaty is too recent");
        sim.State.Turn += RelationRules.PeaceCooldownTurns;
        Apply(sim, new DeclareWarCommand { Player = 0, Target = 1 });

        var restored = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), restored.ComputeHash());
        Assert.Equal(2, restored.Rules().Relations.Memories(restored, 1, 0)[BaseMemories.DeclaredWar]);
    }

    [Fact]
    public void There_is_exactly_one_way_to_declare_war_and_make_peace()
    {
        foreach (var content in new[] { BaseWithProbe.Value, DiplomacyWithProbe.Value })
        {
            var wars = content.CommandTypes.All.Where(c => c.Type == typeof(DeclareWarCommand) || c.Type == typeof(MakePeaceCommand)
                || c.Id.EndsWith(":declare_war", StringComparison.Ordinal) || c.Id.EndsWith(":make_peace", StringComparison.Ordinal))
                .Select(c => c.Id).Order(StringComparer.Ordinal);
            Assert.Equal(["relations:declare_war", "relations:make_peace"], wars);
        }
        var sim = GameFactory.Scenario(DiplomacyWithProbe.Value);
        Assert.Throws<KeyNotFoundException>(() => Command(sim, "ai:declare_war", 0, 1));
        Assert.Throws<KeyNotFoundException>(() => Command(sim, "diplomacy:declare_war", 0, 1));
    }

    [Fact]
    public void A_mod_records_its_own_memory_kind_which_fades_on_the_base_schedule()
    {
        var sim = GameFactory.Scenario(BaseWithProbe.Value);
        var relations = sim.Rules().Relations;
        var kind = sim.Content.Get<MemoryKindDef>(Insult);
        Assert.Equal("insult", kind.LeaderKey);

        var insult = Command(sim, "memory_probe:insult", 0, 1);
        Apply(sim, insult); Apply(sim, insult);
        Assert.Equal(2, relations.Memories(sim, 1, 0)[Insult]);
        Assert.Empty(relations.Memories(sim, 0, 1));

        // A decay of one is a certain roll: one count fades every turn, and only once a turn.
        EndTurn(sim);
        Assert.Equal(1, relations.Memories(sim, 1, 0)[Insult]);
        sim.Content.TurnSystems.All.Single(s => s.Id == "relations:turn").Run(sim);
        Assert.Equal(1, relations.Memories(sim, 1, 0)[Insult]);
        EndTurn(sim);
        Assert.False(relations.Memories(sim, 1, 0).ContainsKey(Insult));

        Assert.Throws<InvalidOperationException>(() => relations.Remember(sim, 1, 0, "memory_probe:unregistered"));
    }

    [Fact]
    public void With_diplomacy_a_mods_memory_moves_attitude_by_its_data_weight()
    {
        var sim = GameFactory.Scenario(DiplomacyWithProbe.Value);
        var diplomacy = sim.Service<Ariel.Mods.Diplomacy.IDiplomacyService>()!;
        long Weighed() => diplomacy.GetAttitude(sim, 1, 0).Ledger.Lines.Where(l => l.Source == "diplomacy:memory:" + Insult).Sum(l => l.Amount);
        int before = diplomacy.GetAttitude(sim, 1, 0).Score;
        Assert.Equal(0, Weighed());

        // The leader's table does not name the kind, so its own weight applies: -300% per count.
        Assert.False(sim.Content.Get<LeaderDef>(sim.State.PlayerById(1).Leader).MemoryAttitudePercents.ContainsKey("insult"));
        var insult = Command(sim, "memory_probe:insult", 0, 1);
        Apply(sim, insult); Apply(sim, insult);
        Assert.Equal(-6, Weighed());
        Assert.Equal(before - 6, diplomacy.GetAttitude(sim, 1, 0).Score);
        Assert.Equal(sim.Rules().Relations.DispositionToward(sim, 1, 0), diplomacy.GetAttitude(sim, 1, 0).Tier);

        EndTurn(sim);
        Assert.Equal(-3, Weighed());
    }
}
