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

    /// <summary>
    /// Requests allowed per hour against libraries this household has connected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from <see cref="ImportsPerHour"/>, and generous, because the
    /// risk is different in kind rather than in degree. That limit exists to
    /// stop an account using this server to reach addresses it picks one at a
    /// time; a connected source is a single fixed address a member of the
    /// household set up with a credential, visible on a screen, and the same
    /// one for every request.
    /// </para>
    /// <para>
    /// It has to be generous to be correct, and the number is worked out rather
    /// than felt. Recipes travel five to a request — small enough that a batch
    /// finishes inside the client's deadline once photos are counted — so a
    /// library of five thousand is fifty reads to see it and a thousand batches
    /// to bring it over. A ceiling that makes the advertised feature impossible
    /// is not a safety measure, which is what six hundred turned out to be.
    /// </para>
    /// </remarks>
    public int SourceRequestsPerHour { get; init; } = 1500;

    /// <summary>
    /// Reads of shared recipes allowed per minute from one address.
    /// </summary>
    /// <remarks>
    /// Not a guess-the-token defence — 256 bits is not walked at any rate —
    /// but the only endpoint that serves a household's content to an
    /// anonymous caller, and the one place where a limit is the difference
    /// between a link and a feed. Generous enough for a page and its photo
    /// several times over, on a shared connection.
    /// </remarks>
    public int SharedRecipesPerIpPerMinute { get; init; } = 120;

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
        SettingsGuard.InRange(SourceRequestsPerHour, 1, 100_000, SectionName, nameof(SourceRequestsPerHour));
        SettingsGuard.InRange(SharedRecipesPerIpPerMinute, 1, 100_000, SectionName, nameof(SharedRecipesPerIpPerMinute));
        SettingsGuard.InRange(RequestsPerSessionPerMinute, 10, 100_000, SectionName, nameof(RequestsPerSessionPerMinute));
    }
}
