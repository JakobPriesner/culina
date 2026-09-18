using Domain.Searches;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Stores a household's saved searches.</summary>
public interface ISavedSearchRepository
{
    /// <summary>One saved search.</summary>
    /// <param name="searchId">Which one.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<SavedSearch>> FindAsync(Guid searchId, CancellationToken cancellationToken);

    /// <summary>
    /// Every one the household has, oldest first.
    /// </summary>
    /// <remarks>
    /// Not paged, and not bounded by a cursor: these are drawn as a row of
    /// chips beside the search field, so the whole list is what the caller
    /// needs and it is a handful of rows.
    /// </remarks>
    /// <param name="householdId">Whose searches.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<SavedSearch>> ListAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>Writes a new saved search.</summary>
    /// <param name="search">The new search.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// Fails with <see cref="SavedSearchErrors.NameTaken"/> when the household
    /// already has one by that name — caught from the unique index rather than
    /// checked first, because a check and an insert are two statements two
    /// people can interleave.
    /// </remarks>
    Task<Result> AddAsync(SavedSearch search, CancellationToken cancellationToken);

    /// <summary>Saves a rename, and whatever it now asks for.</summary>
    /// <param name="search">What it should now say.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> SaveAsync(SavedSearch search, CancellationToken cancellationToken);

    /// <summary>Forgets a saved search.</summary>
    /// <param name="searchId">Which one.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task DeleteAsync(Guid searchId, CancellationToken cancellationToken);
}
