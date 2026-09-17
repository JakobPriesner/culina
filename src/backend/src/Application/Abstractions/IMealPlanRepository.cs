using Domain.Planning;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Stores what a household means to cook.</summary>
public interface IMealPlanRepository
{
    /// <summary>
    /// A week of the plan, with enough of each recipe to draw a card.
    /// </summary>
    /// <param name="householdId">Whose plan.</param>
    /// <param name="from">The day the week starts on.</param>
    /// <param name="days">How many days it runs for.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// The recipe's title and picture come back with it. A week view showing
    /// seven cards would otherwise be seven more requests, and a plan whose
    /// cards arrive one at a time is a plan that flickers.
    /// </remarks>
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
    /// <remarks>
    /// Moving a meal needs the slot it already has, because a move that leaves
    /// the slot out keeps it — and ownership is proved by the same read rather
    /// than by a second one.
    /// </remarks>
    Task<Result<MealPlanEntry>> FindAsync(
        Guid entryId,
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts an entry on its new day, and closes the gaps that leaves.
    /// </summary>
    /// <param name="moved">The entry as it should now be.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// The day it lands on is renumbered from zero afterwards, so a position
    /// is always an index and never a number a client has to guess between.
    /// The day it left keeps its gaps: order survives them, and renumbering a
    /// day nobody is looking at is a write for nothing.
    /// </remarks>
    Task<Result> MoveAsync(MealPlanEntry moved, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a planned meal off, and says which day it was on.
    /// </summary>
    /// <param name="entryId">Which entry.</param>
    /// <param name="householdId">Whose plan it must be.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// The day comes back because the caller has to read that week again, and
    /// asking for the entry first only to delete it is a round trip to learn
    /// something the delete already knows.
    /// </remarks>
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
public sealed record PlannedRecipe(
    MealPlanEntry Entry,
    string Title,
    Guid? ImageId,
    int? TotalMinutes,
    decimal RecipeServings);
