namespace Civ.Game;

public enum CalendarSeason { Winter, Spring, Summer, Autumn }
public enum CalendarPrecision { Year, Season, Month, Day }

/// <summary>Numeric years include zero. Months and days are one-based; labels do not change the date.</summary>
public sealed record CalendarDate(long Year, int Month, int Day, CalendarSeason Season,
    CalendarPrecision Precision, string FormattedDate);

/// <summary>Pure ruleset calendar queries. Missing calendar content returns null.</summary>
public interface IGameCalendar
{
    /// <summary>Elapsed turns are zero-based. Omission uses the simulation's one-based turn minus one.</summary>
    CalendarDate? GetDate(Simulation sim, int? elapsedTurns = null);
}
