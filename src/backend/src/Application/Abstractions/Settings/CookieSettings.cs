namespace Application.Abstractions.Settings;

/// <summary>
/// The session cookie's attributes.
/// </summary>
/// <remarks>
/// The name is fixed rather than configurable: the <c>__Host-</c> prefix is
/// what pins the cookie to this exact origin, and a deployment that could
/// rename it could also accidentally drop the prefix and the protection with
/// it.
/// </remarks>
public sealed record CookieSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Cookies";

    /// <summary>
    /// The session cookie's name. The <c>__Host-</c> prefix requires
    /// <c>Secure</c>, <c>Path=/</c> and no <c>Domain</c> attribute, so a
    /// subdomain cannot set or overwrite it.
    /// </summary>
    public const string SessionCookieName = "__Host-culina.session";

    /// <summary>
    /// The readable companion cookie carrying the CSRF token. Not
    /// <c>HttpOnly</c>, because the client has to echo it back in a header.
    /// </summary>
    public const string CsrfCookieName = "culina.csrf";

    /// <summary>
    /// Whether cookies are marked <c>Secure</c>. Only local HTTP development
    /// justifies false — with it true, the <c>__Host-</c> prefix means the
    /// browser refuses the cookie over plain HTTP and login cannot work.
    /// </summary>
    public bool Secure { get; init; } = true;

    /// <summary>How long a session lives without activity.</summary>
    public int SessionDays { get; init; } = 30;

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate() => SettingsGuard.InRange(SessionDays, 1, 365, SectionName, nameof(SessionDays));
}
