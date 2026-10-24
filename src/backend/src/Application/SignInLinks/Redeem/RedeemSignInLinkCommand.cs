using Application.Abstractions.Messaging;
using Application.Sessions;
using Application.Sessions.SignIn;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;

namespace Application.SignInLinks.Redeem;

/// <summary>Signs a browser in with a mailed sign-in link.</summary>
/// <param name="Token">The token from the link.</param>
/// <param name="IpAddress">The client address, for the devices screen.</param>
/// <param name="UserAgent">The browser, for the devices screen and the new-device notice.</param>
/// <param name="PreviousSessionToken">The session this browser already held, which the new one replaces.</param>
public sealed record RedeemSignInLinkCommand(
    string Token,
    string? IpAddress,
    string? UserAgent,
    string? PreviousSessionToken);

internal sealed class RedeemSignInLinkCommandHandler(LinkSignIn links)
    : ICommandHandler<RedeemSignInLinkCommand, SignInOutcome>
{
    public async Task<Result<SignInOutcome>> Handle(RedeemSignInLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("SignInLinks.Redeem");

        var result = await links.SignInAsync(
            command.Token,
            EmailTokenPurpose.SignIn,
            command.IpAddress,
            command.UserAgent,
            command.PreviousSessionToken,
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
