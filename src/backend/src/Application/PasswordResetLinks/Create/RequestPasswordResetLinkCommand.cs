using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Mail;
using Application.Telemetry;
using Domain.Shared;

namespace Application.PasswordResetLinks.Create;

/// <summary>Mails a link that sets a new password to the address, if it belongs to an account.</summary>
/// <param name="Email">The address the account signs in with.</param>
public sealed record RequestPasswordResetLinkCommand(string Email);

internal sealed class RequestPasswordResetLinkCommandHandler(
    IUserRepository users,
    AccountMail mail,
    MailThrottle throttle,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RequestPasswordResetLinkCommand>
{
    public async Task<Result> Handle(RequestPasswordResetLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("PasswordResetLinks.Create");

        // Refused only when mail is off, which is the same for every address and lets the page say so.
        if (!mail.Sends)
        {
            return tracked.Record(Result.Failure(MailErrors.Off));
        }

        var user = await users.FindByAddressAsync(command.Email, cancellationToken).ConfigureAwait(false);

        // Success either way: whether a mail went out would tell anybody which addresses have accounts.
        if (user is not null && throttle.TryTake("password_reset", user.Id))
        {
            await unitOfWork.InTransactionAsync(
                async token =>
                {
                    await mail.SendPasswordResetAsync(user, token).ConfigureAwait(false);

                    return Result.Success();
                },
                cancellationToken).ConfigureAwait(false);
        }

        return tracked.Record(Result.Success());
    }
}
