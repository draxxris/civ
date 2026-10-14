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
    }
}
