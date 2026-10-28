using System.Numerics;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using TechDef = Ariel.Content.Schema.TechDef;

namespace Ariel.Mods.Research;

internal static class ResearchTimeline
{
    internal static EraDef[] Eras(Simulation sim) => sim.Content.Get<EraDef>().All
        .OrderBy(e => e.StartPercent).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();

    internal static bool Alive(Simulation sim, int player) =>
        sim.State.Cities.Any(c => c.Owner == player) || sim.State.Units.Any(u => u.Owner == player);

    internal static int Leader(Simulation sim, Team team) => team.Members.Where(p => Alive(sim, p))
        .DefaultIfEmpty(team.Members.Min).Min();

    internal static long Percent(Simulation sim, Team team, List<LedgerLine> lines)
    {
        const string option = "foundation:gameoption_realistic_timescale";
        bool enabled = sim.Settings.GameOptions.TryGetValue(option, out bool value) ? value
            : SetupPolicies.GameOptions(sim.Content).SingleOrDefault(o => o.Id == option)?.Default ?? false;
        if (!enabled) return 100;
        var eras = Eras(sim);
        int start = Array.FindIndex(eras, e => e.Id == sim.Settings.StartingEra);
        if (start < 0 || team.Members.Count == 0) return Neutral("missing_era_or_leader", lines);
        return ScheduledPercent(sim, team, eras, start, lines);
    }

    private static long ScheduledPercent(Simulation sim, Team team, EraDef[] eras, int start, List<LedgerLine> lines)
    {
        int eraCount = Math.Max(1, EndEra(sim, eras, start) - start);
        int end = EndEra(sim, eras, start);
        var technologies = sim.Content.Get<TechDef>().All.Select(t =>
            (Tech: t, Era: Array.FindIndex(eras, e => e.Id == t.Era))).ToArray();
        // A custom technology without an era cannot define timeline progress.
        if (technologies.Any(t => t.Era < 0)) return Neutral("missing_technology_era", lines);
        int perEra = technologies.Count(t => t.Era >= start && t.Era <= end) / eraCount;
        if (perEra == 0) return Neutral("sparse_technologies", lines);
        var speed = sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed);
        if (speed.TurnIncrements.IsEmpty || speed.TurnIncrements.Any(s => s.Turns <= 0))
            return Neutral("missing_schedule", lines);
        long turns = speed.TurnIncrements.Sum(s => (long)s.Turns);
        int remaining = 100 - eras[start].StartPercent;
        if (remaining is <= 0 or > 100) return Neutral("invalid_duration", lines);
        return ProgressPercent(sim, team, eras, start, eraCount, technologies, perEra, (BigInteger)turns * remaining, lines);
    }

    private static long ProgressPercent(Simulation sim, Team team, EraDef[] eras, int start, int eraCount,
        (TechDef Tech, int Era)[] technologies, int perEra, BigInteger duration, List<LedgerLine> lines)
    {
        string current = sim.State.PlayerById(Leader(sim, team)).Extensions.TryGet<CityPlayerState>(sim.Content)?.CurrentEra
            ?? sim.Settings.StartingEra;
        int leaderEra = Array.FindIndex(eras, e => e.Id == current);
        if (leaderEra < 0) return Neutral("missing_current_era", lines);
        int adjustment = technologies.Count(t => team.Techs.Contains(t.Tech.Id) && t.Era >= leaderEra)
            - technologies.Count(t => !team.Techs.Contains(t.Tech.Id) && t.Era < leaderEra);
        BigInteger player = (BigInteger)leaderEra * perEra + adjustment;

        BigInteger expected = (BigInteger)Math.Max(0, (long)sim.State.Turn - 1) * eraCount * 100 + start * duration;
        // Preserve integer techCount / eraCount, but keep the reciprocal and elapsed-turn ratio fractional.
        long percent = ResearchPower.TimelinePercent(player * duration - expected * perEra, perEra * duration);
        lines.Add(new("research:timeline", "research:expected_era_millionths", checked((long)(expected * 1_000_000 / duration))));
        lines.Add(new("research:timeline", "research:player_era_millionths", checked((long)(player * 1_000_000 / perEra))));
        return percent;
    }

    private static long Neutral(string reason, List<LedgerLine> lines)
    {
        lines.Add(new("research:timeline", "research:timeline_neutral:" + reason, 1));
        return 100;
    }

    private static int EndEra(Simulation sim, EraDef[] eras, int start)
    {
        // Native terminal era is penultimate + 1, even when the terminal era itself is disabled.
        string[] cutoffs = ["classical", "medieval", "renaissance", "industrial", "modern", "transhuman"];
        foreach (string cutoff in cutoffs)
        {
            string option = cutoff == "transhuman" ? "foundation:gameoption_no_future" : "foundation:gameoption_no_" + cutoff + "_era";
            int index = Array.FindIndex(eras, e => e.Id == "foundation:era_" + cutoff);
            if (index >= 0 && sim.IsOptionEnabled(option)) return Math.Min(eras.Length - 1, Math.Max(start + 1, index));
        }
        return eras.Length - 1;
    }
}
