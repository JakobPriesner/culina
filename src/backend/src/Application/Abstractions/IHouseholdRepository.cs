using Domain.Households;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes households and their membership.</summary>
public interface IHouseholdRepository
{
    /// <summary>Loads a household with its members, in one round trip.</summary>
    /// <param name="householdId">Which household.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Result<Household>> FindAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>The households a user belongs to.</summary>
    /// <param name="userId">Whose memberships.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<Household>> ForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Stores a new household and its first owner.</summary>
    /// <param name="household">The household to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(Household household, CancellationToken cancellationToken);

    /// <summary>Saves the name and the full membership list.</summary>
    /// <param name="household">The changed household.</param>
    /// <param name="expectedVersion">The version the caller last saw.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result<long>> UpdateAsync(
        Household household,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Deletes a household and everything it owns.</summary>
    /// <param name="householdId">Which household.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> DeleteAsync(Guid householdId, CancellationToken cancellationToken);
}
