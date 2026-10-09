namespace Api.Infrastructure;

/// <summary>The per-request values the pipeline puts on <see cref="HttpContext.Items"/>, written by <c>RequestContextMiddleware</c> and authentication.</summary>
internal static class RequestContext
{
    private const string RequestIdKey = "culina.request_id";
    private const string CspNonceKey = "culina.csp_nonce";
    private const string ErrorCodeKey = "culina.error_code";
    private const string CsrfTokenHashKey = "culina.csrf_token_hash";
    private const string HashedAssetKey = "culina.hashed_asset";

    internal static void SetRequestId(HttpContext context, string requestId) =>
        context.Items[RequestIdKey] = requestId;

    /// <summary>The correlation id, or null before <c>RequestContextMiddleware</c> has run (e.g. host-written responses).</summary>
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

    /// <summary>The CSRF digest of the session this request authenticated with, or null when none was admitted.</summary>
    internal static ReadOnlyMemory<byte>? CsrfTokenHash(HttpContext context) =>
        context.Items.TryGetValue(CsrfTokenHashKey, out var value) ? value as ReadOnlyMemory<byte>? : null;

    /// <summary>Records that the static file middleware is answering with a content-hashed asset.</summary>
    internal static void MarkHashedAsset(HttpContext context) =>
        context.Items[HashedAssetKey] = true;

    /// <summary>Whether the response is a hashed asset, which only ever loads as a subresource.</summary>
    internal static bool IsHashedAsset(HttpContext context) =>
        context.Items.ContainsKey(HashedAssetKey);
}
