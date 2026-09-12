using Domain.Shared;

namespace Domain.Cooking;

/// <summary>Failures from the cooking side of a recipe.</summary>
public static class CookingErrors
{
    /// <summary>A note is blank or too long.</summary>
    public static readonly Error InvalidNote = new(
        "cooking.invalid_note",
        "A note needs some text, and not too much of it.",
        ErrorType.Validation);

    /// <summary>The recorded servings are not a plausible amount.</summary>
    public static readonly Error InvalidServings = new(
        "cooking.invalid_servings",
        "That is not a plausible number of servings.",
        ErrorType.Validation);

    /// <summary>No such cook-log entry, or none belonging to the caller.</summary>
    public static readonly Error EntryNotFound = new(
        "cooking.entry_not_found",
        "That entry is not in your cooking history.",
        ErrorType.NotFound);

    /// <summary>There is no cooking session to resume.</summary>
    public static readonly Error SessionNotFound = new(
        "cooking.session_not_found",
        "You are not cooking anything at the moment.",
        ErrorType.NotFound);

    /// <summary>The session has already been finished or given up on.</summary>
    public static readonly Error SessionFinished = new(
        "cooking.session_finished",
        "That cooking session has already ended.",
        ErrorType.Conflict);

    /// <summary>The step index is not a step.</summary>
    public static readonly Error StepOutOfRange = new(
        "cooking.step_out_of_range",
        "That step is not part of this recipe.",
        ErrorType.Validation);

    /// <summary>The note refers to a step that is not in this recipe.</summary>
    public static readonly Error UnknownStep = new(
        "cooking.unknown_step",
        "That step is not part of this recipe.",
        ErrorType.Validation);
}
