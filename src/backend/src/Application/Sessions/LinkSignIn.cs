using Application.Abstractions;
using Application.Sessions.SignIn;
using Domain.Sessions;
using Domain.Shared;
using Domain.Users;

namespace Application.Sessions;

/// <summary>Signs a browser in with a link mailed to the account's address; opening it also confirms the address.</summary>
internal sealed class LinkSignIn(
    IUserRepository users,
    IEmailTokenRepository emailTokens,
    ISecretTokens tokens,
    SessionStarter starter,
    NewDeviceNotice notice,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    internal async Task<Result<SignInOutcome>> SignInAsync(
        string token,
        EmailTokenPurpose purpose,
        string? ipAddress,
        string? userAgent,
        string? previousSessionToken,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        var confirmed = await unitOfWork.InTransactionAsync(
            async transaction =>
            {
                var redeemed = await emailTokens
                    .RedeemAsync(tokens.Digest(token), purpose, now, transaction)
                    .ConfigureAwait(false);

                var found = await redeemed.Match(
                    userId => users.FindAsync(userId, transaction),
                    error => Task.FromResult(Result<User>.Failure(error))).ConfigureAwait(false);

                return await found.Match(
                    user => ConfirmAsync(user, now, transaction),
                    // An account deleted since the mail went out reads like any other dead link.
                    _ => Task.FromResult(Result<User>.Failure(SessionErrors.InvalidLink))).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return await confirmed.Match(
            user => StartAsync(user, ipAddress, userAgent, previousSessionToken, now, cancellationToken),
            error => Task.FromResult(Result<SignInOutcome>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>Whoever opened a link mailed to the address reads that inbox, which is all a confirmation proves.</summary>
    private async Task<Result<User>> ConfirmAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (user.HasConfirmedEmail)
        {
            return user;
        }

        user.ConfirmEmail(now);

        var saved = await users.UpdateAsync(user, user.Version, cancellationToken).ConfigureAwait(false);

        return saved.Map(_ => user);
    }

    private async Task<Result<SignInOutcome>> StartAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        string? previousSessionToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var started = await starter
            .StartAsync(user.Id, SessionClient.Browser, ipAddress, userAgent, now, cancellationToken)
            .ConfigureAwait(false);

        return await started.Match(
            async session =>
            {
                await starter.EndReplacedAsync(previousSessionToken, now, cancellationToken).ConfigureAwait(false);
                await notice.RecordAsync(user, SessionClient.Browser, userAgent, now, cancellationToken).ConfigureAwait(false);

                var isAdmin = await users.IsAdminAsync(user.Id, cancellationToken).ConfigureAwait(false);

                return Result<SignInOutcome>.Success(new SignInOutcome(
                    user.ToSignInResponse(isAdmin, session.CsrfToken),
                    session.SessionToken,
                    SessionClient.Browser));
            },
            error => Task.FromResult(Result<SignInOutcome>.Failure(error))).ConfigureAwait(false);
    }
}
