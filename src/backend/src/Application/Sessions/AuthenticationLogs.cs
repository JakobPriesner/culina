using Microsoft.Extensions.Logging;

namespace Application.Sessions;

/// <summary>
/// Refused credentials, each under an event id of its own.
/// </summary>
/// <remarks>
/// <para>
/// The counters say how many; these say from where. The request scope carries
/// the client address, so fail2ban or CrowdSec can key on the event id and ban
/// the address without anything here naming it twice.
/// </para>
/// <para>
/// Never the email address that was tried: a mistyped password is often the
/// password for something else, typed into the wrong box, and an address next
/// to it is half a credential.
/// </para>
/// </remarks>
internal static partial class AuthenticationLogs
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Refused a sign-in: wrong email address or password, or the account is locked out")]
    internal static partial void SignInRefused(ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Refused a signed-in user's password confirmation")]
    internal static partial void ConfirmationRefused(ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Refused a password reset: wrong, used or expired recovery code, or the account is locked out")]
    internal static partial void RecoveryRefused(ILogger logger);
}
