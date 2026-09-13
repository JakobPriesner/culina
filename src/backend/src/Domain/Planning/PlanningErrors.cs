using Domain.Shared;

namespace Domain.Planning;

/// <summary>What can go wrong with a plan.</summary>
public static class PlanningErrors
{
    /// <summary>A planned entry asks for an impossible number of servings.</summary>
    public static readonly Error InvalidServings = new(
        "planning.invalid_servings",
        "A planned meal is for between one and a thousand.",
        ErrorType.Validation);

    /// <summary>The week asked for is not a week.</summary>
    public static readonly Error InvalidRange = new(
        "planning.invalid_range",
        "A plan is read a week at a time.",
        ErrorType.Validation);

    /// <summary>No such entry, or not this household's.</summary>
    public static readonly Error EntryNotFound = new(
        "planning.entry_not_found",
        "That planned meal is not there.",
        ErrorType.NotFound);
}
