using Domain.Households;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes household invitations.</summary>
public interface IInvitationRepository
{
    /// <summary>Finds an invitation by its code.</summary>
    /// <param name="code">The raw code the caller presented.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>
    /// <c>households.invitation_invalid</c> when no invitation matches — the
    /// same error an expired or used one gets.
    /// </returns>
    Task<Result<HouseholdInvitation>> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    /// <summary>The household's invitations that have not been used.</summary>
    /// <param name="householdId">Which household.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<HouseholdInvitation>> OpenForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Stores a new invitation.</summary>
    /// <param name="invitation">The invitation to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> AddAsync(HouseholdInvitation invitation, CancellationToken cancellationToken);

    /// <summary>Saves a redemption.</summary>
    /// <param name="invitation">The redeemed invitation.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <c>households.invitation_invalid</c> when another request redeemed it
    /// first: the write only matches a row that is still unredeemed, so the
    /// race is settled in SQL.
    /// </returns>
    Task<Result> MarkRedeemedAsync(
        HouseholdInvitation invitation,
        CancellationToken cancellationToken);

    /// <summary>Deletes an invitation before it is used.</summary>
    /// <param name="invitationId">Which invitation.</param>
    /// <param name="householdId">Which household it must belong to.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> RevokeAsync(
        Guid invitationId,
        Guid householdId,
        CancellationToken cancellationToken);
}
