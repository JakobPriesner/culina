namespace Api.Authentication;

/// <summary>The claims a Culina principal carries.</summary>
/// <remarks>
/// Deliberately two. Everything else about a user is read from the database
/// when it is needed, so a change of name, role or membership takes effect on
/// the next request rather than on the next sign-in.
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
