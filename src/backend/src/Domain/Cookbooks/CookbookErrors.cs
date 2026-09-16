using Domain.Shared;

namespace Domain.Cookbooks;

/// <summary>Failures from cookbooks.</summary>
public static class CookbookErrors
{
    /// <summary>No such cookbook, or none this person is allowed to see.</summary>
    /// <remarks>
    /// One error for both, so a cookbook belonging to another household is
    /// indistinguishable from one that never existed. Existence is not leaked
    /// through a status code.
    /// </remarks>
    /// <param name="cookbookId">The one that was asked for.</param>
    public static Error NotFound(Guid cookbookId) => new(
        "cookbooks.not_found",
        $"No cookbook with id '{cookbookId}' exists.",
        ErrorType.NotFound);

    /// <summary>A cookbook needs something to call it.</summary>
    public static readonly Error InvalidName = new(
        "cookbooks.invalid_name",
        "A cookbook needs a name, and it must be shorter than that.",
        ErrorType.Validation);

    /// <summary>A smart cookbook that asks for nothing would be every recipe.</summary>
    public static readonly Error RulesRequired = new(
        "cookbooks.rules_required",
        "A cookbook that fills itself needs at least one rule to fill itself by.",
        ErrorType.Validation);

    /// <summary>A rule nobody could have meant.</summary>
    public static readonly Error InvalidRule = new(
        "cookbooks.invalid_rule",
        "That rule is not one a recipe could match.",
        ErrorType.Validation);

    /// <summary>More conditions than anybody could hold in their head.</summary>
    public static readonly Error TooManyRules = new(
        "cookbooks.too_many_rules",
        "That is more rules than one cookbook can state.",
        ErrorType.Validation);

    /// <summary>
    /// Somebody tried to put a recipe on a shelf that decides for itself.
    /// </summary>
    /// <remarks>
    /// A conflict rather than a validation failure: the request is well formed
    /// and would be fine against any other cookbook. It is this one's nature
    /// that refuses it.
    /// </remarks>
    public static readonly Error RulesDecideMembership = new(
        "cookbooks.rules_decide_membership",
        "This cookbook fills itself, so recipes cannot be put on it by hand.",
        ErrorType.Conflict);

    /// <summary>The description is longer than a shelf label should be.</summary>
    public static readonly Error InvalidDescription = new(
        "cookbooks.invalid_description",
        "That description is too long for a cookbook.",
        ErrorType.Validation);
}
