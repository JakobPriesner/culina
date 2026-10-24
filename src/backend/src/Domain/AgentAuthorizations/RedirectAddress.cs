namespace Domain.AgentAuthorizations;

/// <summary>Where an app connecting an AI agent asks Culina to send the person back, with the code.</summary>
/// <remarks>
/// Only an <c>https</c> address, or a loopback <c>http</c> one for an app on the person's own computer (RFC 8252):
/// anything else could hand the code to whoever listens on the network or to another app's URL scheme.
/// </remarks>
public static class RedirectAddress
{
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "[::1]"];

    /// <summary>Whether an app may name this address as one to come back to.</summary>
    public static bool IsAcceptable(string? value)
    {
        if (value is null
            || value.Contains('#', StringComparison.Ordinal)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length > 0
            || uri.Host.Length == 0)
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps || IsLoopback(uri);
    }

    /// <summary>Whether the address only reaches the computer the browser runs on, as a native app's does.</summary>
    public static bool IsLoopback(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsLoopback(uri);

    /// <summary>
    /// Whether the address an authorization request presents is one the app registered: the same string, or, for a
    /// loopback address, the same but for the port, which a native app picks afresh each time (RFC 8252, 7.3).
    /// </summary>
    public static bool Matches(string registered, string presented)
    {
        ArgumentNullException.ThrowIfNull(registered);
        ArgumentNullException.ThrowIfNull(presented);

        if (string.Equals(registered, presented, StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(registered, UriKind.Absolute, out var expected)
            && Uri.TryCreate(presented, UriKind.Absolute, out var actual)
            && IsLoopback(expected)
            && IsLoopback(actual)
            && !presented.Contains('#', StringComparison.Ordinal)
            && string.Equals(WithoutPort(expected), WithoutPort(actual), StringComparison.Ordinal);
    }

    /// <summary>The host the person is sent to, which the consent screen must show (bracketed for IPv6).</summary>
    public static string HostOf(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Host : value;

    private static bool IsLoopback(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp
        && uri.UserInfo.Length == 0
        && LoopbackHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    private static string WithoutPort(Uri uri) =>
        uri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.PathAndQuery, UriFormat.UriEscaped);
}
