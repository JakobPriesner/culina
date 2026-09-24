using Application.Abstractions;
using Contracts.Planning;
using Domain.Planning;
using Domain.Shared;

namespace Application.Planning;

/// <summary>
/// The wire spelling of a slot, the parse back, and what a week is.
/// </summary>
internal static class PlanningWords
{
    internal static string Of(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "breakfast",
        MealSlot.Lunch => "lunch",
        _ => "dinner"
    };

    /// <summary>
    /// Reads a slot, defaulting to dinner.
    /// </summary>
    /// <param name="value">The wire code, or null.</param>
    /// <remarks>
    /// Omitting it is the ordinary case: most planned meals are dinner, and a
    /// household that never plans anything else never has to know the concept
    /// exists.
    /// </remarks>
    internal static Result<MealSlot> ToSlot(string? value) => value switch
    {
        null or "" or "dinner" => MealSlot.Dinner,
        "breakfast" => MealSlot.Breakfast,
        "lunch" => MealSlot.Lunch,
        _ => new FieldError(
            "slot",
            PlanningErrors.InvalidRange.Code,
            "A meal is 'breakfast', 'lunch' or 'dinner'.")
    };

}

/// <summary>Maps planned meals onto the shape the week view reads.</summary>
internal static class MealPlanMappings
{
    internal static MealPlanResponse ToResponse(
        this IReadOnlyList<PlannedRecipe> planned,
        DateOnly from)
    {
        ArgumentNullException.ThrowIfNull(planned);

        return new MealPlanResponse
        {
            From = from,
            // Every day, planned or not. A week with holes in it is a week the
            // client has to fill in itself, and an empty day is exactly where
            // the screen offers to add something.
            Days =
            [
                .. Enumerable.Range(0, PlanWeek.Days).Select(offset =>
                {
                    var date = from.AddDays(offset);

                    return new PlannedDay
                    {
                        Date = date,
                        Meals =
                        [
                            .. planned
                                .Where(one => one.Entry.Date == date)
                                .OrderBy(one => one.Entry.Slot)
                                .ThenBy(one => one.Entry.SortOrder)
                                .Select(ToMeal)
                        ]
                    };
                })
            ]
        };
    }

    private static PlannedMeal ToMeal(PlannedRecipe planned) => new()
    {
        EntryId = planned.Entry.Id,
        RecipeId = planned.Entry.RecipeId,
        Title = planned.Title,
        ImageId = planned.ImageId,
        TotalMinutes = planned.TotalMinutes,
        Servings = planned.Entry.Servings,
        RecipeServings = planned.RecipeServings,
        Slot = PlanningWords.Of(planned.Entry.Slot),
        IsOnShoppingList = planned.IsOnShoppingList
    };
}
