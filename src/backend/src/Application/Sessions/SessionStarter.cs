using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Sessions;
using Domain.Shared;

namespace Application.Sessions;

/// <summary>Starts a session the one way Culina does, whether a password or an OAuth code signed the person in.</summary>
internal sealed class SessionStarter(ISessionStore sessions, ISecretTokens tokens, CookieSettings cookies)
{
    /// <summary>Stores a new session and hands back the only copies of its secrets.</summary>
    internal async Task<Result<StartedSession>> StartAsync(
        Guid userId,
        SessionClient client,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessionToken = tokens.NewToken();
        var csrfToken = tokens.NewToken();

        var session = Session.Start(
            userId,
            tokens.Digest(sessionToken),
            tokens.Digest(csrfToken),
            now,
            cookies.SessionLifetime,
            ipAddress,
            userAgent,
            client);

        var stored = await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

        return stored.Map(() => new StartedSession(session, sessionToken, csrfToken));
    }

    /// <summary>
    /// Ends the session this browser held before signing in again; the new cookie overwrites the old one, which
    /// would otherwise live on while somebody still used it.
    /// </summary>
    internal async Task EndReplacedAsync(string? previousToken, DateTimeOffset now, CancellationToken cancellationToken)
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
}

/// <summary>A session just started, with its token and CSRF token in the clear, which exist nowhere else.</summary>
/// <param name="Session">The stored session.</param>
/// <param name="SessionToken">What the client sends as its cookie or bearer token.</param>
/// <param name="CsrfToken">What a browser echoes on unsafe requests.</param>
internal sealed record StartedSession(Session Session, string SessionToken, string CsrfToken);
