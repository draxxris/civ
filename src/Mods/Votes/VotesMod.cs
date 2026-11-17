using Ariel.Ai;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.Mods.Votes;

/// <summary>World councils: the United Nations and the Apostolic Palace. A building founds a council; its members
/// elect a secretary, who puts resolutions to them: forced civics, free trade, open borders and defensive pacts for
/// all, peace, sanctions or war against one player, and in the end a vote for the winner of the game. The council
/// answers the base game's trade-terms and civic-restriction seams, and contributes the computer player's votes.</summary>
public sealed class VotesMod : IPreparableMod
{
    private static readonly ModJsonFile<VoteSourceDef[]> Sources = new("data/vote-source.json");
    private static readonly ModJsonFile<VoteDef[]> Votes = new("data/vote.json");
    private static readonly ModJsonFile<BuildingVotesDef[]> Buildings = new("data/building-votes.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Sources, Votes, Buildings];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Sources);
        ContentRegistration.Load(context, Votes);
        ContentRegistration.Load(context, Buildings);
        var service = new CouncilService();
        context.RegisterService<IVotesService>("councils", service);
        context.RegisterService<ITradeTerms>("trade_terms", service);
        context.RegisterService<ICivicRestrictions>("civic_restrictions", service);
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
