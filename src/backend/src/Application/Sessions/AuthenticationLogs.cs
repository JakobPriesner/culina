using Microsoft.Extensions.Logging;

namespace Application.Sessions;

/// <summary>
/// Refused credentials, each under its own event id so fail2ban or CrowdSec can key on it.
/// Never log the email tried: a mistyped password is often another account's, and the address makes it half a credential.
/// </summary>
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
