using Application.Abstractions;
using Domain.Households;
using Domain.Shared;

namespace Application.Households;

/// <summary>Answers whether the caller belongs to a household; a non-member is told it does not exist, never that it is forbidden.</summary>
internal static class HouseholdAccess
{
    /// <summary>Whether the caller is in this household; the same answer for reading and writing (members, not roles).</summary>
    internal static async Task<Result> MemberOfAsync(
        IHouseholdRepository households,
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await households.IsMemberAsync(householdId, userId, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : HouseholdErrors.NotFound(householdId);

    /// <summary>Every household whose recipes this one sees: itself first, then what it inherits, nearest first. Says nothing about the caller.</summary>
    internal static async Task<IReadOnlyList<Guid>> LibraryAsync(
        IHouseholdRepository households,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var ancestors = await households.AncestorsAsync(householdId, cancellationToken).ConfigureAwait(false);

        return [householdId, .. ancestors.Select(ancestor => ancestor.HouseholdId)];
    }
}
