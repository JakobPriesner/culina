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
/// The digest is the one authentication read with the session, so the token is
/// checked against the very session that let the request in, without reading
/// it again.
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
        ISecretTokens tokens,
        ILogger<CsrfMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(logger);

        if (SafeMethods.Includes(context.Request.Method)
            || context.User.FindFirst(CulinaClaims.SessionId) is null
            || context.GetEndpoint()?.Metadata.GetMetadata<CsrfExempt>() is not null
            || IsValid(context, tokens))
        {
            await next(context).ConfigureAwait(false);

            return;
        }

        CulinaTelemetry.CsrfRejections.Add(1);
        logger.Rejected(context.Request, SessionErrors.CsrfInvalid.Code);

        await CustomResults.WriteProblemAsync(context, SessionErrors.CsrfInvalid).ConfigureAwait(false);
    }

    // Constant-time comparison, in the token service, so a timing signal
    // cannot leak the digest one byte at a time. A principal without a digest
    // was not admitted by the session handler and is refused.
    private static bool IsValid(HttpContext context, ISecretTokens tokens) =>
        context.Request.Headers[CulinaHeaders.Csrf] is [{ Length: > 0 } presented]
        && RequestContext.CsrfTokenHash(context) is { } digest
        && tokens.Matches(presented, digest);
}
