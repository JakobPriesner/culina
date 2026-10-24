using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Mail;
using Application.Telemetry;
using Domain.Shared;

namespace Application.SignInLinks.Create;

/// <summary>Mails a link that signs in without a password to the address, if it belongs to an account.</summary>
/// <param name="Email">The address the account signs in with.</param>
public sealed record RequestSignInLinkCommand(string Email);

internal sealed class RequestSignInLinkCommandHandler(
    IUserRepository users,
    AccountMail mail,
    MailThrottle throttle,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RequestSignInLinkCommand>
{
    public async Task<Result> Handle(RequestSignInLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("SignInLinks.Create");

        if (!mail.Sends)
        {
            return tracked.Record(Result.Failure(MailErrors.Off));
        }

        var user = await users.FindByAddressAsync(command.Email, cancellationToken).ConfigureAwait(false);

        // Success either way, as for a reset link. An unconfirmed address gets one too: opening it confirms it.
        if (user is not null && throttle.TryTake("sign_in", user.Id))
        {
            await unitOfWork.InTransactionAsync(
                async token =>
                {
                    await mail.SendSignInLinkAsync(user, token).ConfigureAwait(false);

                    return Result.Success();
                },
                cancellationToken).ConfigureAwait(false);
        }

        return tracked.Record(Result.Success());
    }
}
