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
    /// An owner may remove anyone; anyone may remove themselves. Neither may
    /// leave the household without an owner.
    /// </summary>
    /// <param name="household">The household in question.</param>
    /// <param name="userId">Who is being removed.</param>
    /// <param name="actingUserId">Who is asking.</param>
    public static Result CanRemove(Household household, Guid userId, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);

        var target = household.Find(userId);

        if (target is null)
        {
            return HouseholdErrors.NotAMember;
        }

        var leavingThemselves = userId == actingUserId;
        var permitted = leavingThemselves
            ? CanView(household, actingUserId)
            : CanAdminister(household, actingUserId);

        return permitted.Bind(() => WouldStrandTheHousehold(household, target.Role)
            ? HouseholdErrors.LastOwner
            : Result.Success());
    }

    /// <summary>
    /// Only an owner changes roles, and not if it would leave no owner behind.
    /// </summary>
    /// <param name="household">The household in question.</param>
    /// <param name="userId">Whose role changes.</param>
    /// <param name="role">The new role.</param>
    /// <param name="actingUserId">Who is asking.</param>
    public static Result CanChangeRole(
        Household household,
        Guid userId,
        HouseholdRole role,
        Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);

        var target = household.Find(userId);

        if (target is null)
        {
            return HouseholdErrors.NotAMember;
        }

        return CanAdminister(household, actingUserId)
            .Bind(() => target.Role == HouseholdRole.Owner && role != HouseholdRole.Owner
                && household.OwnerCount == 1
                ? HouseholdErrors.LastOwner
                : Result.Success());
    }

    private static bool WouldStrandTheHousehold(Household household, HouseholdRole removedRole) =>
        removedRole == HouseholdRole.Owner && household.OwnerCount == 1;
}
