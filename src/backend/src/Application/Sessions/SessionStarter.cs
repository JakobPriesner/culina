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
}

/// <summary>A session just started, with its token and CSRF token in the clear, which exist nowhere else.</summary>
/// <param name="Session">The stored session.</param>
/// <param name="SessionToken">What the client sends as its cookie or bearer token.</param>
/// <param name="CsrfToken">What a browser echoes on unsafe requests.</param>
internal sealed record StartedSession(Session Session, string SessionToken, string CsrfToken);
