using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Ai;

/// <summary>The computer player, its saved intent, and its replayable planning and diplomacy commands.</summary>
public sealed class AiMod : IMod
{
    public void Register(IModContext context)
    {
        context.PlayerAIs.Register(new StandardAi());
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
