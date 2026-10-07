namespace Application.Abstractions.Settings;

/// <summary>The session cookie's attributes.</summary>
/// <remarks>
/// The name is fixed: the <c>__Host-</c> prefix pins the cookie to this origin, and a configurable name could drop it.
/// </remarks>
public sealed record CookieSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Cookies";

    /// <summary>The session cookie's name. The <c>__Host-</c> prefix requires <c>Secure</c>, <c>Path=/</c> and no <c>Domain</c>.</summary>
    public const string SessionCookieName = "__Host-culina.session";

    /// <summary>The readable CSRF companion cookie. Not <c>HttpOnly</c>: the client echoes it in a header.</summary>
    public const string CsrfCookieName = "culina.csrf";

    /// <summary>The key that allows insecure cookies outside Development: <c>Cookies__AllowInsecureOutsideDevelopment=true</c>.</summary>
    public const string AllowInsecureKey = "AllowInsecureOutsideDevelopment";

    /// <summary>Whether cookies are marked <c>Secure</c>. Only local HTTP development justifies false; the <c>__Host-</c> prefix otherwise makes login impossible.</summary>
    public bool Secure { get; init; } = true;

    /// <summary>Whether this deployment may run with <see cref="Secure"/> off: always in Development, elsewhere only via <see cref="AllowInsecureKey"/>.</summary>
    /// <remarks>
    /// Decided at startup, never saved by the app: it is the operator's risk, not something an administrator or the first visitor to setup can switch on.
    /// </remarks>
    public bool InsecureAllowed { get; init; }

    /// <summary>Secure cookies are off where nobody allowed it.</summary>
    public bool InsecureWithoutConsent => !Secure && !InsecureAllowed;

    /// <summary>How long a session lives without activity.</summary>
    public int SessionDays { get; init; } = 30;

    /// <summary>How long a session may go unused before the next request extends it.</summary>
    /// <remarks>
    /// Sliding renewal keeps used devices signed in; at most once per interval so a request stays a read, not a write. Zero renews every request (tests only).
    /// </remarks>
    public int RenewAfterHours { get; init; } = 24;

    /// <summary>How long a session may live at all, however much it is used, so a stolen cookie cannot renew forever.</summary>
    public int MaxSessionDays { get; init; } = 90;

    /// <summary>How long a session lives without activity, as a span.</summary>
    public TimeSpan SessionLifetime => TimeSpan.FromDays(SessionDays);

    /// <summary>How long a session may live at all, as a span.</summary>
    public TimeSpan MaxSessionLifetime => TimeSpan.FromDays(MaxSessionDays);

    /// <summary>How long a session may sit idle before a request renews it.</summary>
    public TimeSpan RenewAfter => TimeSpan.FromHours(RenewAfterHours);

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        if (InsecureWithoutConsent)
        {
            throw new InvalidOperationException(
                $"Configuration {SectionName}__{nameof(Secure)} is false outside the Development environment, "
                + "which sends the session cookie without Secure and without its __Host- prefix. Serve Culina "
                + $"over HTTPS and leave it true, or set {SectionName}__{AllowInsecureKey}=true to accept that.");
        }

        SettingsGuard.InRange(SessionDays, 1, 365, SectionName, nameof(SessionDays));

        // Never longer than the lifetime itself, or renewal never happens and people are signed out unexplained.
        SettingsGuard.InRange(RenewAfterHours, 0, SessionDays * 24, SectionName, nameof(RenewAfterHours));

        // Never shorter than the idle lifetime; ten years is as good as no ceiling.
        SettingsGuard.InRange(MaxSessionDays, SessionDays, 3650, SectionName, nameof(MaxSessionDays));
    }
}
