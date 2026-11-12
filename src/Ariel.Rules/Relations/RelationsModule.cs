using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Rules.Relations;

/// <summary>Contact, war and peace between teams, border passage, and the memory ledger: what one player remembers
/// of another and how it fades. Every module reads these, so none of the others owns them. Agreements and attitudes
/// belong to mods, which answer <see cref="ITeamAgreements"/> and <see cref="IDispositions"/>.</summary>
public sealed class RelationsModule : IPreparableMod
{
    private static readonly ModJsonFile<MemoryKindDef[]> Memories = new("data/memory-kind.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Memories];

    public void Register(IModContext context)
    {
        MemoryKinds.Load(context, Memories);
        var service = new RelationsService();
        context.RegisterService<IRelationsService>("relations", service);
        context.RegisterStateComponent<RelationsState>("state");
        context.RegisterCommand<DeclareWarCommand>("declare_war");
        context.RegisterCommand<MakePeaceCommand>("make_peace");
        context.Subscribe<TurnStarted>("contact_turn", (sim, _) => service.DiscoverContacts(sim));
        context.Subscribe<CityFounded>("contact_city", (sim, _) => service.DiscoverContacts(sim));
        context.Subscribe<UnitCreated>("contact_unit", (sim, _) => service.DiscoverContacts(sim));
        context.Subscribe<CityRazed>("memory_raze", RelationsService.RememberRaze);
        context.TurnSystems.Register(new RelationsTurn(service));
    }
}

/// <summary>After team sight is renewed, teams meet whoever they now see, and then memories fade. It runs before the
/// end-of-turn hooks (<see cref="TurnPhase.GameEnd"/>), so a mod's own end-of-turn relation bookkeeping already sees
/// this turn's contacts.</summary>
internal sealed class RelationsTurn(RelationsService service) : ITurnSystem
{
    public string Id => "relations:turn";
    public int Order => TurnPhase.Visibility + 1;
    public void Run(Simulation sim)
    {
        service.DiscoverContacts(sim);
        RelationsService.FadeMemories(sim);
    }
}
