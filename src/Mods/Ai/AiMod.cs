using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.Ai;

/// <summary>The computer player. Registers <c>ai:standard</c> and the two war/peace commands it issues.</summary>
public sealed class AiMod : IMod
{
    public void Register(IModContext context)
    {
        context.PlayerAIs.Register(new StandardAi());
        context.RegisterStateComponent<AiTeamState>("team_state");
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
