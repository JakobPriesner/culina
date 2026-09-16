using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Authentication;

/// <summary>
/// Turns the session cookie into a principal.
/// </summary>
/// <remarks>
/// A handler of our own rather than the framework's cookie authentication,
/// because Culina's cookie carries an opaque reference and not a serialised
/// ticket. Every request therefore reads the session row, which is what makes
/// revocation immediate — the cost is one indexed lookup.
/// </remarks>
internal sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionStore sessions,
    CookieSettings cookies,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (SessionCookies.Read(Context, cookies) is not { Length: > 0 } token)
        {
            // No cookie is not a failure: most requests to the app shell and to
            // static files are anonymous, and reporting failure would fill the
            // log with noise.
            return AuthenticateResult.NoResult();
        }

        var found = await sessions.FindActiveByTokenAsync(token, Context.RequestAborted)
            .ConfigureAwait(false);

        return await found.Match(
            AdmitAsync,
            _ => Task.FromResult(AuthenticateResult.NoResult())).ConfigureAwait(false);
    }

    /// <summary>Admits the session, and keeps it from lapsing while it is used.</summary>
    private async Task<AuthenticateResult> AdmitAsync(Session session)
    {
        var now = time.GetUtcNow();

        if (!session.IsActive(now))
        {
            return AuthenticateResult.NoResult();
        }

        await RenewAsync(session, now).ConfigureAwait(false);

        return AuthenticateResult.Success(TicketFor(session.UserId, session.Id));
    }

    /// <summary>
    /// Keeps a session that is being used from lapsing.
    /// </summary>
    /// <remarks>
    /// Culina has no refresh token, because the cookie is an opaque reference
    /// and not a self-contained one: there is nothing to exchange, and a
    /// revoked session stops working on the next request rather than at the end
    /// of an access token's life. This is what takes its place — the row's
    /// expiry and both cookies move forward while the session is in use, so
    /// <c>Cookies__SessionDays</c> means "thirty days unused" rather than
    /// "thirty days from signing in".
    /// </remarks>
    private async Task RenewAsync(Session session, DateTimeOffset now)
    {
        if (!session.IsDueForRenewal(now, cookies.RenewAfter))
        {
            return;
        }

        session.Touch(now, cookies.SessionLifetime);

        await sessions.UpdateAsync(session, Context.RequestAborted).ConfigureAwait(false);

        // Authentication runs before any endpoint, so the response has not
        // started and a cookie can still be added to it.
        SessionCookies.Renew(Context, cookies, now);
    }

    private AuthenticationTicket TicketFor(Guid userId, Guid sessionId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(CulinaClaims.UserId, userId.ToString()),
                new Claim(CulinaClaims.SessionId, sessionId.ToString())
            ],
            CulinaClaims.Scheme,
            nameType: CulinaClaims.UserId,
            roleType: null);

        return new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
    }
}
