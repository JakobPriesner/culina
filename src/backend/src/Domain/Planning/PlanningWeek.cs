namespace Domain.Planning;

/// <summary>What a week is.</summary>
/// <remarks>
/// A rule about the calendar rather than about the application: the week view,
/// the endpoint that snaps a date to it and anything that plans ahead all have
/// to agree on which Monday a Thursday belongs to.
/// </remarks>
public static class PlanningWeek
{
    /// <summary>
    /// The Monday of the week a day belongs to.
    /// </summary>
    /// <param name="day">Any day of it.</param>
    /// <remarks>
    /// Monday, not Sunday. Culina is written for people who shop at the weekend
    /// for the week that follows, and a week beginning on Sunday splits that
    /// weekend in half.
    /// </remarks>
    public static DateOnly StartOfWeekContaining(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
