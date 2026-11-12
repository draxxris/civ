using Ariel.Content.Schema;

namespace Ariel.Game;

/// <summary>Turn-count scaling for one game speed. Durations follow the speed's pace, never its research rate:
/// research percent prices technologies and beakers, not how long a temporary effect lasts.</summary>
public static class GamePace
{
    /// <summary>Pace percentage for this game speed, falling back to the research rate when content omits it.</summary>
    public static int Percent(Simulation sim)
    {
        var speed = sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed);
        return speed.ScalingPercents.TryGetValue("default", out int percent) && percent > 0
            ? percent : Math.Max(1, speed.ResearchPercent);
    }

    /// <summary>Scale a nominal turn count by the pace, never below one turn.</summary>
    public static int Scale(Simulation sim, int turns) => Math.Max(1, turns * Percent(sim) / 100);
}
