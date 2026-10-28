using System.Text.Json.Serialization;
using Ariel.Game;

namespace Ariel.Mods.Ai;

internal static class IntentCommandRules
{
    internal static string? Validate(Simulation sim, int player) =>
        !sim.State.Players.Any(p => p.Id == player) ? "Unknown player" :
        sim.State.PlayerById(player).EndedTurn ? "Player has ended the turn" : null;
}

/// <summary>Bind an evaluated intent to its command. Replay evaluates live dependencies before writing state.</summary>
public abstract class AiPlanningCommand : GameCommand
{
    // The first application binds the result. Later applications must reproduce that decision, even if a later
    // command completes and removes the mission before the simulation's final commit fingerprint is compared.
    [JsonInclude] public string? ExpectedIntent { get; private set; }
    private string? _evaluated;

    public sealed override string? Validate(Simulation sim)
    {
        if (IntentCommandRules.Validate(sim, Player) is { } error) return error;
        _evaluated = Evaluate(sim);
        return ExpectedIntent is not null && ExpectedIntent != _evaluated ? "AI intent changed" : null;
    }

    public sealed override void Apply(Simulation sim)
    {
        if (_evaluated is null) throw new InvalidOperationException("Intent command was not validated");
        ExpectedIntent ??= _evaluated;
        Commit(sim);
    }

    protected abstract string Evaluate(Simulation sim);
    protected abstract void Commit(Simulation sim);
}

/// <summary>Reconsider civic intent when due, with live legality checks before any adoption.</summary>
public sealed class ReviewCivicGoalCommand : AiPlanningCommand
{
    private CivicReview _review = new(null, []);
    protected override string Evaluate(Simulation sim)
    {
        _review = new CivicPlanner(new(sim, Player)).Evaluate();
        return CanonicalJson.Digest(_review);
    }
    protected override void Commit(Simulation sim) => new CivicPlanner(new(sim, Player)).Commit(_review);
}

/// <summary>Reconsider the primary city for each active land campaign.</summary>
public sealed class ReviewCampaignGoalsCommand : AiPlanningCommand
{
    public int[] Areas { get; init; } = [];
    private SortedDictionary<int, CampaignGoal> _goals = new();
    protected override string Evaluate(Simulation sim)
    {
        _goals = new CampaignTargets(new(sim, Player)).Evaluate(Areas);
        return CanonicalJson.Digest(_goals);
    }
    protected override void Commit(Simulation sim) => AiIntent.Write(new(sim, Player)).Campaigns = _goals;
}

/// <summary>Assign or continue settler destinations in stable unit order.</summary>
public sealed class PrepareSettlerMissionsCommand : AiPlanningCommand
{
    private SortedDictionary<int, UnitMission> _missions = new();
    protected override string Evaluate(Simulation sim)
    {
        var ctx = new AiContext(sim, Player);
        _missions = new SettlerPlanner(ctx, new(ctx)).EvaluateMissions();
        return CanonicalJson.Digest(_missions);
    }
    protected override void Commit(Simulation sim) => AiIntent.CommitMissions(new(sim, Player), _missions);
}

/// <summary>Assign or continue worker destinations in stable unit order.</summary>
public sealed class PrepareWorkerMissionsCommand : AiPlanningCommand
{
    private SortedDictionary<int, UnitMission> _missions = new();
    protected override string Evaluate(Simulation sim)
    {
        _missions = new WorkerPlanner(new(sim, Player)).EvaluateMissions();
        return CanonicalJson.Digest(_missions);
    }
    protected override void Commit(Simulation sim) => AiIntent.CommitMissions(new(sim, Player), _missions);
}

/// <summary>Remove intent after a task completes or movement fails.</summary>
public sealed class ForgetUnitMissionCommand : GameCommand
{
    public int Unit { get; init; }
    public override string? Validate(Simulation sim) => IntentCommandRules.Validate(sim, Player);
    public override void Apply(Simulation sim)
    {
        var ctx = new AiContext(sim, Player);
        AiIntent.Read(ctx).Missions.Remove(Unit);
        AiIntent.RemoveIfEmpty(ctx);
    }
}

/// <summary>Remove intent for expired, dead, captured or role-changed units, including an eliminated empire.</summary>
public sealed class PruneAiIntentCommand : GameCommand
{
    public override string? Validate(Simulation sim) => IntentCommandRules.Validate(sim, Player);
    public override void Apply(Simulation sim)
    {
        var ctx = new AiContext(sim, Player);
        AiIntent.Prune(ctx);
        AiIntent.RemoveIfEmpty(ctx);
    }
}
