namespace Api.Infrastructure;

/// <summary>
/// The per-request values the pipeline puts on <see cref="HttpContext.Items"/>.
/// </summary>
/// <remarks>
/// <c>RequestContextMiddleware</c> writes these; problem documents, log scopes
/// and the SPA's content-security policy read them. A problem document leaves
/// its error code here for the request line, and authentication leaves the
/// session's CSRF digest for <c>CsrfMiddleware</c>.
/// </remarks>
internal static class RequestContext
{
    private const string RequestIdKey = "culina.request_id";
    private const string CspNonceKey = "culina.csp_nonce";
    private const string ErrorCodeKey = "culina.error_code";
    private const string CsrfTokenHashKey = "culina.csrf_token_hash";

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

    internal static void SetErrorCode(HttpContext context, string code) =>
        context.Items[ErrorCodeKey] = code;

    /// <summary>The code of the problem document the response carried, if any.</summary>
    internal static string? ErrorCode(HttpContext context) =>
        context.Items.TryGetValue(ErrorCodeKey, out var value) ? value as string : null;

    internal static void SetCsrfTokenHash(HttpContext context, ReadOnlyMemory<byte> digest) =>
        context.Items[CsrfTokenHashKey] = digest;

    /// <summary>
    /// The CSRF digest of the session this request was authenticated with, or
    /// null when authentication admitted none.
    /// </summary>
    internal static ReadOnlyMemory<byte>? CsrfTokenHash(HttpContext context) =>
        context.Items.TryGetValue(CsrfTokenHashKey, out var value) ? value as ReadOnlyMemory<byte>? : null;
}
