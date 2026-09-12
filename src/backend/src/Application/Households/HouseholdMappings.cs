using Application.Abstractions;
using Contracts.Households;
using Domain.Households;

namespace Application.Households;

/// <summary>
/// The shapes every household operation shares.
/// </summary>
/// <remarks>
/// Only what genuinely has one meaning across every operation: how a role is
/// spelled on the wire, and how a member view becomes a contract. Each
/// operation still builds its own response explicitly, so adding a field to one
/// cannot silently change another.
/// </remarks>
internal static class HouseholdMappings
{
    internal const string OwnerRole = "owner";

    internal const string MemberRole = "member";

    internal static string ToWord(this HouseholdRole role) =>
        role == HouseholdRole.Owner ? OwnerRole : MemberRole;

    internal static string YourRoleIn(this Household household, Guid callerId) =>
        (household.Find(callerId)?.Role ?? HouseholdRole.Member).ToWord();

    internal static HouseholdRole? ToRole(string? word) => word switch
    {
        OwnerRole => HouseholdRole.Owner,
        MemberRole => HouseholdRole.Member,
        _ => null
    };

    internal static Member ToContract(this HouseholdMemberView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new Member
        {
            UserId = view.UserId,
            DisplayName = view.DisplayName,
            Role = view.Role,
            JoinedAt = view.JoinedAt
        };
    }
}
