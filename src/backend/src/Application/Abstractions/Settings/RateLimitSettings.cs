namespace Application.Abstractions.Settings;

/// <summary>The rate limits guarding the endpoints an attacker would hammer; login is limited per IP and per account.</summary>
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

    /// <summary>Invitation redemptions per hour from one address, and as many reads of which household a code is for.</summary>
    public int InvitationPerIpPerHour { get; init; } = 10;

    /// <summary>Recipe imports per hour from one person: the one operation that fetches an address somebody else chose.</summary>
    public int ImportsPerHour { get; init; } = 30;

    /// <summary>Requests per hour from one person against libraries their household has connected.</summary>
    /// <remarks>
    /// Separate from <see cref="ImportsPerHour"/> and generous: a connected source is one fixed address set up
    /// with a credential. Recipes travel five per request, so a library of five thousand needs about a thousand batches.
    /// </remarks>
    public int SourceRequestsPerHour { get; init; } = 1500;

    /// <summary>Reads of shared recipes per minute from one address.</summary>
    /// <remarks>The only endpoint serving household content anonymously; the limit separates a link from a feed.</remarks>
    public int SharedRecipesPerIpPerMinute { get; init; } = 120;

    /// <summary>Assistant requests per hour from one person: the one limit about money rather than load.</summary>
    public int AssistantRequestsPerHour { get; init; } = 60;

    /// <summary>Archives one person may take or restore per hour.</summary>
    /// <remarks>An archive streams every recipe with every photograph inline; a few an hour allows a retry, not a loop.</remarks>
    public int ArchiveExportsPerHour { get; init; } = 5;

    /// <summary>Requests per minute from one address, signed in or not.</summary>
    /// <remarks>Counted per address because the limiter runs before the session cookie is checked.</remarks>
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
        SettingsGuard.InRange(AssistantRequestsPerHour, 1, 10_000, SectionName, nameof(AssistantRequestsPerHour));
        SettingsGuard.InRange(ArchiveExportsPerHour, 1, 1_000, SectionName, nameof(ArchiveExportsPerHour));
        SettingsGuard.InRange(RequestsPerSessionPerMinute, 10, 100_000, SectionName, nameof(RequestsPerSessionPerMinute));
    }
}
