using Domain.Planning;
using Domain.Recipes;

namespace Domain.Shopping;

/// <summary>How much of one line a recipe asked for, and for which planned meal.</summary>
/// <remarks>
/// A line is a sum, and its sources are what make adding a planned week repeatable and taking a meal back possible.
/// The quantity is the recipe's, scaled and in its written unit; the line holds the sum in its canonical unit.
/// </remarks>
public sealed record ShoppingItemSource(
    Guid RecipeId,
    string RecipeTitle,
    Guid? PlanEntryId,
    DateOnly? PlannedDate,
    MealSlot? PlannedSlot,
    Quantity Quantity);
