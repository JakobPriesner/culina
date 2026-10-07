using Domain.Recipes;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes recipes.</summary>
public interface IRecipeRepository
{
    /// <summary>Loads a recipe with its groups, ingredients and steps.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<Recipe>> FindAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Loads several recipes in one round trip; missing ones are left out.</summary>
    /// <param name="recipeIds">Which recipes.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyDictionary<Guid, Recipe>> FindManyAsync(
        IReadOnlyCollection<Guid> recipeIds,
        CancellationToken cancellationToken);

    /// <summary>The household a recipe belongs to, without loading the recipe.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>For access checks: loading the whole aggregate costs six result sets to read one column.</remarks>
    Task<Result<Guid>> HouseholdOfAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Finds matching recipes, one page at a time.</summary>
    /// <param name="search">What to look for.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<RecipePage> SearchAsync(RecipeSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// Counts what every recipe the search matches could be narrowed by:
    /// tags, time ceilings and cuisines.
    /// </summary>
    Task<SearchFacets> FacetsAsync(RecipeSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// The units this household and those it inherits from have written that
    /// are not built in.
    /// </summary>
    /// <param name="library">Whose recipes: a household, then every household it inherits from.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// Read from the recipes rather than from a table of its own. A unit exists
    /// because something is measured in it, so there is no list to maintain and
    /// no way for a catalogue to disagree with what the recipes actually say.
    /// </remarks>
    Task<IReadOnlyList<string>> OwnUnitsAsync(IReadOnlyList<Guid> library, CancellationToken cancellationToken);

    /// <summary>
    /// The ingredient names this household and those it inherits from have
    /// written, best match first.
    /// </summary>
    /// <param name="library">Whose recipes: a household, then every household it inherits from.</param>
    /// <param name="query">What has been typed, which may be empty.</param>
    /// <param name="limit">At most this many.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// A household's own words beat a seeded list after the first few recipes:
    /// they are how these particular people talk about food. Read from the
    /// recipes rather than from a catalogue, for the same reason units are.
    /// </remarks>
    Task<IReadOnlyList<string>> OwnIngredientNamesAsync(
        IReadOnlyList<Guid> library,
        string? query,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Stores a new recipe.</summary>
    /// <param name="recipe">The recipe to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(Recipe recipe, CancellationToken cancellationToken);

    /// <summary>Saves a changed recipe, contents included.</summary>
    /// <param name="recipe">The changed recipe.</param>
    /// <param name="expectedVersion">The version the caller last saw.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result<long>> UpdateAsync(
        Recipe recipe,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Attaches a stored image to a recipe, replacing any previous one.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="image">What was stored.</param>
    /// <param name="contentType">The media type of the renditions.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What this image replaced, if anything.</returns>
    Task<Result<ImageReplacement>> SetImageAsync(
        Guid recipeId,
        StoredImage image,
        string contentType,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gives one recipe the picture another has, when it has one.
    /// </summary>
    /// <param name="fromRecipeId">The recipe whose picture it is.</param>
    /// <param name="toRecipeId">The recipe that gets it too.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// A second row pointing at the same stored file, never a second file:
    /// storage is content-addressed, and a file is only deleted once nothing
    /// points at it, so either recipe can change or lose its picture without
    /// touching the other's.
    /// </remarks>
    Task CopyImageAsync(
        Guid fromRecipeId,
        Guid toRecipeId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Removes a recipe's image.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What was removed, if anything.</returns>
    Task<Result<ImageReplacement>> RemoveImageAsync(
        Guid recipeId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether anything still points at this stored image: a recipe's picture
    /// or a cook photo.
    /// </summary>
    /// <param name="contentHash">Which image.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// <para>
    /// Image storage is content-addressed, so the same picture stored twice is
    /// one file with two rows pointing at it. That used to need somebody to
    /// upload the identical photo twice; importing a library where fifty
    /// recipes carry the same placeholder makes it ordinary.
    /// </para>
    /// <para>
    /// Asked before a file is deleted, because deleting one that another recipe
    /// or somebody's cook photo is still pointing at does not break the recipe
    /// being edited — it breaks a different one, silently, and nothing connects
    /// the two.
    /// </para>
    /// </remarks>
    Task<bool> IsImageStillUsedAsync(string contentHash, CancellationToken cancellationToken);

    /// <summary>The content hash of a recipe's image, for serving it.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<string>> ImageHashAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>The content hashes of several recipes' images, by recipe; recipes without one are left out.</summary>
    /// <param name="recipeIds">Which recipes.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyDictionary<Guid, string>> ImageHashesAsync(
        IReadOnlyCollection<Guid> recipeIds,
        CancellationToken cancellationToken);

    /// <summary>Puts a recipe, and everything under it, in the bin.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="expectedVersion">The version the caller saw; any other is a conflict.</param>
    /// <param name="deletedBy">Who is deleting it.</param>
    /// <param name="now">The injected current time; the purge counts from it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> DeleteAsync(
        Guid recipeId,
        long expectedVersion,
        Guid deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
