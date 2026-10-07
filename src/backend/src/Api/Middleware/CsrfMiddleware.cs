using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Telemetry;
using Domain.Sessions;

namespace Api.Middleware;

/// <summary>Requires unsafe cookie-authenticated requests to echo the session's CSRF token.</summary>
/// <remarks>
/// A synchronizer token, not double-submit: only the server knows the digest, which comes from the same read that authenticated the session.
/// Safe methods are exempt because no <c>GET</c> changes state, which an architecture test asserts.
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

    // Constant-time comparison in the token service, so timing cannot leak the digest. A principal without a digest was not admitted by the session handler.
    private static bool IsValid(HttpContext context, ISecretTokens tokens) =>
        context.Request.Headers[CulinaHeaders.Csrf] is [{ Length: > 0 } presented]
        && RequestContext.CsrfTokenHash(context) is { } digest
        && tokens.Matches(presented, digest);
}
