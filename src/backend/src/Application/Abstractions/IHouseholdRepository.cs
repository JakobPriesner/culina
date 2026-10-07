using Domain.Households;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes households and their membership.</summary>
public interface IHouseholdRepository
{
    /// <summary>Loads a household with its members, in one round trip.</summary>
    Task<Result<Household>> FindAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>The households a user belongs to.</summary>
    Task<IReadOnlyList<Household>> ForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Stores a new household and its first owner.</summary>
    Task<Result> AddAsync(Household household, CancellationToken cancellationToken);

    /// <summary>Saves the name, what it inherits from, and the full membership list.</summary>
    Task<Result<long>> UpdateAsync(
        Household household,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether a user belongs to a household: one indexed lookup, without loading the members.
    /// </summary>
    Task<bool> IsMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a user may see a household's recipes: they are in it, or in a household inheriting
    /// from it, however indirectly.
    /// </summary>
    /// <remarks>Seeing, never changing: editing still needs <see cref="IsMemberAsync"/>.</remarks>
    Task<bool> CanSeeRecipesAsync(Guid householdId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The households this one inherits recipes from, nearest first; empty if none.
    /// </summary>
    Task<IReadOnlyList<InheritedHousehold>> AncestorsAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every household that sees this one's recipes, directly or through others, nearest first.
    /// </summary>
    Task<IReadOnlyList<Heir>> HeirsAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>The household's members, with their names, for the members screen.</summary>
    Task<IReadOnlyList<HouseholdMemberView>> MembersAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Puts a household, and everything it owns, in the bin.</summary>
    Task<Result> DeleteAsync(
        Guid householdId,
        long expectedVersion,
        Guid deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
