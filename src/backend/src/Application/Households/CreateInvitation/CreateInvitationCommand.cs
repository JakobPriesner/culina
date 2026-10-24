using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Mail;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Domain.Users;
using Response = Contracts.Households.CreateInvitation.Response;

namespace Application.Households.CreateInvitation;

/// <summary>Issues an invitation to a household. Owners only.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is inviting.</param>
/// <param name="Email">An address to mail the invitation to, or null to only show the code.</param>
public sealed record CreateInvitationCommand(Guid HouseholdId, Guid UserId, string? Email = null);

internal sealed class CreateInvitationCommandHandler(
    IHouseholdRepository households,
    IInvitationRepository invitations,
    IUserRepository users,
    ISecretTokens tokens,
    AccountMail mail,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateInvitationCommand, Response>
{
    public async Task<Result<Response>> Handle(
        CreateInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.CreateInvitation");

        var found = await households.FindAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);

        var permitted = found.Bind(household =>
            HouseholdMembershipPolicy.CanAdminister(household, command.UserId).Map(() => household));

        var recipient = Recipient(command.Email);

        var result = await permitted.Bind(household => recipient.Map(to => (Household: household, To: to))).Match(
            ready => IssueAsync(command, ready.Household, ready.To.Address, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>The address to mail, checked before anything is written; an empty box means none.</summary>
    private Result<Invitee> Recipient(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Invitee(null);
        }

        if (!mail.Sends)
        {
            return new FieldError("email", MailErrors.Off.Code, MailErrors.Off.Description);
        }

        return Email.Create(raw).Match(
            email => Result<Invitee>.Success(new Invitee(email)),
            error => Result<Invitee>.Failure(new FieldError("email", error.Code, error.Description)));
    }

    private async Task<Result<Response>> IssueAsync(
        CreateInvitationCommand command,
        Household household,
        Email? to,
        CancellationToken cancellationToken)
    {
        var code = tokens.NewToken();

        var invitation = HouseholdInvitation.Issue(
            command.HouseholdId,
            tokens.Digest(code),
            command.UserId,
            time.GetUtcNow());

        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await invitations.AddAsync(invitation, token).ConfigureAwait(false);

                var mailed = await added.Match(
                    () => MailAsync(command.UserId, household, invitation, code, to, token),
                    error => Task.FromResult(Result<bool>.Failure(error))).ConfigureAwait(false);

                // The code leaves here and is never recoverable (only its digest is stored): this
                // response is the one chance to show it.
                return mailed.Map(sent => new Response
                {
                    InvitationId = invitation.Id,
                    Code = code,
                    ExpiresAt = invitation.ExpiresAt,
                    MailedTo = sent ? to?.Value : null
                });
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<bool>> MailAsync(
        Guid inviterId,
        Household household,
        HouseholdInvitation invitation,
        string code,
        Email? to,
        CancellationToken cancellationToken)
    {
        if (to is null)
        {
            return false;
        }

        var inviter = await users.FindAsync(inviterId, cancellationToken).ConfigureAwait(false);

        return await inviter.Match(
            async user => Result<bool>.Success(await mail
                .InviteAsync(to, user.DisplayName.Value, household.Name.Value, code, invitation.ExpiresAt, cancellationToken)
                .ConfigureAwait(false)),
            error => Task.FromResult(Result<bool>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>Who the invitation is mailed to, if anybody.</summary>
    private sealed record Invitee(Email? Address);
}
