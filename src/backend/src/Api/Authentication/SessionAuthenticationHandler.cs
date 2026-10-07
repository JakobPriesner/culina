using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Sessions;
using Domain.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Authentication;

/// <summary>Turns the session cookie into a principal; reads the session row per request so revocation is immediate.</summary>
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
            // No cookie is not a failure: anonymous requests are the norm.
            return AuthenticateResult.NoResult();
        }

        var now = time.GetUtcNow();

        var found = await sessions.FindActiveByTokenAsync(token, now, Context.RequestAborted)
            .ConfigureAwait(false);

        return await found.Match(
            session => AdmitAsync(session, now),
            _ => Task.FromResult(AuthenticateResult.NoResult())).ConfigureAwait(false);
    }

    /// <summary>Admits the session and keeps it from lapsing while used.</summary>
    private async Task<AuthenticateResult> AdmitAsync(Session session, DateTimeOffset now)
    {
        if (!session.IsActive(now))
        {
            return AuthenticateResult.NoResult();
        }

        var renewed = await RenewAsync(session, now).ConfigureAwait(false);

        // Revoked between the read and the renewal: stays revoked.
        return renewed.Match(
            () =>
            {
                // CsrfMiddleware compares against this, saving a second session read.
                RequestContext.SetCsrfTokenHash(Context, session.CsrfTokenHash);

                return AuthenticateResult.Success(TicketFor(session.UserId, session.Id));
            },
            _ => AuthenticateResult.NoResult());
    }

    /// <summary>Slides the session expiry and cookies forward while in use, up to <c>Cookies__MaxSessionDays</c>.</summary>
    private async Task<Result> RenewAsync(Session session, DateTimeOffset now)
    {
        if (!session.IsDueForRenewal(now, cookies.RenewAfter))
        {
            return Result.Success();
        }

        session.Touch(now, cookies.SessionLifetime, cookies.MaxSessionLifetime);

        var renewed = await sessions.RenewAsync(session, Context.RequestAborted).ConfigureAwait(false);

        // Runs before any endpoint, so the response has not started.
        return renewed.Tap(() => SessionCookies.Renew(Context, cookies, now));
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
