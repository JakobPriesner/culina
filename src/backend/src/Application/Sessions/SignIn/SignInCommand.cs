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
/// The client address, for the devices screen and the attempt budget.
/// </param>
/// <param name="UserAgent">The browser, for the devices screen.</param>
/// <param name="PreviousSessionToken">
/// The session this device already held (cookie or bearer token), which the new one replaces.
/// </param>
/// <param name="Client">
/// <c>browser</c> (or null) for a cookie session; <c>ios</c> or <c>android</c> for an app's bearer
/// token.
/// </param>
public sealed record SignInCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent,
    string? PreviousSessionToken,
    string? Client = null);

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

        // Checked before the attempt budget: a malformed request is not a guess.
        if (ToClient(command.Client) is not { } client)
        {
            return tracked.Record(Result<SignInOutcome>.Failure(
                new FieldError("client", SessionErrors.InvalidClient.Code, SessionErrors.InvalidClient.Description)));
        }

        var now = dependencies.Time.GetUtcNow();
        var accountKey = AccountKey.For(command.Email);

        // Counted before the password is checked, so simultaneous guesses cannot all pass before
        // the first failure is recorded.
        if (!attempts.TryReserve(accountKey, command.IpAddress, now))
        {
            // Reported as invalid credentials, not "too many attempts": that would tell an attacker
            // they found a real account.
            return tracked.Record(Refused());
        }

        var user = await FindAsync(command.Email, cancellationToken).ConfigureAwait(false);

        // Verified even when no account matched, against a decoy hash, so timing does not make this
        // endpoint an enumeration oracle.
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

        var outcome = await StartSessionAsync(user, command, client, now, cancellationToken).ConfigureAwait(false);

        await outcome.Match(
            _ => EndReplacedSessionAsync(command.PreviousSessionToken, now, cancellationToken),
            _ => Task.CompletedTask).ConfigureAwait(false);

        tracked.Tag("culina.user_id", user.Id);

        return tracked.Record(outcome);
    }

    /// <summary>
    /// Null for both "not an address" and "no such account": the caller must treat them
    /// identically.
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
    /// Ends the session this browser held before signing in again; the new cookie overwrites the
    /// old one, which would otherwise live on while somebody still used it.
    /// </summary>
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

        // Nothing to report either way: a session that is already gone is what this wants.
        await previous.Match(
            session => sessions.RevokeAsync(session.Id, session.UserId, now, cancellationToken),
            _ => Task.FromResult(Result.Success())).ConfigureAwait(false);
    }

    private async Task UpgradeHashAsync(User user, string password, CancellationToken cancellationToken)
    {
        // The owner proved the password, so the stored hash is replaced with one at the current
        // cost, with no reset email.
        user.ChangePasswordHash(await passwordHasher.HashAsync(password, cancellationToken).ConfigureAwait(false));

        await users.UpdateAsync(user, user.Version, cancellationToken).ConfigureAwait(false);
    }

    private static SessionClient? ToClient(string? client) => client switch
    {
        null or "browser" => SessionClient.Browser,
        "ios" => SessionClient.Ios,
        "android" => SessionClient.Android,
        _ => null
    };

    private async Task<Result<SignInOutcome>> StartSessionAsync(
        User user,
        SignInCommand command,
        SessionClient client,
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
            command.UserAgent,
            client);

        var stored = await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);
        var isAdmin = await users.IsAdminAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return stored.Bind(() => Result<SignInOutcome>.Success(
            new SignInOutcome(user.ToSignInResponse(isAdmin, csrfToken), sessionToken, client)));
    }
}
