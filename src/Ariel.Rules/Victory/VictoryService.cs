using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Victory;

/// <summary>Players' scores: population, land, technologies and world wonders, each weighed by the first time victory
/// by id (<see cref="VictoryDef.ScorePerPopulation"/> and the rest), enabled or not. Without one every score is 0.</summary>
internal sealed class PlayerScores : IPlayerScores
{
    /// <summary>The definition whose weights make the score.</summary>
    internal static VictoryDef? Weights(ContentDb content) => content.Get<VictoryDef>().All
        .Where(v => v.Rule == TimeVictory.ConditionId).OrderBy(v => v.Id, StringComparer.Ordinal).FirstOrDefault();

    public int Score(Simulation sim, int player) => Breakdown(sim, player).Total;

    public PlayerScore Breakdown(Simulation sim, int playerId)
    {
        var weights = Weights(sim.Content);
        int population = 0, wonders = 0, land = 0;
        foreach (var city in sim.State.Cities)
        {
            if (city.Owner != playerId) continue;
            population += city.Population;
            foreach (var building in city.Buildings)
                if (sim.Content.Get<BuildingDef>(building).Limits is { NoLimit: false, World: >= 0 }) wonders++;
        }
        foreach (var plot in sim.State.Plots) if (plot.Owner == playerId) land++;
        int techs = sim.State.TeamOf(playerId).Techs.Count;
        int total = population * (weights?.ScorePerPopulation ?? 0) + land * (weights?.ScorePerLand ?? 0)
            + techs * (weights?.ScorePerTech ?? 0) + wonders * (weights?.ScorePerWonder ?? 0);
        return new(population, land, techs, wonders, total);
    }
}

/// <summary>The standings of one victory check: each civilization's score worked out once, and the teams ranked.</summary>
internal sealed class VictoryStandings : IVictoryStandings
{
    private readonly Dictionary<int, int[]> _members = [];
    private readonly Dictionary<int, int> _scores = [];
    private readonly HashSet<int> _alive = [];

    internal VictoryStandings(Simulation sim, IPlayerScores scores)
    {
        var weights = PlayerScores.Weights(sim.Content);
        var teamScores = new Dictionary<int, int>();
        foreach (var team in sim.State.Players.Where(p => PlayerRules.IsNation(sim, p.Id)).OrderBy(p => p.Id).GroupBy(p => p.TeamId))
        {
            var members = team.Select(p => p.Id).ToArray();
            _members[team.Key] = members;
            int personal = 0, sharedTech = 0;
            foreach (int player in members)
            {
                var score = scores.Breakdown(sim, player);
                _scores[player] = score.Total;
                if (PlayerRules.IsAlive(sim, player)) _alive.Add(player);
                personal += score.Population * (weights?.ScorePerPopulation ?? 0) + score.Land * (weights?.ScorePerLand ?? 0)
                    + score.Wonders * (weights?.ScorePerWonder ?? 0);
                // Technologies are shared by the team, so they must not be multiplied by the member count.
                if (player == members[0]) sharedTech = score.Techs * (weights?.ScorePerTech ?? 0);
            }
            teamScores[team.Key] = personal + sharedTech;
        }
        Teams = _members.Keys.OrderByDescending(t => teamScores[t]).ThenBy(t => t).ToArray();
    }

    public IReadOnlyList<int> Teams { get; }
    public IReadOnlyList<int> Members(int team) => _members.TryGetValue(team, out var members) ? members : [];
    public bool IsAlive(int player) => _alive.Contains(player);

    public int Winner(int team)
    {
        var members = _members[team];
        bool anyAlive = members.Any(_alive.Contains);
        return members.Where(p => !anyAlive || _alive.Contains(p))
            .OrderByDescending(p => _scores[p]).ThenBy(p => p).First();
    }
}

/// <summary>The victory state and check: every enabled victory in priority order asks the condition it names.</summary>
internal sealed class VictoryService(IPlayerScores scores) : IVictoryService
{
    private static VictoryGameState Read(Simulation sim) => sim.State.Extensions.Get<VictoryGameState>(sim.Content) ?? new();

    /// <summary>The enabled victories, lowest priority first; ties by id.</summary>
    private static IEnumerable<VictoryDef> Victories(Simulation sim) => sim.Content.Get<VictoryDef>().All
        .Where(v => v.Enabled).OrderBy(v => v.Priority).ThenBy(v => v.Id, StringComparer.Ordinal);

    public VictoryStatus Status(Simulation sim)
    {
        var state = Read(sim);
        var time = Victories(sim).FirstOrDefault(v => v.Rule == TimeVictory.ConditionId);
        return new(state.IsGameOver, state.WinningTeamId, state.WinningPlayerId, state.VictoryId, state.TurnWon,
            time is null ? 0 : TimeVictory.MaxTurns(sim, time));
    }

    public bool IsEnabled(Simulation sim, string identity) => Victories(sim).Any(v => v.Identity == identity);
    public bool IsDecidedBy(Simulation sim, string condition) => Victories(sim).Any(v => v.Rule == condition);

    public VictoryStatus Evaluate(Simulation sim, bool turnEnd)
    {
        if (Read(sim).IsGameOver) return Status(sim);
        var conditions = sim.Rules().VictoryConditions;
        VictoryStandings? standings = null;
        foreach (var def in Victories(sim))
        {
            var condition = Condition(conditions, def.Rule);
            standings ??= new VictoryStandings(sim, scores);
            if (condition.Winner(sim, def, standings, turnEnd) is { } player) { Record(sim, def, player); break; }
        }
        return Status(sim);
    }

    public VictoryStatus Award(Simulation sim, string condition, int player)
    {
        if (!Read(sim).IsGameOver && Victories(sim).FirstOrDefault(v => v.Rule == condition) is { } def)
            Record(sim, def, player);
        return Status(sim);
    }

    private static IVictoryCondition Condition(IReadOnlyList<IVictoryCondition> conditions, string id)
    {
        foreach (var condition in conditions) if (condition.Id == id) return condition;
        throw new InvalidOperationException($"No victory condition '{id}' is registered.");
    }

    private static void Record(Simulation sim, VictoryDef def, int player)
    {
        int team = sim.State.PlayerById(player).TeamId;
        var state = sim.State.Extensions.Get<VictoryGameState>(sim.Content);
        if (state is null) sim.State.Extensions.Set(sim.Content, state = new VictoryGameState());
        if (!state.Record(team, player, def.Id, sim.State.Turn)) return;
        sim.Events.Publish(new VictoryAchieved(team, player, def.Id, sim.State.Turn));
    }
}
