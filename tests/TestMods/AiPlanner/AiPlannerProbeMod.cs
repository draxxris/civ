using Ariel.Ai;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.TestMods.AiPlanner;

/// <summary>Contributes a planner to the standard computer player. Each AI turn it issues its own command, which
/// notes the turn and the planner slot it ran in on the player.</summary>
public sealed class AiPlannerProbeMod : IMod
{
    /// <summary>Between religion and the city planner, where feature planners that spend units run.</summary>
    public const int Order = AiPlannerOrder.Religion + 150;

    public void Register(IModContext context)
    {
        context.RegisterStateComponent<ProbeLog>("log");
        context.RegisterCommand<NoteAiTurnCommand>("note_turn");
        context.RegisterService<IAiPlanner>("planner", new ProbePlanner());
    }

    private sealed class ProbePlanner : IAiPlanner
    {
        public string Id => "ai_planner_probe:planner";
        public int Order => AiPlannerProbeMod.Order;
        public void Plan(AiContext ctx) => ctx.Issue(new NoteAiTurnCommand { Player = ctx.PlayerId, Cities = ctx.Cities.Count });
    }
}

/// <summary>What the probe planner noted on a player.</summary>
public sealed class ProbeLog
{
    public List<int> Turns { get; set; } = [];
    public int Cities { get; set; }
}

/// <summary>Note this turn on the player.</summary>
public sealed class NoteAiTurnCommand : GameCommand
{
    public int Cities { get; set; }

    public override string? Validate(Simulation sim) =>
        sim.State.Players.Any(p => p.Id == Player) ? null : $"no player {Player}";

    public override void Apply(Simulation sim)
    {
        var player = sim.State.PlayerById(Player);
        var log = player.Extensions.Get<ProbeLog>(sim.Content) ?? new ProbeLog();
        log.Turns.Add(sim.State.Turn);
        log.Cities = Cities;
        player.Extensions.Set(sim.Content, log);
    }
}
