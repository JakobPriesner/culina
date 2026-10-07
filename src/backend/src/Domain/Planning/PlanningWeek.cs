namespace Domain.Planning;

/// <summary>What a week is: a calendar rule, so the week view, snapping endpoint and planners agree on which Monday a Thursday belongs to.</summary>
public static class PlanningWeek
{
    /// <summary>The Monday of the week a day belongs to. Monday because a Sunday start splits the weekend shop in half.</summary>
    /// <param name="day">Any day of it.</param>
    public static DateOnly StartOfWeekContaining(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
