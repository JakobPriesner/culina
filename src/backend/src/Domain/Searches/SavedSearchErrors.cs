using Domain.Shared;

namespace Domain.Searches;

/// <summary>Failures from saved searches.</summary>
public static class SavedSearchErrors
{
    /// <summary>No such saved search, or none this person is allowed to see.</summary>
    /// <remarks>
    /// One error for both, so a search belonging to another household is
    /// indistinguishable from one that never existed.
    /// </remarks>
    /// <param name="searchId">The one that was asked for.</param>
    public static Error NotFound(Guid searchId) => new(
        "searches.not_found",
        $"No saved search with id '{searchId}' exists.",
        ErrorType.NotFound);

    /// <summary>A saved search needs something to call it.</summary>
    public static readonly Error InvalidName = new(
        "searches.invalid_name",
        "A saved search needs a name, and it must be shorter than that.",
        ErrorType.Validation);

    /// <summary>
    /// Two searches in one kitchen cannot share a name.
    /// </summary>
    /// <remarks>
    /// A conflict rather than a validation failure: the request is well formed
    /// and would be fine in any other household. It is what is already there
    /// that refuses it.
    /// </remarks>
    public static readonly Error NameTaken = new(
        "searches.name_taken",
        "This household already has a saved search with that name.",
        ErrorType.Conflict);

    /// <summary>A search asking for nothing is the library.</summary>
    public static readonly Error CriteriaRequired = new(
        "searches.criteria_required",
        "A saved search needs at least one of words, tags, a time limit or an order.",
        ErrorType.Validation);

    /// <summary>A filter nobody could have meant.</summary>
    public static readonly Error InvalidCriteria = new(
        "searches.invalid_criteria",
        "That is not a filter the library can apply.",
        ErrorType.Validation);
}
