using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Ai;

/// <summary>The base module <c>ai</c>: the computer player, its planners, its saved intent, and its replayable planning
/// commands. Linked into the host like the rules modules; mods add planners through
/// <see cref="IAiPlanner"/>.</summary>
public sealed class AiModule : IMod
{
    /// <summary>The module id, which namespaces the AI's commands, state and planners.</summary>
    public const string Id = "ai";
    /// <summary>Folder under the host's <c>rules/</c> that holds this module's staged manifest.</summary>
    public const string Folder = "Ai";

    public void Register(IModContext context)
    {
        context.PlayerAIs.Register(new StandardAi());
        StandardPlanners.Register(context);
        context.RegisterService<IAiValuer<CivicSubject>>("diplomacy_civic_valuer", new DiplomacyCivicValuer());
        context.RegisterService<IDealAppraiser>("deal_appraiser", new DealAppraiser());
        context.RegisterStateComponent<AiPlayerState>("player_state");
        context.RegisterStateComponent<AiTeamState>("team_state");
        context.RegisterCommand<ReviewCivicGoalCommand>("review_civic_goal");
        context.RegisterCommand<ReviewCampaignGoalsCommand>("review_campaign_goals");
        context.RegisterCommand<PrepareSettlerMissionsCommand>("prepare_settler_missions");
        context.RegisterCommand<PrepareWorkerMissionsCommand>("prepare_worker_missions");
        context.RegisterCommand<ForgetUnitMissionCommand>("forget_unit_mission");
        context.RegisterCommand<PruneAiIntentCommand>("prune_intent");
        context.RegisterCommand<PrepareWarCommand>("prepare_war");
        context.Subscribe<CombatResolved>("war_score_combat", WarRecord.Combat);
        context.Subscribe<CityCaptured>("war_score_capture", WarRecord.Capture);
        context.Subscribe<WarDeclared>("war_score_declared", WarRecord.Declared);
        context.Subscribe<PeaceSigned>("war_score_peace", WarRecord.Peace);
    }
}
