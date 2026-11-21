using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Rules.Victory;

/// <summary>Score and victory: every player's score, the saved outcome, the check that ends a game, and the victories
/// that need no feature mod (conquest, domination, cultural, time). A mod adds another way to win by registering an
/// <see cref="IVictoryCondition"/> and a <see cref="VictoryDef"/> that names it, and asks
/// <see cref="IVictoryService.Evaluate"/> when something it owns may have decided the game.</summary>
public sealed class VictoryModule : IPreparableMod
{
    private static readonly ModJsonFile<VictoryDef[]> Victories = new("data/victory.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Victories];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Victories);
        var scores = new PlayerScores();
        var service = new VictoryService(scores);
        context.RegisterService<IPlayerScores>("scores", scores);
        context.RegisterService<IVictoryService>("victory", service);
        context.RegisterService<IVictoryCondition>("conquest", new ConquestVictory());
        context.RegisterService<IVictoryCondition>("domination", new DominationVictory());
        context.RegisterService<IVictoryCondition>("cultural", new CulturalVictory());
        context.RegisterService<IVictoryCondition>("time", new TimeVictory());
        context.RegisterService<Ariel.Presentation.IGameScreen>("screen", new VictoryScreen());
        context.RegisterService<Ariel.Presentation.INotificationSource>("notices", new VictoryNotices());
        context.RegisterService<Ariel.Presentation.IPediaSource>("victories", new VictoryPedia());
        context.RegisterStateComponent<VictoryGameState>("game_state");
        context.Subscribe<CityCaptured>("check_capture", (sim, _) => service.Evaluate(sim, false));
        context.Subscribe<CityRazed>("check_raze", (sim, _) => service.Evaluate(sim, false));
        context.TurnSystems.Register(new VictoryCheck(service));
    }
}

/// <summary>The victory check at the end of every turn, the only one that may end a game on time.</summary>
internal sealed class VictoryCheck(VictoryService service) : ITurnSystem
{
    public string Id => "victory:check";
    public int Order => TurnPhase.GameEnd;
    public void Run(Simulation sim) => service.Evaluate(sim, true);
}
