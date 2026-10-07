namespace Api.Authentication;

/// <summary>The claims a Culina principal carries.</summary>
/// <remarks>
/// Deliberately two: everything else is read from the database when needed, so a change of name,
/// role or membership applies on the next request, not the next sign-in.
/// </remarks>
internal static class CulinaClaims
{
    /// <summary>The authenticated user's id.</summary>
    internal const string UserId = "sub";

    /// <summary>The session the request is using, for "sign out this device".</summary>
    internal const string SessionId = "sid";

    /// <summary>The authentication scheme's name.</summary>
    internal const string Scheme = "culina.session";
}
