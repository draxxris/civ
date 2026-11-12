using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.TestMods.MemoryKind;

/// <summary>Adds a kind of memory of its own and an act that leaves it: the insulted player remembers the insult. The
/// kind fades on the base game's schedule, and a mod that judges attitudes weighs it by its data.</summary>
public sealed class MemoryProbeMod : IPreparableMod
{
    /// <summary>The kind this mod registers in <c>data/memory-kind.json</c>.</summary>
    public const string Insult = "memory_probe:insult";
    private static readonly ModJsonFile<MemoryKindDef[]> Memories = new("data/memory-kind.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Memories];

    public void Register(IModContext context)
    {
        MemoryKinds.Load(context, Memories);
        context.RegisterCommand<InsultCommand>("insult");
    }
}

/// <summary>Insult another player, who remembers it.</summary>
public sealed class InsultCommand : GameCommand
{
    public int Target { get; init; }

    public override string? Validate(Simulation sim) =>
        sim.State.Players.Any(p => p.Id == Player) && sim.State.Players.Any(p => p.Id == Target) && Player != Target ? null : "no such players";

    public override void Apply(Simulation sim) => sim.Rules().Relations.Remember(sim, Target, Player, MemoryProbeMod.Insult);
}
