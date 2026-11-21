using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Ai;

/// <summary>How each of a team's current wars is going, as the computer player keeps score: the production cost of
/// enemy units destroyed plus cities taken, less the same suffered. A war's score starts at zero when it is declared
/// and is forgotten at peace. Teams with no war save nothing.</summary>
public sealed class AiTeamState
{
    /// <summary>Score against each enemy team, by team id.</summary>
    public SortedDictionary<int, int> WarScore { get; set; } = new();
}

/// <summary>Keeps <see cref="AiTeamState.WarScore"/> from combat, capture, war and peace events. Every team is
/// scored, human or not, so a computer player judges a war against a human the same way as against another AI.</summary>
internal static class WarRecord
{
    internal const int CityCaptureScore = 60, CityPopulationScore = 10;

    /// <summary>The score of <paramref name="team"/>'s war against <paramref name="enemy"/>; zero at peace.</summary>
    internal static int Score(Simulation sim, int team, int enemy) =>
        sim.State.TeamById(team).Extensions.Get<AiTeamState>(sim.Content)?.WarScore.GetValueOrDefault(enemy) ?? 0;

    /// <summary>A destroyed unit scores its production cost for the winner's team and against the loser's.</summary>
    internal static void Combat(Simulation sim, CombatResolved combat)
    {
        if (combat.Report is not { LoserDied: true } report || combat.LoserOwner < 0) return;
        int loser = combat.LoserOwner;
        bool attackerLost = report.AttackerOwner == loser;
        int winner = attackerLost ? report.DefenderOwner : report.AttackerOwner;
        string type = attackerLost ? report.AttackerType : report.DefenderType;
        if (winner < 0 || !sim.Content.TryGet<UnitDef>(type, out var def)) return;
        Add(sim, winner, loser, def.Cost);
    }

    /// <summary>A captured city scores a fixed amount plus its population after capture.</summary>
    internal static void Capture(Simulation sim, CityCaptured capture)
    {
        int population = sim.State.FindCity(capture.City)?.Population ?? 0;
        Add(sim, capture.Player, capture.PreviousOwner, CityCaptureScore + CityPopulationScore * population);
    }

    internal static void Declared(Simulation sim, WarDeclared war) => Forget(sim, war.AggressorTeam, war.DefenderTeam);
    internal static void Peace(Simulation sim, PeaceSigned peace) => Forget(sim, peace.FirstTeam, peace.SecondTeam);

    private static void Add(Simulation sim, int winner, int loser, int points)
    {
        if (winner == loser || points == 0 || !sim.State.Players.Any(p => p.Id == winner) || !sim.State.Players.Any(p => p.Id == loser)) return;
        if (!PlayerRules.IsNation(sim, winner) || !PlayerRules.IsNation(sim, loser)) return;
        int a = sim.TeamOf(winner).Id, b = sim.TeamOf(loser).Id;
        if (a == b || !sim.State.TeamById(a).Wars.Contains(b)) return;
        var mine = Edit(sim, a).WarScore; mine[b] = checked(mine.GetValueOrDefault(b) + points);
        var theirs = Edit(sim, b).WarScore; theirs[a] = checked(theirs.GetValueOrDefault(a) - points);
    }

    private static void Forget(Simulation sim, int teamA, int teamB)
    {
        Remove(sim, teamA, teamB);
        Remove(sim, teamB, teamA);
    }

    private static void Remove(Simulation sim, int team, int enemy)
    {
        var extensions = sim.State.TeamById(team).Extensions;
        if (extensions.Get<AiTeamState>(sim.Content) is not { } state || !state.WarScore.Remove(enemy)) return;
        if (state.WarScore.Count == 0) extensions.Remove<AiTeamState>(sim.Content);
    }

    private static AiTeamState Edit(Simulation sim, int team)
    {
        var extensions = sim.State.TeamById(team).Extensions;
        if (extensions.Get<AiTeamState>(sim.Content) is { } state) return state;
        state = new();
        extensions.Set(sim.Content, state);
        return state;
    }
}
