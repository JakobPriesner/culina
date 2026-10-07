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

    /// <summary>Every search the household has, oldest first; not paged, as there are only a handful.</summary>
    /// <param name="householdId">Whose searches.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<SavedSearch>> ListAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new saved search; fails with <see cref="SavedSearchErrors.NameTaken"/> from the unique index,
    /// as a separate check could interleave.
    /// </summary>
    /// <param name="search">The new search.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
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
