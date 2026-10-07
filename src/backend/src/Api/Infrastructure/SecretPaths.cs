using System.Text.RegularExpressions;

namespace Api.Infrastructure;

/// <summary>
/// Paths as they may be written down: in a log line or on a span.
/// </summary>
/// <remarks>
/// <para>
/// A share token and an invitation code travel in the path, and each is the
/// key to what it opens: whoever reads one in a collector or a log store can
/// read the shared recipe or join the household. So the segment that carries
/// one is replaced, in the API's routes and in the app's own pages for them,
/// under every route below it — a route added there later is covered without
/// anybody remembering this file.
/// </para>
/// <para>
/// Everything else is left as it is: an id in a path names a row, and opens
/// nothing on its own.
/// </para>
/// </remarks>
internal static partial class SecretPaths
{
    internal const string Placeholder = "***";

    /// <summary>The path with any share token or invitation code in it replaced.</summary>
    internal static string Redact(PathString path) =>
        Secret().Replace(path.Value ?? string.Empty, $"$1/{Placeholder}");

    [GeneratedRegex(
        "^(/api/v[0-9]+/shared-recipes|/api/v[0-9]+/invitations|/shared|/join)/[^/]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Secret();
}
