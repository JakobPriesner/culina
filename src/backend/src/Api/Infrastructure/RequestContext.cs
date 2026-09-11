namespace Api.Infrastructure;

/// <summary>
/// The per-request values the pipeline puts on <see cref="HttpContext.Items"/>.
/// </summary>
/// <remarks>
/// <c>RequestContextMiddleware</c> writes these; problem documents, log scopes
/// and the SPA's content-security policy read them.
/// </remarks>
internal static class RequestContext
{
    private const string RequestIdKey = "culina.request_id";
    private const string CspNonceKey = "culina.csp_nonce";

    internal static void SetRequestId(HttpContext context, string requestId) =>
        context.Items[RequestIdKey] = requestId;

    /// <summary>
    /// The correlation id, or null before <c>RequestContextMiddleware</c> has
    /// run — which happens for responses written by the host itself.
    /// </summary>
    internal static string? RequestId(HttpContext context) =>
        context.Items.TryGetValue(RequestIdKey, out var value) ? value as string : null;

    internal static void SetCspNonce(HttpContext context, string nonce) =>
        context.Items[CspNonceKey] = nonce;

    /// <summary>The nonce the SPA document's inline script must carry.</summary>
    internal static string? CspNonce(HttpContext context) =>
        context.Items.TryGetValue(CspNonceKey, out var value) ? value as string : null;
}
