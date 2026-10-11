using Domain.Shared;

namespace Domain.Nutrition;

/// <summary>Every failure the nutrition module can return.</summary>
public static class NutritionErrors
{
    /// <summary>The code names no food of the Bundeslebensmittelschlüssel.</summary>
    public static readonly Error UnknownFood = new(
        "nutrition.unknown_food",
        "No food in the Bundeslebensmittelschlüssel has that code.",
        ErrorType.Validation);

    /// <summary>A weight per unit must be more than nothing and no more than one unit could plausibly weigh.</summary>
    public static readonly Error InvalidGrams = new(
        "nutrition.invalid_grams",
        "A weight must be more than 0 g and at most 10000 g.",
        ErrorType.Validation);

    /// <summary>Grams, millilitres and cups already have a size, so a household cannot give them a weight.</summary>
    public static readonly Error UnitHasASize = new(
        "nutrition.unit_has_a_size",
        "That unit already has a size. A weight can be set for a piece, a clove, a spoon, a can and the like.",
        ErrorType.Validation);
}
