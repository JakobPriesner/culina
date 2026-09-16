using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Telemetry;
using Domain.Sessions;

namespace Api.Middleware;

/// <summary>
/// Requires unsafe cookie-authenticated requests to echo the session's CSRF
/// token.
/// </summary>
/// <remarks>
/// <para>
/// A synchronizer token, not a double-submit cookie comparison:
/// <c>SameSite=Lax</c> and the origin check are necessary but not sufficient,
/// and only the server knows the digest this session was issued.
/// </para>
/// <para>
/// Safe methods are exempt, which is sound only because no <c>GET</c> endpoint
/// in Culina changes state — a property an architecture-level test asserts
/// rather than assumes.
/// </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class CsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISessionStore sessions,
        ISecretTokens tokens,
        ILogger<CsrfMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(logger);

        if (SafeMethods.Includes(context.Request.Method)
            || context.User.FindFirst(CulinaClaims.SessionId) is null
            || context.GetEndpoint()?.Metadata.GetMetadata<CsrfExempt>() is not null)
        {
            await next(context).ConfigureAwait(false);

            return;
        }

        if (await IsValidAsync(context, sessions, tokens).ConfigureAwait(false))
        {
            await next(context).ConfigureAwait(false);

            return;
        }

        CulinaTelemetry.CsrfRejections.Add(1);
        logger.Rejected(context.Request.Method, context.Request.Path, SessionErrors.CsrfInvalid.Code);

        await CustomResults.WriteProblemAsync(context, SessionErrors.CsrfInvalid).ConfigureAwait(false);
    }

    private static async Task<bool> IsValidAsync(
        HttpContext context,
        ISessionStore sessions,
        ISecretTokens tokens)
    {
        if (context.Request.Headers[CulinaHeaders.Csrf] is not [{ Length: > 0 } presented])
        {
            return false;
        }

        var settings = context.RequestServices
            .GetRequiredService<Application.Abstractions.Settings.CookieSettings>();

        if (SessionCookies.Read(context, settings) is not { Length: > 0 } sessionToken)
        {
            return false;
        }

        var found = await sessions.FindActiveByTokenAsync(sessionToken, context.RequestAborted)
            .ConfigureAwait(false);

        // Constant-time comparison, in the token service, so a timing signal
        // cannot leak the digest one byte at a time.
        return found.Match(
            session => tokens.Matches(presented, session.CsrfTokenHash),
            _ => false);
    }
}
