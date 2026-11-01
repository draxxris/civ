using System.Text.Json.Serialization;
using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Saved per-team war bookkeeping: the turn each current war started and the turn each peace treaty was signed.</summary>
public sealed class AiTeamState
{
    [JsonInclude] public SortedDictionary<int, int> WarStarted { get; set; } = new();
    [JsonInclude] public SortedDictionary<int, int> PeaceStarted { get; set; } = new();
    /// <summary>Success in each current war: unit value destroyed plus cities taken, less the same suffered.</summary>
    [JsonInclude] public SortedDictionary<int, int> WarScore { get; set; } = new();
}

/// <summary>Shared checks for the minimal team war-state commands.</summary>
internal static class WarRules
{
    internal const int MinimumWarTurns = 12;
    internal const int PeaceCooldownTurns = 10;
    internal static bool IsBarbarian(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.Get<MilitaryPlayerState>(sim.Content)?.Barbarian == true;
    internal static AiTeamState Read(Simulation sim, int team) =>
        sim.State.TeamById(team).Extensions.Get<AiTeamState>(sim.Content) ?? new();
    internal static AiTeamState State(Simulation sim, int team)
    {
        var t = sim.State.TeamById(team);
        var state = t.Extensions.Get<AiTeamState>(sim.Content);
        if (state is not null) return state;
        state = new(); t.Extensions.Set(sim.Content, state); return state;
    }
    internal static string? Common(Simulation sim, int player, int target)
    {
        if (!sim.State.Players.Any(p => p.Id == player) || !sim.State.Players.Any(p => p.Id == target)) return "unknown player";
        if (sim.State.PlayerById(player).TeamId == sim.State.PlayerById(target).TeamId) return "same team";
        return IsBarbarian(sim, target) || IsBarbarian(sim, player) ? "barbarians are always at war" : null;
    }
    internal const int CityCaptureScore = 60, CityPopulationScore = 10;

    /// <summary>A destroyed unit scores its production cost for the winner's team and against the loser's.</summary>
    internal static void ScoreCombat(Simulation sim, CombatResolved combat)
    {
        if (combat.Report is not { LoserDied: true } report || combat.LoserOwner < 0) return;
        int loser = combat.LoserOwner;
        int winner = report.AttackerOwner == loser ? report.DefenderOwner : report.AttackerOwner;
        string type = report.AttackerOwner == loser ? report.AttackerType : report.DefenderType;
        if (winner < 0 || !sim.Content.TryGet<Ariel.Content.Schema.UnitDef>(type, out var def)) return;
        Score(sim, winner, loser, def.Cost);
    }

    internal static void ScoreCapture(Simulation sim, CityCaptured capture)
    {
        int population = sim.State.FindCity(capture.City)?.Population ?? 0;
        Score(sim, capture.Player, capture.PreviousOwner, CityCaptureScore + CityPopulationScore * population);
    }

    private static void Score(Simulation sim, int winner, int loser, int points)
    {
        if (winner == loser || !sim.State.Players.Any(p => p.Id == winner) || !sim.State.Players.Any(p => p.Id == loser)) return;
        int a = sim.State.PlayerById(winner).TeamId, b = sim.State.PlayerById(loser).TeamId;
        if (a == b || !sim.State.TeamById(a).Wars.Contains(b) || IsBarbarian(sim, winner) || IsBarbarian(sim, loser)) return;
        var mine = State(sim, a).WarScore; mine[b] = mine.GetValueOrDefault(b) + points;
        var theirs = State(sim, b).WarScore; theirs[a] = theirs.GetValueOrDefault(a) - points;
    }

    internal static string? PeaceCooldown(Simulation sim, int team, int other) =>
        Read(sim, team).PeaceStarted.TryGetValue(other, out int signed) && sim.State.Turn - signed < PeaceCooldownTurns
            ? "peace treaty is too recent" : null;
}

/// <summary>Declare war on another player's team.</summary>
public sealed class DeclareWarCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => WarRules.Common(sim, Player, Target)
        ?? (sim.TeamOf(Player).Wars.Contains(sim.State.PlayerById(Target).TeamId) ? "already at war" : null)
        ?? WarRules.PeaceCooldown(sim, sim.State.PlayerById(Player).TeamId, sim.State.PlayerById(Target).TeamId)
        ?? sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault()?.ValidateWar(sim, Player, Target);
    public override void Apply(Simulation sim)
    {
var diplomacy = sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        if (diplomacy is not null) { diplomacy.DeclareWar(sim, Player, Target); return; }
        int mine = sim.State.PlayerById(Player).TeamId, theirs = sim.State.PlayerById(Target).TeamId;
        sim.State.SetWar(mine, theirs, true);
        WarRules.State(sim, mine).WarStarted[theirs] = sim.State.Turn;
        WarRules.State(sim, theirs).WarStarted[mine] = sim.State.Turn;
        WarRules.State(sim, mine).WarScore.Remove(theirs);
        WarRules.State(sim, theirs).WarScore.Remove(mine);
    }
}

/// <summary>End a war after it has lasted a minimum number of turns. The treaty blocks new war declarations for a cooldown.</summary>
public sealed class MakePeaceCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim)
    {
        var common = WarRules.Common(sim, Player, Target);
        if (common is not null) return common;
        int mine = sim.State.PlayerById(Player).TeamId, theirs = sim.State.PlayerById(Target).TeamId;
        if (!sim.State.TeamById(mine).Wars.Contains(theirs)) return "not at war";
        return sim.State.Turn - WarRules.Read(sim, mine).WarStarted.GetValueOrDefault(theirs) < WarRules.MinimumWarTurns
            ? "war is too recent" : sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault()?.ValidatePeace(sim, Player, Target);
    }
    public override void Apply(Simulation sim)
    {
var diplomacy = sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        if (diplomacy is not null) { diplomacy.MakePeace(sim, Player, Target); return; }
        int mine = sim.State.PlayerById(Player).TeamId, theirs = sim.State.PlayerById(Target).TeamId;
        sim.State.SetWar(mine, theirs, false);
        WarRules.State(sim, mine).WarStarted.Remove(theirs);
        WarRules.State(sim, theirs).WarStarted.Remove(mine);
        WarRules.State(sim, mine).WarScore.Remove(theirs);
        WarRules.State(sim, theirs).WarScore.Remove(mine);
        WarRules.State(sim, mine).PeaceStarted[theirs] = sim.State.Turn;
        WarRules.State(sim, theirs).PeaceStarted[mine] = sim.State.Turn;
    }
}
