using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Api.Infrastructure;

/// <summary>
/// Serves the compressed copy of a static file the build wrote beside it, to a
/// client that says it can read one.
/// </summary>
/// <remarks>
/// <para>
/// Culina compresses no response, because compressing a cookie-authenticated
/// response invites BREACH — and this does not change that. Nothing here
/// compresses anything. The frontend build writes <c>app.js.br</c> and
/// <c>app.js.gz</c> next to <c>app.js</c>, and this only decides which of three
/// files that already exist on disk to send.
/// </para>
/// <para>
/// BREACH needs a secret and attacker-controlled input in the same body. A
/// content-hashed script is the same bytes for every visitor, carries no secret
/// and reflects nothing, so there was nothing to protect by sending it at three
/// times its size — which is what happened: 211 kB for a first load the
/// frontend's own budget measures at 70. The shell document is never among
/// these files. It is rendered per response, with a nonce, and is not
/// compressed.
/// </para>
/// </remarks>
internal static class PrecompressedAssets
{
    /// <summary>What the build writes, best first.</summary>
    private static readonly (string Coding, string Suffix)[] Copies = [("br", ".br"), ("gzip", ".gz")];

    /// <summary>
    /// Content types by the file's own extension, so <c>app.js.br</c> is still
    /// served as JavaScript rather than refused as a type nobody registered.
    /// </summary>
    internal static IContentTypeProvider ContentTypes { get; } =
        new EncodedContentTypes(new FileExtensionContentTypeProvider());

    /// <summary>
    /// Points the request at a compressed copy, when there is one the client
    /// accepts.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="files">The web root.</param>
    /// <remarks>
    /// <para>
    /// <c>Vary: Accept-Encoding</c> goes on every response for a file that has
    /// copies, whichever one is sent. Without it the operator's proxy may keep
    /// the Brotli answer and hand it to a client that never said it could read
    /// Brotli, which is a page that does not load.
    /// </para>
    /// <para>
    /// Only the path changes here. <c>Content-Encoding</c> is set by
    /// <see cref="Encoding"/> when the static file handling actually sends the
    /// copy, so a header can never describe a body that was not sent.
    /// </para>
    /// </remarks>
    internal static void Choose(HttpContext context, IFileProvider files)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(files);

        var request = context.Request;

        if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            || !request.Path.HasValue
            || ApiPaths.IsApi(request.Path))
        {
            return;
        }

        var accepted = request.GetTypedHeaders().AcceptEncoding;
        string? chosen = null;
        var hasCopies = false;

        foreach (var (coding, suffix) in Copies)
        {
            if (!files.GetFileInfo(request.Path.Value + suffix).Exists)
            {
                continue;
            }

            hasCopies = true;

            if (chosen is null && Accepts(accepted, coding))
            {
                chosen = suffix;
            }
        }

        if (!hasCopies)
        {
            return;
        }

        context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptEncoding);

        if (chosen is not null)
        {
            // Concatenated, not Add()ed: Add joins path segments, and ".br"
            // is a suffix on this one rather than a segment after it.
            request.Path = new PathString(request.Path.Value + chosen);
        }
    }

    /// <summary>
    /// The <c>Content-Encoding</c> for a file about to be sent, or null when it
    /// is not one of the compressed copies.
    /// </summary>
    /// <param name="fileName">The name of the file being sent.</param>
    internal static string? Encoding(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        foreach (var (coding, suffix) in Copies)
        {
            if (fileName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return coding;
            }
        }

        return null;
    }

    /// <summary>
    /// The path a client asked for, before a compressed copy was chosen for it.
    /// </summary>
    /// <param name="path">The request path, possibly pointing at a copy.</param>
    /// <remarks>
    /// What caching decisions are made from. The service worker is revalidated
    /// every time because of what it is, and it is still what it is when the
    /// file on its way out is <c>service-worker.js.br</c>.
    /// </remarks>
    internal static PathString Unencoded(PathString path) =>
        path.HasValue ? new PathString(Strip(path.Value)) : path;

    private static string Strip(string value)
    {
        foreach (var (_, suffix) in Copies)
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal))
            {
                return value[..^suffix.Length];
            }
        }

        return value;
    }

    /// <summary>
    /// Whether the client said it can read this coding.
    /// </summary>
    /// <remarks>
    /// A named coding decides for itself, <c>q=0</c> included — <c>br;q=0</c>
    /// is somebody saying no. Only a coding that is not named falls back to
    /// <c>*</c>.
    /// </remarks>
    private static bool Accepts(IList<StringWithQualityHeaderValue> accepted, string coding)
    {
        StringWithQualityHeaderValue? wildcard = null;

        foreach (var value in accepted)
        {
            if (StringSegment.Equals(value.Value, coding, StringComparison.OrdinalIgnoreCase))
            {
                return value.Quality is not 0;
            }

            if (value.Value == "*")
            {
                wildcard = value;
            }
        }

        return wildcard is not null && wildcard.Quality is not 0;
    }

    /// <summary>Looks through a compression suffix to the file it is a copy of.</summary>
    private sealed class EncodedContentTypes(IContentTypeProvider inner) : IContentTypeProvider
    {
        public bool TryGetContentType(string subpath, out string contentType) =>
            inner.TryGetContentType(Strip(subpath), out contentType!);
    }
}
