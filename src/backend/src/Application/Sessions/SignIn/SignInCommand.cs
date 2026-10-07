using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Sessions;
using Domain.Shared;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Application.Sessions.SignIn;

/// <summary>Signs a user in.</summary>
/// <param name="Email">The address they registered with.</param>
/// <param name="Password">Their password.</param>
/// <param name="IpAddress">
/// The client address, for the devices screen and for whose attempt budget the
/// guess is counted against.
/// </param>
/// <param name="UserAgent">The browser, for the devices screen.</param>
/// <param name="PreviousSessionToken">
/// The session cookie this browser already held, if any, which the new one
/// replaces.
/// </param>
public sealed record SignInCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent,
    string? PreviousSessionToken);

internal sealed class SignInCommandHandler(
    IUserRepository users,
    ISessionStore sessions,
    ISecretTokens tokens,
    IPasswordHasher passwordHasher,
    ILoginAttempts attempts,
    SignInDependencies dependencies,
    ILogger<SignInCommandHandler> logger)
    : ICommandHandler<SignInCommand, SignInOutcome>
{
    public async Task<Result<SignInOutcome>> Handle(
        SignInCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Sessions.SignIn");

        var now = dependencies.Time.GetUtcNow();
        var accountKey = AccountKey.For(command.Email);

        // Counted before the password is checked, so simultaneous guesses
        // cannot all slip through before the first failure is written down.
        if (!attempts.TryReserve(accountKey, command.IpAddress, now))
        {
            // Reported as invalid credentials rather than "too many attempts":
            // telling an attacker they found a real account and merely locked
            // it out is the one thing this whole path exists to avoid.
            return tracked.Record(Refused());
        }

        var user = await FindAsync(command.Email, cancellationToken).ConfigureAwait(false);

        // Verified even when no account matched, against a decoy hash, so the
        // response takes the same time either way. Skipping the work for an
        // unknown address turns this endpoint into an enumeration oracle no
        // matter how careful the error message is.
        var verification = await passwordHasher.VerifyAsync(
            command.Password,
            user?.PasswordHash ?? passwordHasher.DecoyHash,
            cancellationToken).ConfigureAwait(false);

        if (user is null || verification == PasswordVerification.Failed)
        {
            return tracked.Record(Refused());
        }

        attempts.Succeeded(accountKey, command.IpAddress, now);

        if (verification == PasswordVerification.ValidButNeedsRehash)
        {
            await UpgradeHashAsync(user, command.Password, cancellationToken).ConfigureAwait(false);
        }

        var outcome = await StartSessionAsync(user, command, now, cancellationToken).ConfigureAwait(false);

        await outcome.Match(
            _ => EndReplacedSessionAsync(command.PreviousSessionToken, now, cancellationToken),
            _ => Task.CompletedTask).ConfigureAwait(false);

        tracked.Tag("culina.user_id", user.Id);

        return tracked.Record(outcome);
    }

    /// <summary>
    /// Null for both "not an address" and "no such account", because the caller
    /// must treat them identically.
    /// </summary>
    private async Task<User?> FindAsync(string rawEmail, CancellationToken cancellationToken)
    {
        var found = await Email.Create(rawEmail).Match(
            email => users.FindByEmailAsync(email, cancellationToken),
            error => Task.FromResult(Result<User>.Failure(error))).ConfigureAwait(false);

        return found.Match<User?>(user => user, _ => null);
    }

    private Result<SignInOutcome> Refused()
    {
        CulinaTelemetry.LoginFailures.Add(1);
        AuthenticationLogs.SignInRefused(logger);

        return SessionErrors.InvalidCredentials;
    }

    /// <summary>
    /// Ends the session this browser held before signing in again.
    /// </summary>
    /// <remarks>
    /// The new cookie overwrites the old one, so nobody should be holding the
    /// old session any more — and if somebody still is, it is not this
    /// browser. Left alone it would live on for as long as it kept being used.
    /// </remarks>
    private async Task EndReplacedSessionAsync(
        string? previousToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(previousToken))
        {
            return;
        }

        var previous = await sessions.FindActiveByTokenAsync(previousToken, now, cancellationToken)
            .ConfigureAwait(false);

        // Nothing to report either way: a session that is already gone is
        // what this wants.
        await previous.Match(
            session => sessions.RevokeAsync(session.Id, session.UserId, now, cancellationToken),
            _ => Task.FromResult(Result.Success())).ConfigureAwait(false);
    }

    private async Task UpgradeHashAsync(User user, string password, CancellationToken cancellationToken)
    {
        // The owner proved the password, so the stored hash can be replaced
        // with one at the current cost — no reset email, no interruption.
        user.ChangePasswordHash(await passwordHasher.HashAsync(password, cancellationToken).ConfigureAwait(false));

        await users.UpdateAsync(user, user.Version, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<SignInOutcome>> StartSessionAsync(
        User user,
        SignInCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessionToken = tokens.NewToken();
        var csrfToken = tokens.NewToken();

        var session = Session.Start(
            user.Id,
            tokens.Digest(sessionToken),
            tokens.Digest(csrfToken),
            now,
            dependencies.Cookies.SessionLifetime,
            command.IpAddress,
            command.UserAgent);

        var stored = await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);
        var isAdmin = await users.IsAdminAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return stored.Bind(() => Result<SignInOutcome>.Success(
            new SignInOutcome(user.ToSignInResponse(isAdmin, csrfToken), sessionToken)));
    }
}
