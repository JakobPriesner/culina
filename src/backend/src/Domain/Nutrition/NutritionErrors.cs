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
}
