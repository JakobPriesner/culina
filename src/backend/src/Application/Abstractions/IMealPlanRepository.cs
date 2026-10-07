using Domain.Planning;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Stores what a household means to cook.</summary>
public interface IMealPlanRepository
{
    /// <summary>A week of the plan, with each recipe's card data.</summary>
    /// <param name="householdId">Whose plan.</param>
    /// <param name="from">The day the week starts on.</param>
    /// <param name="days">How many days it runs for.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<PlannedRecipe>> ForWeekAsync(
        Guid householdId,
        DateOnly from,
        int days,
        CancellationToken cancellationToken);

    /// <summary>Where a new entry sits among that day's.</summary>
    /// <param name="householdId">Whose plan.</param>
    /// <param name="date">Which day.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<int> NextSortOrderAsync(Guid householdId, DateOnly date, CancellationToken cancellationToken);

    /// <summary>Plans a meal.</summary>
    /// <param name="entry">What to plan.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(MealPlanEntry entry, CancellationToken cancellationToken);

    /// <summary>Reads one entry, if it is this household's.</summary>
    /// <param name="entryId">Which entry.</param>
    /// <param name="householdId">Whose plan it must be.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<MealPlanEntry>> FindAsync(
        Guid entryId,
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Puts an entry on its new day.</summary>
    /// <param name="moved">The entry as it should now be.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>The destination day is renumbered from zero; the day it left keeps its gaps.</remarks>
    Task<Result> MoveAsync(MealPlanEntry moved, CancellationToken cancellationToken);

    /// <summary>Takes a planned meal off and returns the day it was on.</summary>
    /// <param name="entryId">Which entry.</param>
    /// <param name="householdId">Whose plan it must be.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result<DateOnly>> RemoveAsync(
        Guid entryId,
        Guid householdId,
        CancellationToken cancellationToken);
}

/// <summary>A planned meal, with what the card needs to show it.</summary>
/// <param name="Entry">The plan entry.</param>
/// <param name="Title">What the recipe is called.</param>
/// <param name="ImageId">Its picture, if it has one.</param>
/// <param name="TotalMinutes">Hands-on plus cooking, when both are known.</param>
/// <param name="RecipeServings">What the recipe itself is written for.</param>
/// <param name="IsOnShoppingList">Whether its ingredients are on the shopping list.</param>
public sealed record PlannedRecipe(
    MealPlanEntry Entry,
    string Title,
    Guid? ImageId,
    int? TotalMinutes,
    decimal RecipeServings,
    bool IsOnShoppingList);
