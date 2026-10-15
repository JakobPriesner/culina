using Domain.Shared;

namespace Domain.Ingredients;

/// <summary>Failures from merging ingredient names.</summary>
public static class IngredientErrors
{
    /// <summary>A merge needs two names, each one a recipe line could carry.</summary>
    public static readonly Error InvalidName = new(
        "ingredients.invalid_name",
        "An ingredient needs a name of at most 120 characters.",
        ErrorType.Validation);

    /// <summary>Merging a name into itself changes nothing.</summary>
    public static readonly Error SameName = new(
        "ingredients.same_name",
        "An ingredient cannot be merged into itself.",
        ErrorType.Validation);

    /// <summary>The household has listed as many merges as it may keep.</summary>
    public static readonly Error TooManyMerges = new(
        "ingredients.too_many_merges",
        "A household may keep at most 1000 ingredient merges; remove one to make another.",
        ErrorType.Validation);

    /// <summary>The household has listed as many words in both numbers as it may keep.</summary>
    public static readonly Error TooManyNouns = new(
        "ingredients.too_many_nouns",
        "A household may list at most 500 words of its own; remove one to add another.",
        ErrorType.Validation);

    /// <summary>A noun in both numbers is two different words, each one word.</summary>
    public static readonly Error InvalidNoun = new(
        "ingredients.invalid_noun",
        "Singular and plural need to be one word each, and differ.",
        ErrorType.Validation);
}
