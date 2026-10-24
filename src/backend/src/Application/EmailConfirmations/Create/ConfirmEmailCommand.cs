using Application.Abstractions.Messaging;
using Application.Sessions;
using Application.Sessions.SignIn;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;

namespace Application.EmailConfirmations.Create;

/// <summary>Confirms an account's address with the mailed link, and signs the browser that opened it in.</summary>
/// <param name="Token">The token from the link.</param>
/// <param name="IpAddress">The client address, for the devices screen.</param>
/// <param name="UserAgent">The browser, for the devices screen.</param>
/// <param name="PreviousSessionToken">The session this browser already held, which the new one replaces.</param>
/// <remarks>
/// Signed in because the account's password was set at registration and the link proves the inbox: asking for the
/// password straight after would only add a step.
/// </remarks>
public sealed record ConfirmEmailCommand(
    string Token,
    string? IpAddress,
    string? UserAgent,
    string? PreviousSessionToken);

internal sealed class ConfirmEmailCommandHandler(LinkSignIn links)
    : ICommandHandler<ConfirmEmailCommand, SignInOutcome>
{
    public async Task<Result<SignInOutcome>> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("EmailConfirmations.Create");

        var result = await links.SignInAsync(
            command.Token,
            EmailTokenPurpose.EmailConfirmation,
            command.IpAddress,
            command.UserAgent,
            command.PreviousSessionToken,
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
