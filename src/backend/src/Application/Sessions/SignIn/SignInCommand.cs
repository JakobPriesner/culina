using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Sessions;
using Domain.Shared;
using Domain.Users;

namespace Application.Sessions.SignIn;

/// <summary>Signs a user in.</summary>
/// <param name="Email">The address they registered with.</param>
/// <param name="Password">Their password.</param>
/// <param name="IpAddress">The client address, for the devices screen.</param>
/// <param name="UserAgent">The browser, for the devices screen.</param>
public sealed record SignInCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent);

internal sealed class SignInCommandHandler(
    IUserRepository users,
    ISessionStore sessions,
    ISecretTokens tokens,
    IPasswordHasher passwordHasher,
    ILoginAttempts attempts,
    SignInDependencies dependencies)
    : ICommandHandler<SignInCommand, SignInOutcome>
{
    public async Task<Result<SignInOutcome>> Handle(
        SignInCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Sessions.SignIn");

        var now = dependencies.Time.GetUtcNow();
        var accountKey = AccountKey(command.Email);

        if (attempts.IsLockedOut(accountKey, now))
        {
            // Reported as invalid credentials rather than "too many attempts":
            // telling an attacker they found a real account and merely locked
            // it out is the one thing this whole path exists to avoid.
            return tracked.Record(Failed(accountKey, now));
        }

        var user = await FindAsync(command.Email, cancellationToken).ConfigureAwait(false);

        // Verified even when no account matched, against a decoy hash, so the
        // response takes the same time either way. Skipping the work for an
        // unknown address turns this endpoint into an enumeration oracle no
        // matter how careful the error message is.
        var verification = passwordHasher.Verify(
            command.Password,
            user?.PasswordHash ?? passwordHasher.DecoyHash);

        if (user is null || verification == PasswordVerification.Failed)
        {
            return tracked.Record(Failed(accountKey, now));
        }

        attempts.Clear(accountKey);

        if (verification == PasswordVerification.ValidButNeedsRehash)
        {
            await UpgradeHashAsync(user, command.Password, cancellationToken).ConfigureAwait(false);
        }

        var outcome = await StartSessionAsync(user, command, now, cancellationToken).ConfigureAwait(false);

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

    private Result<SignInOutcome> Failed(string accountKey, DateTimeOffset now)
    {
        attempts.RecordFailure(accountKey, now);
        CulinaTelemetry.LoginFailures.Add(1);

        return SessionErrors.InvalidCredentials;
    }

    private async Task UpgradeHashAsync(User user, string password, CancellationToken cancellationToken)
    {
        // The owner proved the password, so the stored hash can be replaced
        // with one at the current cost — no reset email, no interruption.
        user.ChangePasswordHash(passwordHasher.Hash(password));

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

    /// <summary>
    /// A digest of the normalised address, because the attempt counter lives in
    /// memory and shows up in diagnostics, and neither should hold an email.
    /// </summary>
    private static string AccountKey(string rawEmail) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawEmail.Trim().ToLowerInvariant())));
}
