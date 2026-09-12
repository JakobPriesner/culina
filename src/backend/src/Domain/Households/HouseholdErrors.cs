using Domain.Shared;

namespace Domain.Households;

/// <summary>Every failure the households module can return.</summary>
public static class HouseholdErrors
{
    /// <summary>
    /// No such household — or one the caller cannot see.
    /// </summary>
    /// <remarks>
    /// A non-member gets this rather than a 403, deliberately. Answering
    /// "forbidden" would confirm the household exists, which is exactly the
    /// fact a caller who is not a member has no business learning.
    /// </remarks>
    public static Error NotFound(Guid householdId) => new(
        "households.not_found",
        $"No household with id '{householdId}' exists.",
        ErrorType.NotFound);

    /// <summary>The caller is a member, but this needs an owner.</summary>
    public static readonly Error NotOwner = new(
        "households.not_owner",
        "Only an owner of this household can do that.",
        ErrorType.Forbidden);

    /// <summary>The change would leave the household with no owner.</summary>
    public static readonly Error LastOwner = new(
        "households.last_owner",
        "A household needs at least one owner. Make someone else an owner first.",
        ErrorType.Conflict);

    /// <summary>That user is already in this household.</summary>
    public static readonly Error AlreadyAMember = new(
        "households.already_a_member",
        "That person is already in this household.",
        ErrorType.Conflict);

    /// <summary>That user is not in this household.</summary>
    public static readonly Error NotAMember = new(
        "households.not_a_member",
        "That person is not in this household.",
        ErrorType.NotFound);

    /// <summary>
    /// The invitation code is unknown, expired or already used.
    /// </summary>
    /// <remarks>
    /// One error for all three cases on purpose: distinguishing them would let
    /// someone probe codes for validity.
    /// </remarks>
    public static readonly Error InvitationInvalid = new(
        "households.invitation_invalid",
        "That invitation is not valid. Ask for a new one.",
        ErrorType.NotFound);

    /// <summary>The role is not one this app recognises.</summary>
    public static readonly Error InvalidRole = new(
        "households.invalid_role",
        "A member is either an owner or a member.",
        ErrorType.Validation);

    /// <summary>The household name is blank or too long.</summary>
    public static readonly Error InvalidName = new(
        "households.invalid_name",
        "A household name is required, and it may be at most 80 characters.",
        ErrorType.Validation);
}
