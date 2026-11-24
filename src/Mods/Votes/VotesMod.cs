using Ariel.Ai;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.Mods.Votes;

/// <summary>World councils: the United Nations and the Apostolic Palace. A building founds a council; its members
/// elect a secretary, who puts resolutions to them: forced civics, free trade, open borders and defensive pacts for
/// all, peace, sanctions or war against one player, and in the end a vote for the winner of the game. The council
/// answers the base game's trade-terms and civic-restriction seams, decides the diplomatic victory (a victory
/// condition the base game asks), and contributes the computer player's votes.</summary>
public sealed class VotesMod : IPreparableMod
{
    private static readonly ModJsonFile<VoteSourceDef[]> Sources = new("data/vote-source.json");
    private static readonly ModJsonFile<VoteDef[]> Votes = new("data/vote.json");
    private static readonly ModJsonFile<BuildingVotesDef[]> Buildings = new("data/building-votes.json");
    /// <summary>The council a religious building seats (the Apostolic Palace), registered only with Religion, whose
    /// building it is.</summary>
    private static readonly ModJsonFile<BuildingVotesDef[]> ReligiousBuildings = new("data/religion/building-votes.json");
    private static readonly ModJsonFile<VictoryDef[]> Victories = new("data/victory.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Sources, Votes, Buildings, ReligiousBuildings, Victories];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Sources);
        ContentRegistration.Load(context, Votes);
        ContentRegistration.Load(context, Buildings);
        if (context.IsLoaded("religion")) ContentRegistration.Load(context, ReligiousBuildings);
        ContentRegistration.Load(context, Victories);
        var service = new CouncilService();
        context.RegisterService<IVotesService>("councils", service);
        context.RegisterService<ITradeTerms>("trade_terms", service);
        context.RegisterService<ICivicRestrictions>("civic_restrictions", service);
        context.RegisterService<IVictoryCondition>("council_victory", new CouncilVictory());
        context.RegisterService<IAiPlanner>("planner", new CouncilPlanner());
        context.RegisterService<Ariel.Presentation.IGameScreen>("councils_screen", new CouncilScreen());
        context.RegisterService<Ariel.Presentation.INotificationSource>("notices", new CouncilNotices());
        context.RegisterService<Ariel.Presentation.IPediaSource>("pedia", new CouncilPedia());
        context.RegisterStateComponent<VotesGameState>("game_state");
        context.RegisterCommand<ProposeResolutionCommand>("propose_resolution");
        context.RegisterCommand<CastVoteCommand>("cast_vote");
        context.TurnSystems.Register(new CouncilSessions(service));
    }
}

/// <summary>The diplomatic victory: won by a council's victory resolution, which the council awards through
/// <see cref="IVictoryService.Award"/>. The victory check never finds a winner by it.</summary>
internal sealed class CouncilVictory : IVictoryCondition
{
    internal const string ConditionId = "votes:council";
    public string Id => ConditionId;
    public int? Winner(Simulation sim, VictoryDef victory, IVictoryStandings standings, bool turnEnd) => null;
}
