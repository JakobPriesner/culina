using Domain.Shared;

namespace Domain.Households;

/// <summary>
/// Every rule about who may do what in a household.
/// </summary>
/// <remarks>
/// They live here, in one place, so no handler re-derives them and no two
/// endpoints can disagree about whether the last owner may leave.
/// </remarks>
public static class HouseholdMembershipPolicy
{
    /// <summary>The caller must be a member to see anything at all.</summary>
    /// <param name="household">The household in question.</param>
    /// <param name="actingUserId">Who is asking.</param>
    /// <returns>
    /// <c>households.not_found</c> for a non-member, never
    /// <c>households.not_owner</c>: answering "forbidden" would confirm the
    /// household exists.
    /// </returns>
    public static Result CanView(Household household, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);

        return household.Find(actingUserId) is null
            ? HouseholdErrors.NotFound(household.Id)
            : Result.Success();
    }

    /// <summary>Inviting, removing, renaming and deleting need an owner.</summary>
    /// <param name="household">The household in question.</param>
    /// <param name="actingUserId">Who is asking.</param>
    public static Result CanAdminister(Household household, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);

        return CanView(household, actingUserId)
            .Bind(() => household.Find(actingUserId)!.Role == HouseholdRole.Owner
                ? Result.Success()
                : HouseholdErrors.NotOwner);
    }

    /// <summary>
    /// Inheriting is an owner's decision about their own household, made with
    /// recipes they can already see.
    /// </summary>
    /// <param name="household">The household that would inherit.</param>
    /// <param name="parent">The household it would inherit from.</param>
    /// <param name="parentLibrary">Every household whose recipes the parent sees, itself included.</param>
    /// <param name="actingUserId">Who is asking.</param>
    /// <remarks>
    /// The caller must be in the parent, because inheriting shows its recipes
    /// to everybody in the heir: a stranger to the parent could otherwise open
    /// somebody else's kitchen to their own friends. A non-member of the parent
    /// is told it does not exist, as everywhere else. A parent that already
    /// sees this household's recipes would close a loop, and a loop has no
    /// answer to "whose recipe is this".
    /// </remarks>
    public static Result CanInherit(
        Household household,
        Household parent,
        IReadOnlyCollection<Guid> parentLibrary,
        Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(parentLibrary);

        return CanAdminister(household, actingUserId)
            .Bind(() => CanView(parent, actingUserId))
            .Bind(() => parent.Id == household.Id || parentLibrary.Contains(household.Id)
                ? HouseholdErrors.InheritanceCycle
                : Result.Success());
    }

    /// <summary>
    /// An owner of the household being inherited from may cut an heir loose.
    /// </summary>
    /// <param name="heir">The household that inherits.</param>
    /// <param name="parent">The household it inherits from.</param>
    /// <param name="actingUserId">Who is asking.</param>
    /// <remarks>
    /// Only a direct heir: one that inherits through another household is that
    /// household's to cut, and cutting the one in between cuts it too. An heir
    /// that does not inherit from this household is not found, like anything
    /// else the caller has no say over.
    /// </remarks>
    public static Result CanCutInheritance(Household heir, Household parent, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(heir);
        ArgumentNullException.ThrowIfNull(parent);

        return CanAdminister(parent, actingUserId)
            .Bind(() => heir.InheritsFrom == parent.Id
                ? Result.Success()
                : HouseholdErrors.NotFound(heir.Id));
    }

    /// <summary>
    /// An owner may remove anyone; anyone may remove themselves. Neither may
    /// leave the household without an owner.
    /// </summary>
    /// <param name="household">The household in question.</param>
    /// <param name="userId">Who is being removed.</param>
    /// <param name="actingUserId">Who is asking.</param>
    /// <remarks>
    /// The caller's own standing is checked before anything is said about the
    /// target: answering "not a member" to a stranger would tell them the
    /// household exists, and answering it only for some ids would tell them
    /// who is in it.
    /// </remarks>
    public static Result CanRemove(Household household, Guid userId, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);

        var leavingThemselves = userId == actingUserId;
        var permitted = leavingThemselves
            ? CanView(household, actingUserId)
            : CanAdminister(household, actingUserId);

        return permitted.Bind(() =>
        {
            if (household.Find(userId) is not { } target)
            {
                return HouseholdErrors.NotAMember;
            }

            return WouldStrandTheHousehold(household, target.Role)
                ? HouseholdErrors.LastOwner
                : Result.Success();
        });
    }

    /// <summary>
    /// Only an owner changes roles, and not if it would leave no owner behind.
    /// </summary>
    /// <param name="household">The household in question.</param>
    /// <param name="userId">Whose role changes.</param>
    /// <param name="role">The new role.</param>
    /// <param name="actingUserId">Who is asking.</param>
    /// <remarks>
    /// The caller first, as in <see cref="CanRemove"/>, so a stranger learns
    /// nothing about the household or who is in it.
    /// </remarks>
    public static Result CanChangeRole(
        Household household,
        Guid userId,
        HouseholdRole role,
        Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);

        return CanAdminister(household, actingUserId).Bind(() =>
        {
            if (household.Find(userId) is not { } target)
            {
                return HouseholdErrors.NotAMember;
            }

            return target.Role == HouseholdRole.Owner && role != HouseholdRole.Owner
                && household.OwnerCount == 1
                ? HouseholdErrors.LastOwner
                : Result.Success();
        });
    }

    private static bool WouldStrandTheHousehold(Household household, HouseholdRole removedRole) =>
        removedRole == HouseholdRole.Owner && household.OwnerCount == 1;
}
