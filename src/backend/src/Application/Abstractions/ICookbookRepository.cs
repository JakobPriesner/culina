using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Stores a household's shelves of recipes.</summary>
/// <remarks>
/// Shelf contents are written directly, not through <see cref="Cookbook"/>: it has no size bound,
/// so loading every membership row to rename it would be wasted work.
/// </remarks>
public interface ICookbookRepository
{
    /// <summary>One cookbook's own metadata.</summary>
    Task<Result<Cookbook>> FindAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>One cookbook, with what its page draws.</summary>
    Task<Result<CookbookOnAShelf>> DescribeAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>A page of the household's cookbooks, most recently changed first.</summary>
    Task<CookbookPage> ListAsync(
        Guid householdId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Writes a new cookbook.</summary>
    Task<Result> AddAsync(Cookbook cookbook, CancellationToken cancellationToken);

    /// <summary>Saves a rename, checking the version in the SQL.</summary>
    Task<Result<long>> SaveAsync(
        Cookbook cookbook,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Puts a cookbook in the bin, leaving every recipe that was on it.</summary>
    Task<Result> DeleteAsync(
        Guid cookbookId,
        long expectedVersion,
        Guid deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Puts a recipe on a shelf, or leaves it where it already is.</summary>
    /// <returns>
    /// Whether a row was written; false (already on) is a success, and spares the cookbook's
    /// version a no-op bump.
    /// </returns>
    Task<bool> AddRecipeAsync(
        Guid cookbookId,
        Guid recipeId,
        Guid addedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Takes a recipe off a shelf; returns whether a row was removed.</summary>
    Task<bool> RemoveRecipeAsync(Guid cookbookId, Guid recipeId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that what is on a shelf changed, with no version check: adding a recipe is not a
    /// lost-update risk.
    /// </summary>
    Task TouchAsync(Guid cookbookId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Every recipe on a shelf, whichever kind it is, by id.</summary>
    Task<IReadOnlyList<Guid>> RecipeIdsAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>Which of this household's shelves a recipe is on.</summary>
    Task<IReadOnlyList<CookbookOnAShelf>> ContainingAsync(
        Guid recipeId,
        Guid householdId,
        CancellationToken cancellationToken);
}

/// <summary>A cookbook with what a card needs to draw it.</summary>
/// <param name="Cookbook">The shelf itself.</param>
/// <param name="RecipeCount">How many recipes are on it.</param>
/// <param name="Cover">
/// Up to four photographed recipes, oldest first, so the cover stops changing once full.
/// </param>
public sealed record CookbookOnAShelf(
    Cookbook Cookbook,
    int RecipeCount,
    IReadOnlyList<CoverPicture> Cover);

/// <summary>One picture on a cookbook's cover.</summary>
/// <param name="RecipeId">Whose picture; served from the recipe's own address.</param>
/// <param name="ImageId">
/// The recipe's current picture; part of the address, so a replacement is a new address.
/// </param>
public sealed record CoverPicture(Guid RecipeId, Guid ImageId);

/// <summary>A page of cookbooks.</summary>
/// <param name="Items">The cookbooks on it.</param>
/// <param name="NextCursor">Where the next page resumes, or null at the end.</param>
/// <param name="Total">How many the household has.</param>
public sealed record CookbookPage(
    IReadOnlyList<CookbookOnAShelf> Items,
    string? NextCursor,
    int Total);
