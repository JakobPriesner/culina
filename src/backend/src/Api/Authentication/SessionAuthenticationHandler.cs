using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Abstractions;
using Application.Abstractions.Settings;
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

        return found.Match(
            session => session.IsActive(time.GetUtcNow())
                ? AuthenticateResult.Success(TicketFor(session.UserId, session.Id))
                : AuthenticateResult.NoResult(),
            _ => AuthenticateResult.NoResult());
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
