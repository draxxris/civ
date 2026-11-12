using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Ai;

/// <summary>The base module <c>ai</c>: the computer player, its planners, its saved intent, and its replayable planning
/// and diplomacy commands. Linked into the host like the rules modules; mods add planners through
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
        context.RegisterStateComponent<AiTeamState>("team_state");
        context.RegisterStateComponent<AiPlayerState>("player_state");
        context.RegisterCommand<ReviewCivicGoalCommand>("review_civic_goal");
        context.RegisterCommand<ReviewCampaignGoalsCommand>("review_campaign_goals");
        context.RegisterCommand<PrepareSettlerMissionsCommand>("prepare_settler_missions");
        context.RegisterCommand<PrepareWorkerMissionsCommand>("prepare_worker_missions");
        context.RegisterCommand<ForgetUnitMissionCommand>("forget_unit_mission");
        context.RegisterCommand<PruneAiIntentCommand>("prune_intent");
        context.RegisterCommand<DeclareWarCommand>("declare_war");
        context.RegisterCommand<MakePeaceCommand>("make_peace");
        context.Subscribe<DiplomaticWarDeclared>("diplomatic_war", (sim, war) =>
        {
            WarRules.State(sim, war.AggressorTeam).WarStarted[war.DefenderTeam] = sim.State.Turn;
            WarRules.State(sim, war.DefenderTeam).WarStarted[war.AggressorTeam] = sim.State.Turn;
        });
        context.Subscribe<DiplomaticPeaceSigned>("diplomatic_peace", (sim, peace) =>
        {
            var first = WarRules.State(sim, peace.FirstTeam);
            var second = WarRules.State(sim, peace.SecondTeam);
            first.WarStarted.Remove(peace.SecondTeam); second.WarStarted.Remove(peace.FirstTeam);
            first.PeaceStarted[peace.SecondTeam] = sim.State.Turn;
            second.PeaceStarted[peace.FirstTeam] = sim.State.Turn;
        });
    }
}
