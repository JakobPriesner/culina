using Domain.Shared;

namespace Domain.Planning;

/// <summary>
/// A recipe this household means to cook on a particular day.
/// </summary>
/// <remarks>
/// Household-owned, like the recipe itself: a plan is what the people who eat
/// together have agreed on, and one that only its author could see would be a
/// diary rather than a plan.
/// </remarks>
public sealed class MealPlanEntry
{
    private MealPlanEntry(
        Guid id,
        Guid householdId,
        DateOnly date,
        Guid recipeId,
        decimal? servings,
        MealSlot slot,
        int sortOrder)
    {
        Id = id;
        HouseholdId = householdId;
        Date = date;
        RecipeId = recipeId;
        Servings = servings;
        Slot = slot;
        SortOrder = sortOrder;
    }

    /// <summary>The entry's id.</summary>
    public Guid Id { get; }

    /// <summary>Whose plan.</summary>
    public Guid HouseholdId { get; }

    /// <summary>Which day. A date, because "Tuesday" has no time zone.</summary>
    public DateOnly Date { get; }

    /// <summary>What is being cooked.</summary>
    public Guid RecipeId { get; }

    /// <summary>
    /// How many it is being made for, or null for however many it was written
    /// for.
    /// </summary>
    public decimal? Servings { get; }

    /// <summary>Which meal of the day.</summary>
    public MealSlot Slot { get; }

    /// <summary>Where it sits among that day's entries.</summary>
    public int SortOrder { get; }

    /// <summary>Plans a meal.</summary>
    /// <param name="householdId">Whose plan.</param>
    /// <param name="date">Which day.</param>
    /// <param name="recipeId">What to cook.</param>
    /// <param name="servings">For how many, or null.</param>
    /// <param name="slot">Which meal of the day.</param>
    /// <param name="sortOrder">Where it sits among that day's entries.</param>
    public static Result<MealPlanEntry> Plan(
        Guid householdId,
        DateOnly date,
        Guid recipeId,
        decimal? servings,
        MealSlot slot,
        int sortOrder)
    {
        if (servings is <= 0 or > 1000)
        {
            return PlanningErrors.InvalidServings;
        }

        return new MealPlanEntry(
            CulinaId.New(),
            householdId,
            date,
            recipeId,
            servings,
            slot,
            sortOrder);
    }

    /// <summary>Rebuilds an entry from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="householdId">Whose plan.</param>
    /// <param name="date">Which day.</param>
    /// <param name="recipeId">What to cook.</param>
    /// <param name="servings">For how many.</param>
    /// <param name="slot">Which meal.</param>
    /// <param name="sortOrder">Where it sits.</param>
    public static MealPlanEntry Restore(
        Guid id,
        Guid householdId,
        DateOnly date,
        Guid recipeId,
        decimal? servings,
        MealSlot slot,
        int sortOrder) =>
        new(id, householdId, date, recipeId, servings, slot, sortOrder);
}

/// <summary>
/// Which meal of the day something is for.
/// </summary>
/// <remarks>
/// Three, and no "snack" or "dessert". A slot only earns its place if it
/// changes what you buy, and the week view hides every slot nothing is planned
/// in — so for the household that only ever plans dinner, the concept is
/// invisible.
/// </remarks>
public enum MealSlot
{
    /// <summary>The first meal.</summary>
    Breakfast = 0,

    /// <summary>The middle one.</summary>
    Lunch = 1,

    /// <summary>The one people actually plan.</summary>
    Dinner = 2
}
