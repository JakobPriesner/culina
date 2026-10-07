using System.Text.RegularExpressions;

namespace Api.Infrastructure;

/// <summary>Redacts share tokens and invitation codes (secrets that travel in the path) before a path is logged or put on a span.</summary>
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
