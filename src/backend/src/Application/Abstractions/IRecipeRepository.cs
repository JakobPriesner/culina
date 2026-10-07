using Domain.Recipes;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes recipes.</summary>
public interface IRecipeRepository
{
    /// <summary>Loads a recipe with its groups, ingredients and steps.</summary>
    Task<Result<Recipe>> FindAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Loads several recipes in one round trip; missing ones are left out.</summary>
    Task<IReadOnlyDictionary<Guid, Recipe>> FindManyAsync(
        IReadOnlyCollection<Guid> recipeIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The household a recipe belongs to, without loading it: the aggregate costs six result sets
    /// to read one column.
    /// </summary>
    Task<Result<Guid>> HouseholdOfAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Finds matching recipes, one page at a time.</summary>
    Task<RecipePage> SearchAsync(RecipeSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// Counts what the matching recipes could be narrowed by: tags, time ceilings and cuisines.
    /// </summary>
    Task<SearchFacets> FacetsAsync(RecipeSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// The non-built-in units this household and those it inherits from have written, read from the
    /// recipes so no catalogue can disagree with them.
    /// </summary>
    Task<IReadOnlyList<string>> OwnUnitsAsync(IReadOnlyList<Guid> library, CancellationToken cancellationToken);

    /// <summary>
    /// The ingredient names this household and those it inherits from have written, best match
    /// first; read from the recipes like units, since a household's own words beat a seeded list.
    /// </summary>
    Task<IReadOnlyList<string>> OwnIngredientNamesAsync(
        IReadOnlyList<Guid> library,
        string? query,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Stores a new recipe.</summary>
    Task<Result> AddAsync(Recipe recipe, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a changed recipe, contents included, when <c>expectedVersion</c> is still current.
    /// </summary>
    Task<Result<long>> UpdateAsync(
        Recipe recipe,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attaches a stored image to a recipe, replacing any previous one; returns what was replaced.
    /// </summary>
    Task<Result<ImageReplacement>> SetImageAsync(
        Guid recipeId,
        StoredImage image,
        string contentType,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Gives one recipe the picture another has, when it has one.</summary>
    /// <remarks>
    /// A second row for the same stored file, never a second file: storage is content-addressed and
    /// a file is deleted only once nothing points at it.
    /// </remarks>
    Task CopyImageAsync(
        Guid fromRecipeId,
        Guid toRecipeId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Removes a recipe's image; returns what was removed, if anything.</summary>
    Task<Result<ImageReplacement>> RemoveImageAsync(
        Guid recipeId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether anything still points at this stored image: a recipe's picture or a cook photo.
    /// </summary>
    /// <remarks>
    /// Asked before deleting a file: images are content-addressed, so another recipe or cook photo
    /// may share it, and deleting it breaks that one silently.
    /// </remarks>
    Task<bool> IsImageStillUsedAsync(string contentHash, CancellationToken cancellationToken);

    /// <summary>The content hash of a recipe's image, for serving it.</summary>
    Task<Result<string>> ImageHashAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>
    /// The content hashes of several recipes' images, by recipe; recipes without one are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ImageHashesAsync(
        IReadOnlyCollection<Guid> recipeIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Puts a recipe, and everything under it, in the bin; any version other than
    /// <c>expectedVersion</c> is a conflict.
    /// </summary>
    Task<Result> DeleteAsync(
        Guid recipeId,
        long expectedVersion,
        Guid deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
