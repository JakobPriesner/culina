using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Stores a household's shelves of recipes.</summary>
/// <remarks>
/// What is on a shelf is written through here directly rather than through the
/// <see cref="Cookbook"/> aggregate. A cookbook has no size bound, so loading
/// every membership row to rename it — or to add one recipe — would be work
/// nobody asked for. The meal plan's entries are handled the same way.
/// </remarks>
public interface ICookbookRepository
{
    /// <summary>One cookbook's own metadata.</summary>
    /// <param name="cookbookId">Which one.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<Cookbook>> FindAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>One cookbook, with what its page draws.</summary>
    /// <param name="cookbookId">Which one.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<CookbookOnAShelf>> DescribeAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>A page of the household's cookbooks, most recently changed first.</summary>
    /// <param name="householdId">Whose shelves.</param>
    /// <param name="cursor">Where to resume, or null for the first page.</param>
    /// <param name="limit">How many at most.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<CookbookPage> ListAsync(
        Guid householdId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Writes a new cookbook.</summary>
    /// <param name="cookbook">The new shelf.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(Cookbook cookbook, CancellationToken cancellationToken);

    /// <summary>Saves a rename, checking the version in the SQL.</summary>
    /// <param name="cookbook">What it should now say.</param>
    /// <param name="expectedVersion">The version the caller was holding.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result<long>> SaveAsync(
        Cookbook cookbook,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Removes a cookbook, leaving every recipe that was on it.</summary>
    /// <param name="cookbookId">Which one.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task DeleteAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a recipe on a shelf, or leaves it where it already is.
    /// </summary>
    /// <param name="cookbookId">Which shelf.</param>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="addedBy">Who put it there.</param>
    /// <param name="now">When.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether a row was actually written.</returns>
    /// <remarks>
    /// False means it was already on, which is a success and not an error: a
    /// double tap and a retried request are both ordinary. The caller uses the
    /// answer to decide whether the cookbook's own version needs bumping —
    /// churning it on a no-op would throw away a good cached copy for nothing.
    /// </remarks>
    Task<bool> AddRecipeAsync(
        Guid cookbookId,
        Guid recipeId,
        Guid addedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Takes a recipe off a shelf.</summary>
    /// <param name="cookbookId">Which shelf.</param>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether a row was actually removed.</returns>
    Task<bool> RemoveRecipeAsync(Guid cookbookId, Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Records that what is on a shelf changed.</summary>
    /// <param name="cookbookId">Which shelf.</param>
    /// <param name="now">When.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// Unconditional, with no version check: adding a recipe is not an edit two
    /// people can lose each other's work over, so making it a concurrency event
    /// would cost a conflict dialog and buy nothing.
    /// </remarks>
    Task TouchAsync(Guid cookbookId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Every recipe on a shelf, whichever kind it is, by id.</summary>
    /// <param name="cookbookId">Which shelf.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Guid>> RecipeIdsAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>Which of this household's shelves a recipe is on.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="householdId">Whose shelves.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<CookbookOnAShelf>> ContainingAsync(
        Guid recipeId,
        Guid householdId,
        CancellationToken cancellationToken);
}

/// <summary>A cookbook with what a card needs to draw it.</summary>
/// <param name="Cookbook">The shelf itself.</param>
/// <param name="RecipeCount">How many recipes are on it.</param>
/// <param name="CoverRecipeIds">
/// Up to four photographed recipes, oldest first — so a cover stops moving once
/// there are four, rather than changing face every time something is added. The
/// recipes and not their images, because a picture is served from the recipe's
/// own address.
/// </param>
public sealed record CookbookOnAShelf(
    Cookbook Cookbook,
    int RecipeCount,
    IReadOnlyList<Guid> CoverRecipeIds);

/// <summary>A page of cookbooks.</summary>
/// <param name="Items">The cookbooks on it.</param>
/// <param name="NextCursor">Where the next page resumes, or null at the end.</param>
/// <param name="Total">How many the household has.</param>
public sealed record CookbookPage(
    IReadOnlyList<CookbookOnAShelf> Items,
    string? NextCursor,
    int Total);
