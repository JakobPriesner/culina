using Domain.Cooking;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes the record of what someone has cooked.</summary>
public interface ICookLogRepository
{
    /// <summary>This person's entries for one recipe, newest first.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="userId">Whose log.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<CookLogEntry>> ForRecipeAsync(
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Appends an entry.</summary>
    /// <param name="entry">What was cooked.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(CookLogEntry entry, CancellationToken cancellationToken);

    /// <summary>One of this person's entries, or not found.</summary>
    /// <param name="entryId">Which entry.</param>
    /// <param name="userId">Whose it must be.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<CookLogEntry>> FindAsync(
        Guid entryId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Hangs a photo on an entry, or takes the one it has away.</summary>
    /// <param name="entryId">Which entry.</param>
    /// <param name="userId">Whose it must be.</param>
    /// <param name="photo">What was stored, or null to remove.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> SetPhotoAsync(
        Guid entryId,
        Guid userId,
        CookPhoto? photo,
        CancellationToken cancellationToken);

    /// <summary>Removes an entry, for the undo behind the "made it" toast.</summary>
    /// <param name="entryId">Which entry.</param>
    /// <param name="userId">Whose it must be.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> RemoveAsync(Guid entryId, Guid userId, CancellationToken cancellationToken);
}
