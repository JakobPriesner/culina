using Domain.Import;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Stores the libraries a household has connected.</summary>
public interface IRecipeSourceRepository
{
    /// <summary>Loads a connection, token included.</summary>
    /// <param name="sourceId">Which connection.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<RecipeSource>> FindAsync(Guid sourceId, CancellationToken cancellationToken);

    /// <summary>What this household has connected, oldest first.</summary>
    /// <param name="householdId">Whose kitchen.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<RecipeSource>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Stores a new connection.</summary>
    /// <param name="source">The connection to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// A conflict when this household has already connected that address, which
    /// the database decides rather than a read-then-write in the handler.
    /// </returns>
    Task<Result> AddAsync(RecipeSource source, CancellationToken cancellationToken);

    /// <summary>Saves a connection that has changed.</summary>
    /// <param name="source">The changed connection.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> SaveAsync(RecipeSource source, CancellationToken cancellationToken);

    /// <summary>Forgets a connection. The recipes it brought over stay.</summary>
    /// <param name="sourceId">Which connection.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task DeleteAsync(Guid sourceId, CancellationToken cancellationToken);
}

/// <summary>Stores where imported recipes came from.</summary>
public interface IRecipeOriginRepository
{
    /// <summary>Records where a recipe came from.</summary>
    /// <param name="origin">The provenance to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(RecipeOrigin origin, CancellationToken cancellationToken);

    /// <summary>Where one recipe came from, or a failure when it was written here.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<RecipeOrigin>> FindAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of these have already been brought into this kitchen.
    /// </summary>
    /// <param name="householdId">Whose kitchen.</param>
    /// <param name="kind">Which sort of place they came from.</param>
    /// <param name="externalIds">The ids over there to ask about.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>
    /// A map from the id over there to the recipe here, holding only the ones
    /// that are already here.
    /// </returns>
    /// <remarks>
    /// One query for a whole page, rather than one per row. This is asked every
    /// time somebody browses another app's library, and a browse of fifty
    /// recipes should not be fifty queries.
    /// </remarks>
    Task<IReadOnlyDictionary<string, Guid>> AlreadyHereAsync(
        Guid householdId,
        SourceKind kind,
        IReadOnlyList<string> externalIds,
        CancellationToken cancellationToken);
}
