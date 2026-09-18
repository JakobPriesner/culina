using Domain.Shared;

namespace Domain.Suggestions;

/// <summary>Every failure the suggestions module can return.</summary>
public static class SuggestionErrors
{
    /// <summary>More were asked for than any screen has room to show.</summary>
    public static readonly Error InvalidLimit = new(
        "suggestions.invalid_limit",
        "Ask for between 1 and 12 suggestions. More than that is a list, and the recipe search is the list.",
        ErrorType.Validation);

    /// <summary>A slot was named that is not one of the three the plan has.</summary>
    public static readonly Error UnknownSlot = new(
        "suggestions.unknown_slot",
        "A slot is 'breakfast', 'lunch' or 'dinner'.",
        ErrorType.Validation);

    /// <summary>A purpose was named that does not exist.</summary>
    public static readonly Error UnknownPurpose = new(
        "suggestions.unknown_purpose",
        "A purpose is 'decide' or 'like'. Use GET /recipes?sort=suggested to rank the whole collection.",
        ErrorType.Validation);

    /// <summary>"Like this one" was asked without saying which one.</summary>
    public static readonly Error MissingLikeRecipe = new(
        "suggestions.missing_like_recipe",
        "Asking for recipes like one means naming it: send 'likeRecipeId'.",
        ErrorType.Validation);
}
