using Application.Abstractions;
using Domain.Households;
using Domain.Shared;

namespace Application.Households;

/// <summary>
/// Answers whether the caller belongs to a household.
/// </summary>
/// <remarks>
/// <para>
/// The question every feature asks before it reads or writes anything a
/// household owns — planning, shopping, archives, cookbooks, saved searches and
/// recipes all ask it, which is why it lives with households rather than with
/// any one of them.
/// </para>
/// <para>
/// The part that matters is the error: a non-member is told the household does
/// not exist, never that it exists and is forbidden, so a stranger learns
/// nothing about which households there are.
/// </para>
/// </remarks>
internal static class HouseholdAccess
{
    /// <summary>
    /// Whether the caller is in this household at all.
    /// </summary>
    /// <param name="households">The household repository.</param>
    /// <param name="householdId">The household being reached into.</param>
    /// <param name="userId">Who is asking.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <remarks>
    /// The same answer for reading and for writing: a household has members,
    /// not roles, and a member may do anything in their own kitchen.
    /// </remarks>
    internal static async Task<Result> MemberOfAsync(
        IHouseholdRepository households,
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await households.IsMemberAsync(householdId, userId, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : HouseholdErrors.NotFound(householdId);

    /// <summary>
    /// Every household whose recipes this one sees: itself first, then what it
    /// inherits, nearest first.
    /// </summary>
    /// <param name="households">The household repository.</param>
    /// <param name="householdId">The household whose library it is.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <remarks>
    /// Says nothing about the caller: ask <see cref="MemberOfAsync"/> first.
    /// </remarks>
    internal static async Task<IReadOnlyList<Guid>> LibraryAsync(
        IHouseholdRepository households,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var ancestors = await households.AncestorsAsync(householdId, cancellationToken).ConfigureAwait(false);

        return [householdId, .. ancestors.Select(ancestor => ancestor.HouseholdId)];
    }
}
