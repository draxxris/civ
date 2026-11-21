using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Victory;

/// <summary>Conquest: every civilization still in the game is on one team, and there were at least two teams.</summary>
internal sealed class ConquestVictory : IVictoryCondition
{
    public string Id => "victory:conquest";

    public int? Winner(Simulation sim, VictoryDef victory, IVictoryStandings standings, bool turnEnd)
    {
        if (standings.Teams.Count < 2) return null;
        int? survivor = null;
        foreach (int team in standings.Teams)
        {
            if (!standings.Members(team).Any(standings.IsAlive)) continue;
            if (survivor is not null) return null;
            survivor = team;
        }
        return survivor is { } winner ? standings.Winner(winner) : null;
    }
}

/// <summary>Domination: one team holds <see cref="VictoryDef.PopulationPercent"/> of all city population and
/// <see cref="VictoryDef.LandPercent"/> of all land plots. Teams are asked highest score first.</summary>
internal sealed class DominationVictory : IVictoryCondition
{
    public string Id => "victory:domination";

    public int? Winner(Simulation sim, VictoryDef victory, IVictoryStandings standings, bool turnEnd)
    {
        if (standings.Teams.Count < 2) return null;
        var teamOf = new Dictionary<int, int>();
        foreach (int team in standings.Teams) foreach (int member in standings.Members(team)) teamOf[member] = team;
        long totalPopulation = 0, totalLand = 0;
        var population = new Dictionary<int, long>();
        var land = new Dictionary<int, long>();
        foreach (var city in sim.State.Cities)
        {
            totalPopulation += city.Population;
            if (teamOf.TryGetValue(city.Owner, out int team)) population[team] = population.GetValueOrDefault(team) + city.Population;
        }
        foreach (var plot in sim.State.Plots)
        {
            if (sim.Content.TryGet<TerrainDef>(plot.Terrain, out var terrain) && terrain.IsWater) continue;
            totalLand++;
            if (plot.Owner is { } owner && teamOf.TryGetValue(owner, out int team)) land[team] = land.GetValueOrDefault(team) + 1;
        }
        if (totalPopulation == 0 || totalLand == 0) return null;
        foreach (int team in standings.Teams)
            if (population.GetValueOrDefault(team) * 100 >= totalPopulation * victory.PopulationPercent
                && land.GetValueOrDefault(team) * 100 >= totalLand * victory.LandPercent)
                return standings.Winner(team);
        return null;
    }
}

/// <summary>Cultural: one team has <see cref="VictoryDef.CultureCities"/> cities at <see cref="VictoryDef.CultureLevel"/>
/// or above, counting the level a city's culture is eligible for. Teams are asked highest score first.</summary>
internal sealed class CulturalVictory : IVictoryCondition
{
    public string Id => "victory:cultural";

    public int? Winner(Simulation sim, VictoryDef victory, IVictoryStandings standings, bool turnEnd)
    {
        if (victory.CultureLevel is null || victory.CultureCities <= 0) return null;
        string speed = sim.Settings.GameSpeed;
        long threshold = sim.Content.Get<CultureLevelDef>(victory.CultureLevel).SpeedThresholds[speed];
        var culture = sim.Rules().CityCulture;
        foreach (int team in standings.Teams)
        {
            var members = standings.Members(team);
            int count = 0;
            foreach (var city in sim.State.Cities)
                if (members.Contains(city.Owner) && culture.Level(sim, city.Id).SpeedThresholds[speed] >= threshold) count++;
            if (count >= victory.CultureCities) return standings.Winner(team);
        }
        return null;
    }
}

/// <summary>Time: at the end of the turn that reaches the turn limit, the living team with the highest score wins.</summary>
internal sealed class TimeVictory : IVictoryCondition
{
    internal const string ConditionId = "victory:time";
    public string Id => ConditionId;

    /// <summary>The turn limit, scaled by the game speed's victory delay.</summary>
    internal static int MaxTurns(Simulation sim, VictoryDef victory) =>
        (int)((long)victory.MaxTurns * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).VictoryDelayPercent / 100);

    public int? Winner(Simulation sim, VictoryDef victory, IVictoryStandings standings, bool turnEnd)
    {
        if (!turnEnd || victory.MaxTurns <= 0 || sim.State.Turn < MaxTurns(sim, victory)) return null;
        // An eliminated teammate can hold land and outscore the living, so only a team with a living member may win,
        // and the winner is its best living member.
        foreach (int team in standings.Teams)
            if (standings.Members(team).Any(standings.IsAlive)) return standings.Winner(team);
        return null;
    }
}
