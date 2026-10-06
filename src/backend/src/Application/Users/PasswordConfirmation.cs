using Application.Abstractions;
using Application.Sessions;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Users;

/// <summary>
/// Asks a signed-in person for their password again before something that
/// would let a stolen session keep the account.
/// </summary>
/// <remarks>
/// Changing the password and making recovery codes both hand out a way back
/// in that outlives the session, so a session alone is not enough for either.
/// The check draws on the same per-account budget as signing in: otherwise a
/// stolen session would be an unlimited password oracle.
/// </remarks>
/// <param name="passwordHasher">Verifies the password.</param>
/// <param name="attempts">Counts failures per account.</param>
/// <param name="time">The injected clock.</param>
/// <param name="logger">Records a refusal.</param>
internal sealed class PasswordConfirmation(
    IPasswordHasher passwordHasher,
    ILoginAttempts attempts,
    TimeProvider time,
    ILogger<PasswordConfirmation> logger)
{
    /// <summary>Succeeds when the password is the account's current one.</summary>
    /// <param name="user">Whose password.</param>
    /// <param name="password">What they typed.</param>
    /// <param name="clientAddress">Where they are, which decides whose attempt budget is used.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    internal async Task<Result> ConfirmAsync(
        User user,
        string? password,
        string? clientAddress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = time.GetUtcNow();
        var accountKey = AccountKey.For(user.Email.Value);

        if (attempts.TryReserve(accountKey, clientAddress, now)
            && await passwordHasher.VerifyAsync(password ?? string.Empty, user.PasswordHash, cancellationToken)
                .ConfigureAwait(false) != PasswordVerification.Failed)
        {
            attempts.Succeeded(accountKey, clientAddress, now);

            return Result.Success();
        }

        CulinaTelemetry.LoginFailures.Add(1);
        AuthenticationLogs.ConfirmationRefused(logger);

        return UserErrors.IncorrectPassword;
    }
}
