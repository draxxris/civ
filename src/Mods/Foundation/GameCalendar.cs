using System.Globalization;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Foundation;

/// <summary>Month-only CvDate schedules, derived without a mutable simulation clock.</summary>
internal sealed class GameCalendar : IGameCalendar
{
    private sealed record Step(long EndTurn, long EndMonth, int Months);

    public CalendarDate? GetDate(Simulation sim, int? elapsedTurns = null)
    {
        int elapsed = elapsedTurns ?? checked(sim.State.Turn - 1);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed);
        if (!sim.Content.TryGet<GameSpeedDef>(sim.Settings.GameSpeed, out var speed)) return null;
        int startYear = sim.Content.Get<RuleConstantsDef>().All.SingleOrDefault()?.Values.StartYear ?? 0;
        var steps = BuildSchedule(speed, startYear);
        if (steps.Length == 0) return null;
        int percent = sim.Content.TryGet<EraDef>(sim.Settings.StartingEra, out var era) ? era.StartPercent : 0;
        if (percent is < 0 or > 100) throw new InvalidDataException("Calendar starting-era percentage must be between 0 and 100");
        long offset = (long)((Int128)steps[^1].EndTurn * percent / 100);
        return DateAt(steps, checked(offset + elapsed), startYear);
    }

    internal void ValidateShipped(GameSpeedDef speed)
    {
        // Increments are positive, so the largest possible start year gives the strictest upper bound.
        // Validate without capturing raw constants; queries validate again against effective content.
        if (BuildSchedule(speed, int.MaxValue).Length == 0)
            throw new InvalidDataException($"Calendar schedule is empty: {speed.Id}");
    }

    private static Step[] BuildSchedule(GameSpeedDef speed, int startYear)
    {
        var steps = new List<Step>();
        long turn = 0, month = (long)startYear * 12 + 1; // CvDate starts in February, with zero-based month 1.
        int previousMonths = speed.TurnIncrements.IsEmpty ? 0 : speed.TurnIncrements[0].Months;
        try
        {
            foreach (var increment in speed.TurnIncrements)
            {
                if (increment.Turns <= 0 || increment.Months <= 0)
                    throw new InvalidDataException($"Calendar durations and month increments must be positive: {speed.Id}");
                // CvDate selects the previous increment at its inclusive date endpoint.
                month = checked(month + previousMonths + (increment.Turns - 1L) * increment.Months);
                turn = checked(turn + increment.Turns);
                steps.Add(new(turn, month, increment.Months));
                previousMonths = increment.Months;
            }
            // All supported elapsed-turn queries, including a 100-percent era start, must fit.
            _ = checked(turn + int.MaxValue);
            _ = checked(month + (long)int.MaxValue * previousMonths);
        }
        catch (OverflowException error)
        {
            throw new InvalidDataException($"Calendar schedule exceeds the supported integer range: {speed.Id}", error);
        }
        return steps.ToArray();
    }

    private static CalendarDate DateAt(Step[] steps, long turn, int startYear)
    {
        long previousTurn = 0, previousMonth = (long)startYear * 12 + 1;
        int previousMonths = steps[0].Months;
        foreach (var step in steps)
        {
            if (turn <= step.EndTurn)
            {
                long remaining = turn - previousTurn;
                long month = remaining == 0 ? previousMonth
                    : checked(previousMonth + previousMonths + (remaining - 1) * step.Months);
                return Describe(month, step.Months);
            }
            previousTurn = step.EndTurn;
            previousMonth = step.EndMonth;
            previousMonths = step.Months;
        }
        // CvDate's random-access tail uses an uninitialized local. Continue its last live increment instead.
        return Describe(checked(previousMonth + (turn - previousTurn) * previousMonths), previousMonths);
    }

    private static CalendarDate Describe(long totalMonths, int incrementMonths)
    {
        long year = Math.DivRem(totalMonths, 12, out long remainder);
        if (remainder < 0) { year--; remainder += 12; }
        int month = (int)remainder + 1;
        var season = month switch
        {
            <= 2 or 12 => CalendarSeason.Winter,
            <= 5 => CalendarSeason.Spring,
            <= 8 => CalendarSeason.Summer,
            _ => CalendarSeason.Autumn,
        };
        var precision = incrementMonths % 12 == 0 || incrementMonths >= 24 ? CalendarPrecision.Year
            : incrementMonths >= 3 ? CalendarPrecision.Season : CalendarPrecision.Month;
        string label = year < 0 ? (-year).ToString(CultureInfo.InvariantCulture) + " BCE"
            : year.ToString(CultureInfo.InvariantCulture) + " CE";
        string formatted = precision switch
        {
            CalendarPrecision.Season => season + ", " + label,
            CalendarPrecision.Month => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month) + ", " + label,
            _ => label,
        };
        return new(year, month, 1, season, precision, formatted);
    }
}
