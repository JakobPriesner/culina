using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Api.Infrastructure;

/// <summary>Serves the build's precompressed copy of a static file to a client that accepts one.</summary>
/// <remarks>
/// Nothing is compressed at runtime (BREACH): this only picks among files already on disk. Content-hashed
/// scripts hold no secret and reflect nothing; the per-response shell document is never among them.
/// </remarks>
internal static class PrecompressedAssets
{
    private static readonly (string Coding, string Suffix)[] Copies = [("br", ".br"), ("gzip", ".gz")];

    /// <summary>Content types by the file's own extension, so <c>app.js.br</c> is still served as JavaScript.</summary>
    internal static IContentTypeProvider ContentTypes { get; } =
        new EncodedContentTypes(new FileExtensionContentTypeProvider());

    /// <summary>Points the request at a compressed copy, when there is one the client accepts.</summary>
    /// <remarks>
    /// <c>Vary: Accept-Encoding</c> goes on every response for a file with copies, so a proxy cannot serve
    /// Brotli to a client that cannot read it. <c>Content-Encoding</c> is set by <see cref="Encoding"/> only when the copy is sent.
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
            // Concatenated: Add() joins path segments, and ".br" is a suffix.
            request.Path = new PathString(request.Path.Value + chosen);
        }
    }

    /// <summary>The <c>Content-Encoding</c> for a file about to be sent, or null when it is not a compressed copy.</summary>
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

    /// <summary>The path a client asked for, before a compressed copy was chosen (caching decisions are made from it).</summary>
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

    // A named coding decides for itself (br;q=0 is a no); only unnamed codings fall back to *.
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

    private sealed class EncodedContentTypes(IContentTypeProvider inner) : IContentTypeProvider
    {
        public bool TryGetContentType(string subpath, out string contentType) =>
            inner.TryGetContentType(Strip(subpath), out contentType!);
    }
}
