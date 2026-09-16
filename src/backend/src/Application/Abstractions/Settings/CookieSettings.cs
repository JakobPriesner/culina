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

    /// <summary>
    /// How long a session may go unused before the next request extends it.
    /// </summary>
    /// <remarks>
    /// The sliding renewal is what keeps somebody signed in on a device they
    /// actually use: without it <see cref="SessionDays"/> counts from the
    /// moment they signed in, and using Culina every day makes no difference to
    /// the day they are signed out. Renewing on every request would turn the
    /// one indexed read an authenticated request costs into a read and a write,
    /// so it happens at most once per interval — a day is invisible against a
    /// thirty-day lifetime and costs one write per device per day. Zero renews
    /// on every request, which only a test has a reason to ask for.
    /// </remarks>
    public int RenewAfterHours { get; init; } = 24;

    /// <summary>How long a session lives without activity, as a span.</summary>
    public TimeSpan SessionLifetime => TimeSpan.FromDays(SessionDays);

    /// <summary>How long a session may sit idle before a request renews it.</summary>
    public TimeSpan RenewAfter => TimeSpan.FromHours(RenewAfterHours);

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        SettingsGuard.InRange(SessionDays, 1, 365, SectionName, nameof(SessionDays));

        // Never longer than the lifetime itself: an interval that outlives the
        // session it is meant to extend is a renewal that never happens, and
        // the symptom is people being signed out for no visible reason.
        SettingsGuard.InRange(RenewAfterHours, 0, SessionDays * 24, SectionName, nameof(RenewAfterHours));
    }
}
