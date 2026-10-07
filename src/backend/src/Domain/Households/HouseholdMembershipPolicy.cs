using Domain.Shared;

namespace Domain.Households;

/// <summary>
/// Every rule about who may do what in a household, in one place so no two endpoints disagree.
/// </summary>
public static class HouseholdMembershipPolicy
{
    /// <summary>The caller must be a member to see anything at all.</summary>
    /// <remarks>
    /// A non-member gets <c>households.not_found</c>, never "forbidden", which would confirm the
    /// household exists.
    /// </remarks>
    public static Result CanView(Household household, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);

        return household.Find(actingUserId) is null
            ? HouseholdErrors.NotFound(household.Id)
            : Result.Success();
    }

    /// <summary>Inviting, removing, renaming and deleting need an owner.</summary>
    public static Result CanAdminister(Household household, Guid actingUserId)
    {
        ArgumentNullException.ThrowIfNull(household);

        return CanView(household, actingUserId)
            .Bind(() => household.Find(actingUserId)!.Role == HouseholdRole.Owner
                ? Result.Success()
                : HouseholdErrors.NotOwner);
    }

    /// <summary>
    /// Inheriting is an owner's decision about their own household, made with recipes they can
    /// already see.
    /// </summary>
    /// <param name="household">The household that would inherit.</param>
    /// <param name="parent">The household it would inherit from.</param>
    /// <param name="parentLibrary">
    /// Every household whose recipes the parent sees, itself included.
    /// </param>
    /// <param name="actingUserId">Who is asking.</param>
    /// <remarks>
    /// The caller must be in the parent (else it "does not exist"), and a loop is refused: it has
    /// no answer to "whose recipe is this".
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
    /// An owner of the household being inherited from may cut a direct heir loose.
    /// </summary>
    /// <param name="heir">The household that inherits.</param>
    /// <param name="parent">The household it inherits from.</param>
    /// <param name="actingUserId">Who is asking.</param>
    /// <remarks>
    /// An indirect heir is that household's to cut, and is reported as not found.
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
    /// An owner may remove anyone; anyone may remove themselves; neither may leave no owner.
    /// </summary>
    /// <remarks>
    /// The caller's standing is checked before the target's, so a stranger learns nothing about the
    /// household or its members.
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

    /// <summary>Only an owner changes roles, and not if it would leave no owner behind.</summary>
    /// <remarks>The caller is checked first, as in <see cref="CanRemove"/>.</remarks>
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
