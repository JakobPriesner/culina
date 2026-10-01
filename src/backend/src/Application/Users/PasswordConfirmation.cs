using Application.Abstractions;
using Application.Sessions;
using Application.Telemetry;
using Domain.Shared;
using Domain.Users;

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
internal sealed class PasswordConfirmation(
    IPasswordHasher passwordHasher,
    ILoginAttempts attempts,
    TimeProvider time)
{
    /// <summary>Succeeds when the password is the account's current one.</summary>
    /// <param name="user">Whose password.</param>
    /// <param name="password">What they typed.</param>
    internal Result Confirm(User user, string? password)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = time.GetUtcNow();
        var accountKey = AccountKey.For(user.Email.Value);

        if (!attempts.IsLockedOut(accountKey, now)
            && passwordHasher.Verify(password ?? string.Empty, user.PasswordHash) != PasswordVerification.Failed)
        {
            attempts.Clear(accountKey);

            return Result.Success();
        }

        attempts.RecordFailure(accountKey, now);
        CulinaTelemetry.LoginFailures.Add(1);

        return UserErrors.IncorrectPassword;
    }
}
