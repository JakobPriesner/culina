namespace Application.Abstractions.Settings;

/// <summary>
/// The rate limits guarding the endpoints an attacker would hammer.
/// </summary>
/// <remarks>
/// Login is limited per IP <em>and</em> per account: per-IP alone lets a
/// botnet spread an attack on one account across many addresses, and
/// per-account alone lets one address walk a password list across many
/// accounts.
/// </remarks>
public sealed record RateLimitSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "RateLimits";

    /// <summary>Login attempts allowed per minute from one address.</summary>
    public int LoginPerIpPerMinute { get; init; } = 10;

    /// <summary>Login attempts allowed per minute against one account.</summary>
    public int LoginPerAccountPerMinute { get; init; } = 5;

    /// <summary>Registrations allowed per hour from one address.</summary>
    public int RegisterPerIpPerHour { get; init; } = 5;

    /// <summary>Invitation redemptions allowed per hour from one address.</summary>
    public int InvitationPerIpPerHour { get; init; } = 10;

    /// <summary>
    /// Recipe imports allowed per hour from one caller.
    /// </summary>
    /// <remarks>
    /// The one operation that makes the server fetch an address somebody else
    /// chose. Generous for a person writing down recipes, and nowhere near
    /// enough to sweep a network with.
    /// </remarks>
    public int ImportsPerHour { get; init; } = 30;

    /// <summary>Requests allowed per minute from one authenticated session.</summary>
    public int RequestsPerSessionPerMinute { get; init; } = 600;

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        SettingsGuard.InRange(LoginPerIpPerMinute, 1, 10_000, SectionName, nameof(LoginPerIpPerMinute));
        SettingsGuard.InRange(LoginPerAccountPerMinute, 1, 10_000, SectionName, nameof(LoginPerAccountPerMinute));
        SettingsGuard.InRange(RegisterPerIpPerHour, 1, 10_000, SectionName, nameof(RegisterPerIpPerHour));
        SettingsGuard.InRange(InvitationPerIpPerHour, 1, 10_000, SectionName, nameof(InvitationPerIpPerHour));
        SettingsGuard.InRange(ImportsPerHour, 1, 10_000, SectionName, nameof(ImportsPerHour));
        SettingsGuard.InRange(RequestsPerSessionPerMinute, 10, 100_000, SectionName, nameof(RequestsPerSessionPerMinute));
    }
}
