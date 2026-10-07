using Domain.Households;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes household invitations.</summary>
public interface IInvitationRepository
{
    /// <summary>Finds an invitation by its code.</summary>
    /// <returns>
    /// <c>households.invitation_invalid</c> when none matches; an expired or used one gets the same
    /// error.
    /// </returns>
    Task<Result<HouseholdInvitation>> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    /// <summary>The household's invitations that have not been used.</summary>
    Task<IReadOnlyList<HouseholdInvitation>> OpenForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Stores a new invitation.</summary>
    Task<Result> AddAsync(HouseholdInvitation invitation, CancellationToken cancellationToken);

    /// <summary>Saves a redemption.</summary>
    /// <returns>
    /// <c>households.invitation_invalid</c> when another request redeemed it first; the SQL only
    /// matches an unredeemed row.
    /// </returns>
    Task<Result> MarkRedeemedAsync(
        HouseholdInvitation invitation,
        CancellationToken cancellationToken);

    /// <summary>Deletes an invitation before it is used.</summary>
    Task<Result> RevokeAsync(
        Guid invitationId,
        Guid householdId,
        CancellationToken cancellationToken);
}
