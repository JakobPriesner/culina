namespace Domain.Planning;

/// <summary>What a week is: a calendar rule, so the week view, snapping endpoint and planners agree on which Monday a Thursday belongs to.</summary>
public static class PlanningWeek
{
    /// <summary>The earliest day that can be planned: far enough from the calendar's start that its Monday exists.</summary>
    public static readonly DateOnly Earliest = DateOnly.MinValue.AddDays(7);

    /// <summary>The latest day that can be planned: a week is read as seven days after its Monday, which must still be on the calendar.</summary>
    public static readonly DateOnly Latest = DateOnly.MaxValue.AddDays(-14);

    /// <summary>Whether a week can be built around the day without running off either end of the calendar.</summary>
    /// <param name="day">Any day.</param>
    public static bool IsPlannable(DateOnly day) => day >= Earliest && day <= Latest;

    /// <summary>The Monday of the week a day belongs to. Monday because a Sunday start splits the weekend shop in half.</summary>
    /// <param name="day">Any day of it.</param>
    public static DateOnly StartOfWeekContaining(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
