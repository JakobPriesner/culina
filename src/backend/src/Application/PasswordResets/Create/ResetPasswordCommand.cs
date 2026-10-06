using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Sessions;
using Application.Telemetry;
using Domain.Sessions;
using Domain.Shared;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.PasswordResets.Create;

/// <summary>Sets a new password for an account, unlocked by a recovery code.</summary>
/// <param name="Email">The address the account signs in with.</param>
/// <param name="Code">A saved or issued recovery code, as typed.</param>
/// <param name="Password">The new password.</param>
/// <param name="IpAddress">The client address, which decides whose attempt budget is used.</param>
public sealed record ResetPasswordCommand(string Email, string Code, string Password, string? IpAddress);

internal sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    IRecoveryCodeRepository recoveryCodes,
    ISessionStore sessions,
    ISecretTokens tokens,
    IPasswordHasher passwordHasher,
    ILoginAttempts attempts,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ILogger<ResetPasswordCommandHandler> logger)
    : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("PasswordResets.Create");

        // First, and before anything is looked up: a password that is too short
        // is refused the same way whoever is asking, so saying so reveals
        // nothing about the account.
        var result = await User.EnsureAcceptablePassword(command.Password).Match(
            () => AttemptAsync(command, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result> AttemptAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var accountKey = AccountKey.For(command.Email);

        // Counted before the code is checked, as sign-in does, and reported as
        // an invalid code rather than "too many attempts" for the same reason:
        // a lockout confirms the account exists.
        var result = !attempts.TryReserve(accountKey, command.IpAddress, now)
            ? SessionErrors.InvalidRecoveryCode
            : await Email.Create(command.Email).Match(
                email => RedeemAsync(email, command, now, cancellationToken),
                _ => Task.FromResult(Result.Failure(SessionErrors.InvalidRecoveryCode))).ConfigureAwait(false);

        result.Match(
            () => attempts.Succeeded(accountKey, command.IpAddress, now),
            _ =>
            {
                CulinaTelemetry.RecoveryFailures.Add(1);
                AuthenticationLogs.RecoveryRefused(logger);
            });

        return result;
    }

    private Task<Result> RedeemAsync(
        Email email,
        ResetPasswordCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        unitOfWork.InTransactionAsync(
            async token =>
            {
                var redeemed = await recoveryCodes
                    .RedeemAsync(email, tokens.Digest(RecoveryCode.Normalise(command.Code)), now, token)
                    .ConfigureAwait(false);

                var found = await redeemed.Match(
                    userId => users.FindAsync(userId, token),
                    error => Task.FromResult(Result<User>.Failure(error))).ConfigureAwait(false);

                return await found.Match(
                    user => ReplacePasswordAsync(user, command.Password, now, token),
                    error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken);

    private async Task<Result> ReplacePasswordAsync(
        User user,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Hashed only once the code is known to be good, so guessing codes
        // costs the server a lookup and never an Argon2 run.
        user.ChangePasswordHash(await passwordHasher.HashAsync(password, cancellationToken).ConfigureAwait(false));

        var saved = await users.UpdateAsync(user, user.Version, cancellationToken).ConfigureAwait(false);

        // Whoever was signed in as this account — perhaps the person who took
        // the old password — is signed out everywhere.
        await sessions.RevokeAllAsync(user.Id, keepSessionId: null, now, cancellationToken).ConfigureAwait(false);

        return saved.Bind(_ => Result.Success());
    }
}
