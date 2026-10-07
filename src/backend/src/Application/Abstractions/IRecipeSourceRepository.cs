using Domain.Import;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Stores the libraries a household has connected.</summary>
public interface IRecipeSourceRepository
{
    /// <summary>Loads a connection, token included.</summary>
    Task<Result<RecipeSource>> FindAsync(Guid sourceId, CancellationToken cancellationToken);

    /// <summary>What this household has connected, oldest first.</summary>
    Task<IReadOnlyList<RecipeSource>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new connection; a conflict when this household already connected that address,
    /// decided by the database rather than a read-then-write.
    /// </summary>
    Task<Result> AddAsync(RecipeSource source, CancellationToken cancellationToken);

    /// <summary>Saves a connection that has changed.</summary>
    Task<Result> SaveAsync(RecipeSource source, CancellationToken cancellationToken);

    /// <summary>Forgets a connection. The recipes it brought over stay.</summary>
    Task DeleteAsync(Guid sourceId, CancellationToken cancellationToken);
}

/// <summary>Stores where imported recipes came from.</summary>
public interface IRecipeOriginRepository
{
    /// <summary>Records where a recipe came from.</summary>
    Task<Result> AddAsync(RecipeOrigin origin, CancellationToken cancellationToken);

    /// <summary>Where one recipe came from, or a failure when it was written here.</summary>
    Task<Result<RecipeOrigin>> FindAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of these external ids have already been brought into this kitchen, as a map from the
    /// id over there to the recipe here.
    /// </summary>
    /// <remarks>
    /// One query per page: a browse of fifty recipes should not be fifty queries.
    /// </remarks>
    Task<IReadOnlyDictionary<string, Guid>> AlreadyHereAsync(
        Guid householdId,
        SourceKind kind,
        IReadOnlyList<string> externalIds,
        CancellationToken cancellationToken);
}
