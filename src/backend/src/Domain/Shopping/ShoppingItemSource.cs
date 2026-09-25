using Domain.Planning;
using Domain.Recipes;

namespace Domain.Shopping;

/// <summary>
/// How much of one line a recipe asked for, and for which planned meal.
/// </summary>
/// <remarks>
/// <para>
/// A line is a sum — 200 g of butter from one recipe and 100 g from another is
/// one line reading 300 g — and without its sources nothing can say what it is
/// a sum of. That is what makes adding a planned week something that can be
/// repeated: a meal whose sources are already here is already shopped for.
/// And it is what makes taking a meal back possible at all, because only the
/// source knows how much of the 300 g was that meal's.
/// </para>
/// <para>
/// The quantity is the recipe's, scaled to what was being cooked and in the
/// unit it was written in; the line holds the sum in its canonical unit.
/// </para>
/// </remarks>
/// <param name="RecipeId">The recipe that asked for it.</param>
/// <param name="RecipeTitle">What that recipe is currently called.</param>
/// <param name="PlanEntryId">
/// The planned meal it is for, or null for a recipe put on the list by itself.
/// </param>
/// <param name="PlannedDate">Which day it is planned for, when it is planned.</param>
/// <param name="PlannedSlot">Which meal it is planned for, when it is planned.</param>
/// <param name="Quantity">How much it asked for.</param>
public sealed record ShoppingItemSource(
    Guid RecipeId,
    string RecipeTitle,
    Guid? PlanEntryId,
    DateOnly? PlannedDate,
    MealSlot? PlannedSlot,
    Quantity Quantity);
