using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.RedeemInvitation.Response;

namespace Application.Households.RedeemInvitation;

/// <summary>Joins a household with an invitation code.</summary>
/// <param name="Code">The code the caller was given.</param>
/// <param name="UserId">Who is joining.</param>
public sealed record RedeemInvitationCommand(string Code, Guid UserId);

internal sealed class RedeemInvitationCommandHandler(
    IInvitationRepository invitations,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<RedeemInvitationCommand, Response>
{
    public async Task<Result<Response>> Handle(
        RedeemInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.RedeemInvitation");

        var found = await invitations
            .FindByCodeAsync(command.Code, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            invitation => JoinAsync(invitation, command.UserId, cancellationToken),
            // Unknown, expired and used all arrive as one error, so a code cannot be probed.
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> JoinAsync(
        HouseholdInvitation invitation,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        var redeemed = invitation.Redeem(userId, now);

        return await redeemed.Match(
            () => JoinUnlessInAsync(invitation, userId, now, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<Response>> JoinUnlessInAsync(
        HouseholdInvitation invitation,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var member = await households
            .IsMemberAsync(invitation.HouseholdId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (member)
        {
            return await AlreadyInAsync(invitation, cancellationToken).ConfigureAwait(false);
        }

        return await unitOfWork
            .InTransactionAsync(token => AddMemberAsync(invitation, userId, now, token), cancellationToken)
            .ConfigureAwait(false);
    }

    // Somebody opening a link to a household they are already in (usually its owner): not an error, and the code is not used up.
    // Only asked once the code is known good, so it reveals nothing.
    private async Task<Result<Response>> AlreadyInAsync(
        HouseholdInvitation invitation,
        CancellationToken cancellationToken)
    {
        var household = await households
            .FindAsync(invitation.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        return household.Map(found => new Response
        {
            HouseholdId = found.Id,
            Name = found.Name.Value,
            AlreadyMember = true
        });
    }

    private async Task<Result<Response>> AddMemberAsync(
        HouseholdInvitation invitation,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Marked used first: the update only matches an unredeemed row, so racing requests settle in SQL and one proceeds.
        var marked = await invitations
            .MarkRedeemedAsync(invitation, cancellationToken)
            .ConfigureAwait(false);

        var household = await households
            .FindAsync(invitation.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var joined = marked.Bind(() => household)
            .Bind(found => found
                .Add(userId, HouseholdRole.Member, now)
                .Map(() => found));

        return await joined.Match(
            async found =>
            {
                var saved = await households
                    .UpdateAsync(found, found.Version, cancellationToken)
                    .ConfigureAwait(false);

                return saved.Map(_ => new Response
                {
                    HouseholdId = found.Id,
                    Name = found.Name.Value,
                    AlreadyMember = false
                });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);
    }
}
