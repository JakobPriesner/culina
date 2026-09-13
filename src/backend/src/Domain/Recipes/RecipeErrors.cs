using Domain.Shared;

namespace Domain.Recipes;

/// <summary>Every failure the recipes module can return.</summary>
public static class RecipeErrors
{
    /// <summary>No such recipe, or none the caller may see.</summary>
    public static Error NotFound(Guid recipeId) => new(
        "recipes.not_found",
        $"No recipe with id '{recipeId}' exists.",
        ErrorType.NotFound);

    /// <summary>The title is blank or too long.</summary>
    public static readonly Error InvalidTitle = new(
        "recipes.invalid_title",
        "A title is required, and it may be at most 200 characters.",
        ErrorType.Validation);

    /// <summary>The yield is zero, negative or implausible.</summary>
    public static readonly Error InvalidYield = new(
        "recipes.invalid_yield",
        "A recipe must make more than nothing, and less than a thousand of it.",
        ErrorType.Validation);

    /// <summary>An amount is negative or implausibly large.</summary>
    public static readonly Error InvalidQuantity = new(
        "recipes.invalid_quantity",
        "An amount must be greater than zero.",
        ErrorType.Validation);

    /// <summary>A unit is blank, too long, or not made of words.</summary>
    public static readonly Error InvalidUnit = new(
        "recipes.invalid_unit",
        "A unit is written in words: 'g', 'tbsp', 'Schuss'. At most 16 characters.",
        ErrorType.Validation);

    /// <summary>Two amounts cannot be added because their units do not combine.</summary>
    public static readonly Error IncompatibleUnits = new(
        "recipes.incompatible_units",
        "Those amounts are in units that cannot be added together.",
        ErrorType.Validation);

    /// <summary>An ingredient name is blank or too long.</summary>
    public static readonly Error InvalidIngredientName = new(
        "recipes.invalid_ingredient_name",
        "An ingredient needs a name of at most 120 characters.",
        ErrorType.Validation);

    /// <summary>A step's text is blank or too long.</summary>
    public static readonly Error InvalidStepText = new(
        "recipes.invalid_step_text",
        "A step needs text of at most 4000 characters.",
        ErrorType.Validation);

    /// <summary>A step refers to an ingredient this recipe does not have.</summary>
    public static readonly Error UnknownIngredientReference = new(
        "recipes.unknown_ingredient_reference",
        "A step refers to an ingredient that is not in this recipe.",
        ErrorType.Validation);

    /// <summary>An ingredient cannot be removed while a step still mentions it.</summary>
    public static Error IngredientInUse(int stepNumber) => new(
        "recipes.ingredient_in_use",
        $"Step {stepNumber} still refers to that ingredient. Remove the mention first.",
        ErrorType.Conflict);

    /// <summary>The recipe has more ingredients than anyone could cook from.</summary>
    public static readonly Error TooManyIngredients = new(
        "recipes.too_many_ingredients",
        "A recipe may have at most 200 ingredients.",
        ErrorType.Validation);

    /// <summary>The recipe has more steps than anyone could follow.</summary>
    public static readonly Error TooManySteps = new(
        "recipes.too_many_steps",
        "A recipe may have at most 100 steps.",
        ErrorType.Validation);

    /// <summary>A time is negative or implausibly long.</summary>
    public static readonly Error InvalidDuration = new(
        "recipes.invalid_duration",
        "A time must be between zero and one week.",
        ErrorType.Validation);
}
